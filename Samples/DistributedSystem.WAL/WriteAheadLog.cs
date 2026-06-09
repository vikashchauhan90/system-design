using System.Buffers.Binary;
using System.Text;

namespace DistributedSystem.WAL;

public sealed class WriteAheadLog : IDisposable
{
    private const int HeaderSize = sizeof(ulong) + sizeof(long) + sizeof(long);
    private readonly string _directory;
    private readonly string _logFilePath;
    private FileStream _stream;
    private long _nextSequenceNumber;
    private bool _disposed;

    public long LastSequenceNumber => _nextSequenceNumber - 1;
    public string LogFilePath => _logFilePath;

    public WriteAheadLog(string directory, string fileName = "wal.log")
    {
        if (string.IsNullOrWhiteSpace(directory))
        {
            throw new ArgumentException("Log directory is required.", nameof(directory));
        }

        _directory = directory;
        Directory.CreateDirectory(_directory);
        _logFilePath = Path.Combine(_directory, fileName);

        _stream = new FileStream(
            _logFilePath,
            FileMode.OpenOrCreate,
            FileAccess.ReadWrite,
            FileShare.Read,
            bufferSize: 4096,
            options: FileOptions.WriteThrough);

        _nextSequenceNumber = Recover().LastOrDefault()?.SequenceNumber + 1 ?? 1;
        _stream.Seek(0, SeekOrigin.End);
    }

    public WalEntry Append(string payload)
    {
        if (payload is null)
        {
            throw new ArgumentNullException(nameof(payload));
        }

        return Append(Encoding.UTF8.GetBytes(payload), DateTime.UtcNow);
    }

    public WalEntry Append(byte[] payload, DateTime timestamp)
    {
        if (payload is null)
        {
            throw new ArgumentNullException(nameof(payload));
        }

        EnsureNotDisposed();

        var sequenceNumber = _nextSequenceNumber++;
        var payloadLength = (ulong)payload.Length;
        Span<byte> header = stackalloc byte[HeaderSize];
        BinaryPrimitives.WriteUInt64LittleEndian(header.Slice(0, sizeof(ulong)), payloadLength);
        BinaryPrimitives.WriteInt64LittleEndian(header.Slice(sizeof(ulong), sizeof(long)), sequenceNumber);
        BinaryPrimitives.WriteInt64LittleEndian(header.Slice(sizeof(ulong) + sizeof(long), sizeof(long)), timestamp.Ticks);

        lock (_stream)
        {
            _stream.Write(header);
            _stream.Write(payload);
            _stream.Flush(flushToDisk: true);
        }

        return new WalEntry(sequenceNumber, timestamp, payload);
    }

    public IEnumerable<WalEntry> Recover()
    {
        EnsureNotDisposed();

        var recovered = new List<WalEntry>();
        lock (_stream)
        {
            _stream.Seek(0, SeekOrigin.Begin);
            while (true)
            {
                var headerBuffer = new byte[HeaderSize];
                var headerBytesRead = ReadExact(headerBuffer);
                if (headerBytesRead == 0)
                {
                    break;
                }

                if (headerBytesRead < HeaderSize)
                {
                    break;
                }

                var payloadLength = BinaryPrimitives.ReadUInt64LittleEndian(headerBuffer.AsSpan(0, sizeof(ulong)));
                var sequenceNumber = BinaryPrimitives.ReadInt64LittleEndian(headerBuffer.AsSpan(sizeof(ulong), sizeof(long)));
                var timestampTicks = BinaryPrimitives.ReadInt64LittleEndian(headerBuffer.AsSpan(sizeof(ulong) + sizeof(long), sizeof(long)));

                if (payloadLength > int.MaxValue)
                {
                    break;
                }

                var payload = new byte[payloadLength];
                var payloadBytesRead = ReadExact(payload);
                if ((ulong)payloadBytesRead < payloadLength)
                {
                    break;
                }

                recovered.Add(new WalEntry(sequenceNumber, new DateTime(timestampTicks, DateTimeKind.Utc), payload));
            }
        }

        return recovered;
    }

    public void Truncate(long beforeSequenceNumber)
    {
        if (beforeSequenceNumber <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(beforeSequenceNumber));
        }

        EnsureNotDisposed();

        var tempPath = Path.Combine(_directory, Guid.NewGuid().ToString("N") + ".tmp");
        var entriesToKeep = Recover().Where(entry => entry.SequenceNumber >= beforeSequenceNumber).ToList();

        using (var writeStream = new FileStream(tempPath, FileMode.Create, FileAccess.Write, FileShare.None, 4096, FileOptions.WriteThrough))
        {
            foreach (var entry in entriesToKeep)
            {
                WriteEntry(writeStream, entry);
            }

            writeStream.Flush(true);
        }

        lock (_stream)
        {
            _stream.Dispose();
            File.Replace(tempPath, _logFilePath, null);
            _stream = new FileStream(
                _logFilePath,
                FileMode.Open,
                FileAccess.ReadWrite,
                FileShare.Read,
                bufferSize: 4096,
                options: FileOptions.WriteThrough);
            _stream.Seek(0, SeekOrigin.End);
        }

        _nextSequenceNumber = entriesToKeep.LastOrDefault()?.SequenceNumber + 1 ?? 1;
    }

    public void Dispose()
    {
        if (!_disposed)
        {
            _stream?.Dispose();
            _disposed = true;
        }
    }

    private void WriteEntry(Stream stream, WalEntry entry)
    {
        var payloadLength = (ulong)entry.Payload.Length;
        Span<byte> header = stackalloc byte[HeaderSize];
        BinaryPrimitives.WriteUInt64LittleEndian(header.Slice(0, sizeof(ulong)), payloadLength);
        BinaryPrimitives.WriteInt64LittleEndian(header.Slice(sizeof(ulong), sizeof(long)), entry.SequenceNumber);
        BinaryPrimitives.WriteInt64LittleEndian(header.Slice(sizeof(ulong) + sizeof(long), sizeof(long)), entry.Timestamp.Ticks);
        stream.Write(header);
        stream.Write(entry.Payload);
    }

    private int ReadExact(byte[] buffer)
    {
        int totalRead = 0;
        while (totalRead < buffer.Length)
        {
            var bytesRead = _stream.Read(buffer, totalRead, buffer.Length - totalRead);
            if (bytesRead == 0)
            {
                break;
            }

            totalRead += bytesRead;
        }

        return totalRead;
    }

    private void EnsureNotDisposed()
    {
        if (_disposed)
        {
            throw new ObjectDisposedException(nameof(WriteAheadLog));
        }
    }
}
