namespace DistributedSystem.LSMTree;

public class MemTable
{
    private readonly SortedDictionary<string, byte[]> _data;
    private readonly long _maxSize;
    private long _currentSize;

    public bool IsFull => _currentSize >= _maxSize;
    public long SizeInBytes => _currentSize;
    public int Count => _data.Count;
    public MemTable(long maxSizeInBytes = 1024 * 1024) // 1MB default
    {
        _data = new SortedDictionary<string, byte[]>();
        _maxSize = maxSizeInBytes;
        _currentSize = 0;
    }

    public void Add(string key, byte[] value)
    {
        // If key exists, subtract its old size
        if (_data.TryGetValue(key, out var oldValue))
        {
            _currentSize -= oldValue.Length;
        }

        _data[key] = value;
        _currentSize += value.Length + key.Length * 2; // Approximate overhead
    }

    public byte[]? Get(string key)
    {
        return _data.TryGetValue(key, out var value) ? value : null;
    }

    public IEnumerable<KeyValuePair<string, byte[]>> GetAll()
    {
        foreach (var kvp in _data)
        {
            yield return kvp;
        }
    }

    public void Clear()
    {
        _data.Clear();
        _currentSize = 0;
    }
    
}
