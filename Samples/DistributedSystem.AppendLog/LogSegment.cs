using System.Buffers.Binary;
using System.Text;

namespace DistributedSystem.AppendLog;

public sealed class LogSegment
{
    public string DirectoryPath { get; }
    public long BaseOffset { get; }
    public DateTime CreatedAt { get; }
    public long NextOffset { get; private set; }
    public long LastOffset => NextOffset - 1;

    public string DataFilePath { get; }
    public string OffsetIndexPath { get; }
    public string TimeIndexPath { get; }
    public string TransactionIndexPath { get; }

    public AppendLogConfig Config { get; }
    public OffsetIndex OffsetIndex { get; }
    public TimeIndex TimeIndex { get; }
    public TransactionIndex TransactionIndex { get; }

    private long _bytesSinceLastIndexEntry;

    public LogSegment(string directoryPath, long baseOffset, AppendLogConfig? config = null)
    {
        Config = config ?? new AppendLogConfig();
        DirectoryPath = directoryPath;
        BaseOffset = baseOffset;
        CreatedAt = DateTime.UtcNow;
        DataFilePath = Path.Combine(directoryPath, $"segment-{baseOffset}.log");
        OffsetIndexPath = Path.Combine(directoryPath, $"segment-{baseOffset}.offset.idx");
        TimeIndexPath = Path.Combine(directoryPath, $"segment-{baseOffset}.time.idx");
        TransactionIndexPath = Path.Combine(directoryPath, $"segment-{baseOffset}.txn.idx");

        Directory.CreateDirectory(directoryPath);
        OffsetIndex = OffsetIndex.Load(OffsetIndexPath, Config.OffsetIndexInterval);
        TimeIndex = TimeIndex.Load(TimeIndexPath, Config.TimeIndexMaxEntries, Config.TimeIndexSparseInterval);
        TransactionIndex = TransactionIndex.Load(TransactionIndexPath);
        NextOffset = CalculateNextOffset();
    }

    private long CalculateNextOffset()
    {
        if (!File.Exists(DataFilePath))
        {
            return BaseOffset;
        }

        using var stream = File.OpenRead(DataFilePath);
        while (stream.Position < stream.Length)
        {
            if (!TryReadBatchHeader(stream, out var batch))
            {
                break;
            }

            SkipBatchRecords(stream, batch.Count);
            NextOffset = batch.LastOffset + 1;
        }

        return NextOffset == 0 ? BaseOffset : NextOffset;
    }

    private static bool TryReadBatchHeader(Stream stream, out RecordBatch batch)
    {
        batch = default!;
        var headerSize = sizeof(long) * 2 + sizeof(int);
        var headerBuffer = new byte[headerSize];
        if (stream.Read(headerBuffer) != headerSize)
        {
            return false;
        }

        var baseOffset = BinaryPrimitives.ReadInt64LittleEndian(headerBuffer[..8]);
        var timestampTicks = BinaryPrimitives.ReadInt64LittleEndian(headerBuffer[8..16]);
        var count = BinaryPrimitives.ReadInt32LittleEndian(headerBuffer[16..20]);
        var timestamp = new DateTime(timestampTicks, DateTimeKind.Utc);

        batch = new RecordBatch(baseOffset, timestamp, Array.Empty<Record>());
        return true;
    }

    private static void SkipBatchRecords(Stream stream, int count)
    {
        for (var i = 0; i < count; i++)
        {
            var lengthBuffer = new byte[sizeof(int)];
            stream.Read(lengthBuffer);
            var keyLen = BinaryPrimitives.ReadInt32LittleEndian(lengthBuffer);
            stream.Seek(keyLen, SeekOrigin.Current);
            stream.Read(lengthBuffer);
            var valueLen = BinaryPrimitives.ReadInt32LittleEndian(lengthBuffer);
            stream.Seek(valueLen + sizeof(long), SeekOrigin.Current);
        }
    }

    public void Append(RecordBatch batch)
    {
        if (batch.BaseOffset != NextOffset)
        {
            throw new InvalidOperationException($"Batch base offset {batch.BaseOffset} must equal next offset {NextOffset}.");
        }

        using var stream = File.Open(DataFilePath, FileMode.Append, FileAccess.Write, FileShare.Read);
        var position = stream.Position;
        WriteBatch(stream, batch);
        stream.Flush();

        var written = stream.Position - position;
        _bytesSinceLastIndexEntry += written;

        if (OffsetIndex.Entries.Count == 0 || _bytesSinceLastIndexEntry >= Config.IndexIntervalBytes)
        {
            OffsetIndex.Append(batch.BaseOffset, position, force: true);
            TimeIndex.Append(batch.BaseOffset, batch.Timestamp);
            _bytesSinceLastIndexEntry = 0;
            SaveIndexes();
        }

        NextOffset = batch.LastOffset + 1;
    }

    private static void WriteBatch(Stream stream, RecordBatch batch)
    {
        var header = new byte[sizeof(long) * 2 + sizeof(int)];
        BinaryPrimitives.WriteInt64LittleEndian(header[..8], batch.BaseOffset);
        BinaryPrimitives.WriteInt64LittleEndian(header[8..16], batch.Timestamp.Ticks);
        BinaryPrimitives.WriteInt32LittleEndian(header[16..20], batch.Count);
        stream.Write(header);

        foreach (var record in batch.Records)
        {
            var keyLen = record.Key.Length;
            var valueLen = record.Value.Length;
            var buffer = new byte[sizeof(int) * 2 + sizeof(long) + keyLen + valueLen];
            BinaryPrimitives.WriteInt32LittleEndian(buffer[..4], keyLen);
            BinaryPrimitives.WriteInt32LittleEndian(buffer[4..8], valueLen);
            BinaryPrimitives.WriteInt64LittleEndian(buffer[8..16], record.Timestamp.Ticks);
            record.Key.CopyTo(buffer.AsSpan(16, keyLen));
            record.Value.CopyTo(buffer.AsSpan(16 + keyLen, valueLen));
            stream.Write(buffer);
        }
    }

