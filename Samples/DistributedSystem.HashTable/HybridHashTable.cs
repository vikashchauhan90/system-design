namespace DistributedSystem.HashTable;

public class HybridHashTable
{
    private const int LISTPACK_THRESHOLD = 64;

    private ListPackHashTable? _listPack;
    private RehashingHashTable? _hashTable;

    public HybridHashTable()
    {
        _listPack = new ListPackHashTable();
    }

    public void Add(string key, byte[] value)
    {
        if (_hashTable != null)
        {
            _hashTable.Add(key, value);
            return;
        }

        _listPack!.Add(key, value);

        if (_listPack.Count > LISTPACK_THRESHOLD)
        {
            _hashTable = ConvertListPackToHashTable();
            _listPack = null;
        }
    }

    public byte[]? Get(string key)
    {
        if (_hashTable != null)
            return _hashTable.GetKey(key);

        return _listPack!.Get(key);
    }

    public void Remove(string key)
    {
        if (_hashTable != null)
        {
            _hashTable.Remove(key);
            return;
        }

        _listPack!.Remove(key);
    }

    public RehashingHashTable ConvertListPackToHashTable()
    {
        var table = new RehashingHashTable();

        foreach (var entry in _listPack?.Entries() ?? [])
        {
            table.Add(entry.Key, entry.Value);
        }

        return table;
    }
}
