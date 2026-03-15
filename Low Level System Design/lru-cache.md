# LRU cache

```C#
public class LruCache<TKey, TValue> where TKey : notnull where TValue : class
{
    private readonly int capacity;
    private readonly Dictionary<TKey, TValue> cache;
    private readonly LinkedList<TKey> queue;

    public LruCache(int capacity)
    {
        this.capacity = capacity;
        this.cache = new Dictionary<TKey, TValue>(capacity);
        this.queue = new LinkedList<TKey>();
    }

    public void Add(TKey key, TValue value)
    {
        if (cache.TryGetValue(key, out _))
        {
            queue.Remove(key);
            queue.AddLast(key);
            cache[key] = value;
            return;
        }

        if (this.cache.Count >= capacity)
        {
            RemoveFirst();
        }

        cache.Add(key, value);
        this.queue.AddLast(key);

    }

    public TValue GetValue(TKey key)
    {
        if (cache.TryGetValue(key, out TValue node))
        {
            queue.Remove(key);
            queue.AddLast(key);
            return node;
        }
        return default;
    }

    private void RemoveFirst()
    {
        var node = this.queue.First;
        this.queue.RemoveFirst();
        this.cache.Remove(node!.Value!);
    }
}
```