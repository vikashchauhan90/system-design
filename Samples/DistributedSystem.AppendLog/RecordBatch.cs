using System.Text;

namespace DistributedSystem.Core.AppendLog;

public sealed class Record
{
    public byte[] Key { get; init; }
    public byte[] Value { get; init; }
    public DateTime Timestamp { get; init; }

    public Record(byte[] key, byte[] value, DateTime timestamp)
    {
        Key = key;
        Value = value;
        Timestamp = timestamp;
    }

    public static Record FromString(string key, string value, DateTime timestamp)
        => new(Encoding.UTF8.GetBytes(key), Encoding.UTF8.GetBytes(value), timestamp);
}

public sealed class RecordBatch
{
    public long BaseOffset { get; init; }
    public DateTime Timestamp { get; init; }
    public IReadOnlyList<Record> Records { get; init; }

    public long LastOffset => BaseOffset + Records.Count - 1;
    public int Count => Records.Count;

    public RecordBatch(long baseOffset, DateTime timestamp, IReadOnlyList<Record> records)
    {
        BaseOffset = baseOffset;
        Timestamp = timestamp;
        Records = records;
    }

    public int GetSizeInBytes()
    {
        var size = sizeof(long) + sizeof(long) + sizeof(int);
        foreach (var record in Records)
        {
            size += sizeof(int) + record.Key.Length;
            size += sizeof(int) + record.Value.Length;
            size += sizeof(long);
        }
        return size;
    }
}
