using System.Buffers.Binary;

namespace DistributedSystem.AppendLog;

public sealed class OffsetIndex
{
    private readonly int _sparseInterval;
    private readonly List<OffsetIndexEntry> _entries = new();

    public OffsetIndex(int sparseInterval = 100)
    {
        _sparseInterval = sparseInterval;
    }

    public IReadOnlyList<OffsetIndexEntry> Entries => _entries;

    public void Append(long offset, long position)
    {
        if (_entries.Count == 0 || offset - _entries[^1].Offset >= _sparseInterval)
        {
            _entries.Add(new OffsetIndexEntry(offset, position));
        }
    }

    public OffsetIndexEntry? Lookup(long targetOffset)
    {
        for (var i = _entries.Count - 1; i >= 0; i--)
        {
            if (_entries[i].Offset <= targetOffset)
            {
                return _entries[i];
            }
        }

        return null;
    }

    public void TruncateTo(long offset)
    {
        _entries.RemoveAll(entry => entry.Offset > offset);
    }

    public long LastOffset => _entries.Count == 0 ? -1 : _entries[^1].Offset;

    public void Save(string path)
    {
        using var stream = File.Open(path, FileMode.Create, FileAccess.Write, FileShare.None);
        foreach (var entry in _entries)
        {
            Span<byte> buffer = stackalloc byte[sizeof(long) * 2];
            BinaryPrimitives.WriteInt64LittleEndian(buffer[..8], entry.Offset);
            BinaryPrimitives.WriteInt64LittleEndian(buffer[8..], entry.Position);
            stream.Write(buffer);
        }
    }

    public static OffsetIndex Load(string path, int sparseInterval = 100)
    {
        var index = new OffsetIndex(sparseInterval);
        if (!File.Exists(path))
        {
            return index;
        }

        using var stream = File.OpenRead(path);
        var buffer = new byte[sizeof(long) * 2];
        while (stream.Read(buffer) == buffer.Length)
        {
            var offset = BinaryPrimitives.ReadInt64LittleEndian(buffer[..8]);
            var position = BinaryPrimitives.ReadInt64LittleEndian(buffer[8..]);
            index._entries.Add(new OffsetIndexEntry(offset, position));
        }

        return index;
    }
}
