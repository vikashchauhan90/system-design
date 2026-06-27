using System.Text;

namespace DistributedSystem.LSMTree;

public sealed class MemTable
{
    private readonly SortedDictionary<string, byte[]> _data;
    private readonly long _maxSize;
    private long _currentSize;

    public bool IsFull => _currentSize >= _maxSize;
    public long SizeInBytes => _currentSize;
    public int Count => _data.Count;

    public MemTable(long maxSizeInBytes = 1024 * 1024) // 1MB default
    {
        _data = new SortedDictionary<string, byte[]>(StringComparer.Ordinal);
        _maxSize = maxSizeInBytes;
        _currentSize = 0;
    }

    public void Add(string key, byte[] value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        var normalizedValue = value ?? Array.Empty<byte>();

        if (_data.TryGetValue(key, out var previousValue))
        {
            _currentSize -= EstimateEntrySize(key, previousValue);
        }

        _data[key] = normalizedValue;
        _currentSize += EstimateEntrySize(key, normalizedValue);
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

    private static long EstimateEntrySize(string key, byte[] value)
    {
        return Encoding.UTF8.GetByteCount(key) + value.Length + 24;
    }
}
