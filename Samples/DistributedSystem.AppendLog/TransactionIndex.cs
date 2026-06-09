using System.Buffers.Binary;

namespace DistributedSystem.Core.AppendLog;

public sealed class TransactionIndex
{
    private readonly List<TransactionIndexEntry> _entries = new();

    public IReadOnlyList<TransactionIndexEntry> Entries => _entries;

    public void Append(long transactionId, long startOffset, TransactionState state)
    {
        _entries.Add(new TransactionIndexEntry(transactionId, startOffset, state));
    }

    public TransactionIndexEntry? Lookup(long transactionId)
    {
        return _entries.LastOrDefault(x => x.TransactionId == transactionId);
    }

    public void TruncateTo(long offset)
    {
        _entries.RemoveAll(entry => entry.StartOffset > offset);
    }

    public void Save(string path)
    {
        using var stream = File.Open(path, FileMode.Create, FileAccess.Write, FileShare.None);
        foreach (var entry in _entries)
        {
            Span<byte> buffer = stackalloc byte[sizeof(long) * 2 + sizeof(int)];
            BinaryPrimitives.WriteInt64LittleEndian(buffer[..8], entry.TransactionId);
            BinaryPrimitives.WriteInt64LittleEndian(buffer[8..16], entry.StartOffset);
            BinaryPrimitives.WriteInt32LittleEndian(buffer[16..], (int)entry.State);
            stream.Write(buffer);
        }
    }

    public static TransactionIndex Load(string path)
    {
        var index = new TransactionIndex();
        if (!File.Exists(path))
        {
            return index;
        }

        using var stream = File.OpenRead(path);
        var buffer = new byte[sizeof(long) * 2 + sizeof(int)];
        while (stream.Read(buffer) == buffer.Length)
        {
            var transactionId = BinaryPrimitives.ReadInt64LittleEndian(buffer[..8]);
            var startOffset = BinaryPrimitives.ReadInt64LittleEndian(buffer[8..16]);
            var state = (TransactionState)BinaryPrimitives.ReadInt32LittleEndian(buffer[16..]);
            index._entries.Add(new TransactionIndexEntry(transactionId, startOffset, state));
        }

        return index;
    }
}