    public bool ShouldRoll(long maxSegmentBytes, TimeSpan maxSegmentAge)
    {
        var size = File.Exists(DataFilePath) ? new FileInfo(DataFilePath).Length : 0;
        return size >= maxSegmentBytes || DateTime.UtcNow - CreatedAt >= maxSegmentAge;
    }

    public void TruncateTo(long offset)
    {
        if (offset < BaseOffset)
        {
            throw new InvalidOperationException("Cannot truncate before base offset.");
        }

        if (offset >= LastOffset)
        {
            return;
        }

        var targetPosition = FindPositionForOffset(offset + 1);
        if (targetPosition < 0)
        {
            throw new InvalidOperationException("Offset boundary not found for truncation.");
        }

        using var stream = File.Open(DataFilePath, FileMode.Open, FileAccess.Write, FileShare.None);
        stream.SetLength(targetPosition);
        stream.Flush();

        ReloadIndexes(offset);
        NextOffset = offset + 1;
    }

    private long FindPositionForOffset(long targetOffset)
    {
        var hint = OffsetIndex.Lookup(targetOffset);
        var position = hint?.Position ?? 0L;

        using var stream = File.OpenRead(DataFilePath);
        stream.Seek(position, SeekOrigin.Begin);

        while (stream.Position < stream.Length)
        {
            var headerBuffer = new byte[sizeof(long) * 2 + sizeof(int)];
            if (stream.Read(headerBuffer) != headerBuffer.Length)
            {
                break;
            }

            var baseOffset = BinaryPrimitives.ReadInt64LittleEndian(headerBuffer[..8]);
            var count = BinaryPrimitives.ReadInt32LittleEndian(headerBuffer[16..20]);
            var batchStart = baseOffset;
            var batchEnd = baseOffset + count - 1;

            if (batchEnd >= targetOffset)
            {
                return stream.Position - headerBuffer.Length;
            }

            SkipBatchRecords(stream, count);
        }

        return -1;
    }

    private void ReloadIndexes(long truncateOffset)
    {
        OffsetIndex.TruncateTo(truncateOffset);
        TimeIndex.TruncateTo(truncateOffset);
        SaveIndexes();
    }

    public TransactionIndexEntry? LookupTransaction(long transactionId)
    {
        return TransactionIndex.Lookup(transactionId);
    }

    public IEnumerable<RecordBatch> ReadFrom(long offset)
    {
        var hint = OffsetIndex.Lookup(offset) ?? new OffsetIndexEntry(BaseOffset, 0);
        using var stream = File.OpenRead(DataFilePath);
        stream.Seek(hint.Position, SeekOrigin.Begin);

        while (stream.Position < stream.Length)
        {
            var headerBuffer = new byte[sizeof(long) * 2 + sizeof(int)];
            if (stream.Read(headerBuffer) != headerBuffer.Length)
            {
                yield break;
            }

            var baseOffset = BinaryPrimitives.ReadInt64LittleEndian(headerBuffer[..8]);
            var timestamp = new DateTime(BinaryPrimitives.ReadInt64LittleEndian(headerBuffer[8..16]), DateTimeKind.Utc);
            var count = BinaryPrimitives.ReadInt32LittleEndian(headerBuffer[16..20]);
            var records = new List<Record>(count);

            for (var i = 0; i < count; i++)
            {
                var header = new byte[sizeof(int) * 2 + sizeof(long)];
                stream.Read(header);
                var keyLen = BinaryPrimitives.ReadInt32LittleEndian(header[..4]);
                var valueLen = BinaryPrimitives.ReadInt32LittleEndian(header[4..8]);
                var recordTimestamp = new DateTime(BinaryPrimitives.ReadInt64LittleEndian(header[8..16]), DateTimeKind.Utc);
                var key = new byte[keyLen];
                var value = new byte[valueLen];
                stream.Read(key);
                stream.Read(value);
                records.Add(new Record(key, value, recordTimestamp));
            }

            var batch = new RecordBatch(baseOffset, timestamp, records);
            if (batch.LastOffset >= offset)
            {
                yield return batch;
            }
        }
    }

    public RecordBatch? ReadBatchAtOffset(long offset)
    {
        var position = FindPositionForOffset(offset);
        if (position < 0)
        {
            return null;
        }

        using var stream = File.OpenRead(DataFilePath);
        stream.Seek(position, SeekOrigin.Begin);

        if (!TryReadBatchHeader(stream, out var batch))
        {
            return null;
        }

        var records = new List<Record>(batch.Count);
        for (var i = 0; i < batch.Count; i++)
        {
            var header = new byte[sizeof(int) * 2 + sizeof(long)];
            stream.Read(header);
            var keyLen = BinaryPrimitives.ReadInt32LittleEndian(header[..4]);
            var valueLen = BinaryPrimitives.ReadInt32LittleEndian(header[4..8]);
            var recordTimestamp = new DateTime(BinaryPrimitives.ReadInt64LittleEndian(header[8..16]), DateTimeKind.Utc);
            var key = new byte[keyLen];
            var value = new byte[valueLen];
            stream.Read(key);
            stream.Read(value);
            records.Add(new Record(key, value, recordTimestamp));
        }

        return new RecordBatch(batch.BaseOffset, batch.Timestamp, records);
    }

    private void SaveIndexes()
    {
        OffsetIndex.Save(OffsetIndexPath);
        TimeIndex.Save(TimeIndexPath);
    }
}
