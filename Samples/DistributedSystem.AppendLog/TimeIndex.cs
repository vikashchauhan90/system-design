using System.Buffers.Binary;

namespace DistributedSystem.AppendLog;

public sealed class TimeIndex
{
    private readonly int _maxEntries;
    private readonly List<TimeIndexEntry> _entries = new();
    private readonly TimeSpan _sparseInterval;

    public TimeIndex(int maxEntries = 1024, TimeSpan? sparseInterval = null)
    {
        _maxEntries = maxEntries;
        _sparseInterval = sparseInterval ?? TimeSpan.FromSeconds(1);
    }

    public IReadOnlyList<TimeIndexEntry> Entries => _entries;

    public bool IsFull() => _entries.Count >= _maxEntries;

    public void MaybeAppend(long offset, DateTime timestamp)
    {
        if (_entries.Count == 0 || timestamp - _entries[^1].Timestamp >= _sparseInterval)
        {
            _entries.Add(new TimeIndexEntry(timestamp, offset));
        }
    }

    public TimeIndexEntry? Lookup(DateTime targetTimestamp)
    {
        for (var i = 0; i < _entries.Count; i++)
        {
            if (_entries[i].Timestamp >= targetTimestamp)
            {
                return _entries[i];
            }
        }

        return _entries.Count > 0 ? _entries[^1] : null;
    }

    public void TruncateTo(long offset)
    {
        _entries.RemoveAll(entry => entry.Offset > offset);
    }

    public void Save(string path)
    {
        using var stream = File.Open(path, FileMode.Create, FileAccess.Write, FileShare.None);
        foreach (var entry in _entries)
        {
            Span<byte> buffer = stackalloc byte[sizeof(long) * 2];
            BinaryPrimitives.WriteInt64LittleEndian(buffer[..8], entry.Timestamp.Ticks);
            BinaryPrimitives.WriteInt64LittleEndian(buffer[8..], entry.Offset);
            stream.Write(buffer);
        }
    }

    public static TimeIndex Load(string path, int maxEntries = 1024, TimeSpan? sparseInterval = null)
    {
        var index = new TimeIndex(maxEntries, sparseInterval);
        if (!File.Exists(path))
        {
            return index;
        }

        using var stream = File.OpenRead(path);
        var buffer = new byte[sizeof(long) * 2];
        while (stream.Read(buffer) == buffer.Length)
        {
            var ticks = BinaryPrimitives.ReadInt64LittleEndian(buffer[..8]);
            var offset = BinaryPrimitives.ReadInt64LittleEndian(buffer[8..]);
            index._entries.Add(new TimeIndexEntry(new DateTime(ticks, DateTimeKind.Utc), offset));
        }

        return index;
    }
}
