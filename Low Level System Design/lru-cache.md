# LRU cache

```C#
using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

public class LruCache<TKey, TValue> : IEnumerable<KeyValuePair<TKey, TValue>> 
    where TKey : notnull 
    where TValue : class
{
    private readonly int _capacity;
    private readonly Dictionary<TKey, LinkedListNode<CacheEntry>> _cache;
    private readonly LinkedList<CacheEntry> _accessList;
    private readonly ReaderWriterLockSlim _lock;
    private readonly TimeSpan _defaultTtl;
    private readonly IEvictionPolicy _evictionPolicy;
    private int _hitCount;
    private int _missCount;
    private long _totalAccessCount;

    // Statistics
    public int HitCount => Interlocked.CompareExchange(ref _hitCount, 0, 0);
    public int MissCount => Interlocked.CompareExchange(ref _missCount, 0, 0);
    public double HitRate => TotalAccessCount > 0 ? (double)HitCount / TotalAccessCount : 0;
    public long TotalAccessCount => Interlocked.Read(ref _totalAccessCount);
    public int Count => _cache.Count;
    public int Capacity => _capacity;
    public bool IsEmpty => _cache.Count == 0;
    public bool IsFull => _cache.Count >= _capacity;

    // Cache entry structure
    private class CacheEntry
    {
        public TKey Key { get; set; }
        public TValue Value { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime? ExpiresAt { get; set; }
        public long AccessCount { get; set; }
        public DateTime LastAccessedAt { get; set; }
        public long SizeBytes { get; set; }

        public CacheEntry(TKey key, TValue value)
        {
            Key = key;
            Value = value;
            CreatedAt = DateTime.UtcNow;
            LastAccessedAt = DateTime.UtcNow;
            AccessCount = 1;
        }

        public bool IsExpired => ExpiresAt.HasValue && ExpiresAt.Value <= DateTime.UtcNow;
    }

    // Eviction policy interface
    public interface IEvictionPolicy
    {
        LinkedListNode<CacheEntry> SelectVictim(LinkedList<CacheEntry> accessList);
    }

    // LRU eviction policy (default)
    public class LruEvictionPolicy : IEvictionPolicy
    {
        public LinkedListNode<CacheEntry> SelectVictim(LinkedList<CacheEntry> accessList)
        {
            return accessList.First;
        }
    }

    // LFU (Least Frequently Used) eviction policy
    public class LfuEvictionPolicy : IEvictionPolicy
    {
        public LinkedListNode<CacheEntry> SelectVictim(LinkedList<CacheEntry> accessList)
        {
            // Find node with minimum access count
            var currentNode = accessList.First;
            var victimNode = currentNode;
            long minAccessCount = long.MaxValue;

            while (currentNode != null)
            {
                if (currentNode.Value.AccessCount < minAccessCount)
                {
                    minAccessCount = currentNode.Value.AccessCount;
                    victimNode = currentNode;
                }
                currentNode = currentNode.Next;
            }

            return victimNode;
        }
    }

    // MRU (Most Recently Used) eviction policy
    public class MruEvictionPolicy : IEvictionPolicy
    {
        public LinkedListNode<CacheEntry> SelectVictim(LinkedList<CacheEntry> accessList)
        {
            return accessList.Last;
        }
    }

    public LruCache(int capacity, TimeSpan? defaultTtl = null, IEvictionPolicy evictionPolicy = null)
    {
        if (capacity <= 0)
            throw new ArgumentException("Capacity must be greater than 0", nameof(capacity));

        _capacity = capacity;
        _cache = new Dictionary<TKey, LinkedListNode<CacheEntry>>(capacity);
        _accessList = new LinkedList<CacheEntry>();
        _lock = new ReaderWriterLockSlim(LockRecursionPolicy.NoRecursion);
        _defaultTtl = defaultTtl ?? TimeSpan.MaxValue;
        _evictionPolicy = evictionPolicy ?? new LruEvictionPolicy();
    }

    /// <summary>
    /// Adds or updates a value in the cache
    /// </summary>
    public void Add(TKey key, TValue value, TimeSpan? ttl = null)
    {
        if (key == null)
            throw new ArgumentNullException(nameof(key));

        _lock.EnterWriteLock();
        try
        {
            // Check if key already exists
            if (_cache.TryGetValue(key, out var existingNode))
            {
                // Update existing entry
                existingNode.Value.Value = value;
                existingNode.Value.LastAccessedAt = DateTime.UtcNow;
                existingNode.Value.AccessCount++;
                
                // Update TTL if specified
                if (ttl.HasValue && ttl.Value != TimeSpan.MaxValue)
                {
                    existingNode.Value.ExpiresAt = DateTime.UtcNow.Add(ttl.Value);
                }
                
                // Move to end (most recently used)
                _accessList.Remove(existingNode);
                _accessList.AddLast(existingNode);
                return;
            }

            // Check capacity and evict if needed
            if (_cache.Count >= _capacity)
            {
                EvictOne();
            }

            // Create new entry
            var entry = new CacheEntry(key, value);
            if (ttl.HasValue && ttl.Value != TimeSpan.MaxValue)
            {
                entry.ExpiresAt = DateTime.UtcNow.Add(ttl.Value);
            }
            else if (_defaultTtl != TimeSpan.MaxValue)
            {
                entry.ExpiresAt = DateTime.UtcNow.Add(_defaultTtl);
            }

            // Add to access list and dictionary
            var node = new LinkedListNode<CacheEntry>(entry);
            _accessList.AddLast(node);
            _cache[key] = node;
        }
        finally
        {
            _lock.ExitWriteLock();
        }
    }

    /// <summary>
    /// Gets a value from the cache
    /// </summary>
    public TValue Get(TKey key)
    {
        if (key == null)
            throw new ArgumentNullException(nameof(key));

        Interlocked.Increment(ref _totalAccessCount);
        
        _lock.EnterUpgradeableReadLock();
        try
        {
            if (!_cache.TryGetValue(key, out var node))
            {
                Interlocked.Increment(ref _missCount);
                return null;
            }

            var entry = node.Value;
            
            // Check if entry is expired
            if (entry.IsExpired)
            {
                _lock.EnterWriteLock();
                try
                {
                    // Double-check expiration after acquiring write lock
                    if (entry.IsExpired)
                    {
                        RemoveNode(node);
                        Interlocked.Increment(ref _missCount);
                        return null;
                    }
                }
                finally
                {
                    _lock.ExitWriteLock();
                }
            }

            // Update access statistics
            entry.LastAccessedAt = DateTime.UtcNow;
            entry.AccessCount++;
            
            // Move to end (most recently used)
            _lock.EnterWriteLock();
            try
            {
                _accessList.Remove(node);
                _accessList.AddLast(node);
            }
            finally
            {
                _lock.ExitWriteLock();
            }
            
            Interlocked.Increment(ref _hitCount);
            return entry.Value;
        }
        finally
        {
            _lock.ExitUpgradeableReadLock();
        }
    }

    /// <summary>
    /// Gets a value with async computation if not present
    /// </summary>
    public async Task<TValue> GetOrAddAsync(TKey key, Func<TKey, Task<TValue>> valueFactory, TimeSpan? ttl = null)
    {
        var value = Get(key);
        if (value != null)
            return value;

        // Compute value asynchronously
        var newValue = await valueFactory(key);
        Add(key, newValue, ttl);
        return newValue;
    }

    /// <summary>
    /// Gets a value or adds it if not present
    /// </summary>
    public TValue GetOrAdd(TKey key, Func<TKey, TValue> valueFactory, TimeSpan? ttl = null)
    {
        var value = Get(key);
        if (value != null)
            return value;

        // Add with factory
        var newValue = valueFactory(key);
        Add(key, newValue, ttl);
        return newValue;
    }

    /// <summary>
    /// Tries to get a value from the cache
    /// </summary>
    public bool TryGetValue(TKey key, out TValue value)
    {
        value = Get(key);
        return value != null;
    }

    /// <summary>
    /// Checks if the cache contains the key
    /// </summary>
    public bool Contains(TKey key)
    {
        _lock.EnterReadLock();
        try
        {
            if (_cache.TryGetValue(key, out var node))
            {
                var entry = node.Value;
                if (!entry.IsExpired)
                    return true;
                
                // Remove expired entry
                _lock.EnterWriteLock();
                try
                {
                    if (entry.IsExpired)
                        RemoveNode(node);
                }
                finally
                {
                    _lock.ExitWriteLock();
                }
            }
            return false;
        }
        finally
        {
            _lock.ExitReadLock();
        }
    }

    /// <summary>
    /// Removes a key from the cache
    /// </summary>
    public bool Remove(TKey key)
    {
        if (key == null)
            throw new ArgumentNullException(nameof(key));

        _lock.EnterWriteLock();
        try
        {
            if (_cache.TryGetValue(key, out var node))
            {
                RemoveNode(node);
                return true;
            }
            return false;
        }
        finally
        {
            _lock.ExitWriteLock();
        }
    }

    /// <summary>
    /// Clears all items from the cache
    /// </summary>
    public void Clear()
    {
        _lock.EnterWriteLock();
        try
        {
            _cache.Clear();
            _accessList.Clear();
            _hitCount = 0;
            _missCount = 0;
            _totalAccessCount = 0;
        }
        finally
        {
            _lock.ExitWriteLock();
        }
    }

    /// <summary>
    /// Removes expired entries from the cache
    /// </summary>
    public int CleanupExpired()
    {
        var removed = 0;
        _lock.EnterWriteLock();
        try
        {
            var currentNode = _accessList.First;
            while (currentNode != null)
            {
                var nextNode = currentNode.Next;
                if (currentNode.Value.IsExpired)
                {
                    RemoveNode(currentNode);
                    removed++;
                }
                currentNode = nextNode;
            }
        }
        finally
        {
            _lock.ExitWriteLock();
        }
        return removed;
    }

    /// <summary>
    /// Evicts a single item based on eviction policy
    /// </summary>
    private void EvictOne()
    {
        var victimNode = _evictionPolicy.SelectVictim(_accessList);
        if (victimNode != null)
        {
            RemoveNode(victimNode);
        }
    }

    /// <summary>
    /// Removes a node from the cache
    /// </summary>
    private void RemoveNode(LinkedListNode<CacheEntry> node)
    {
        var key = node.Value.Key;
        _cache.Remove(key);
        _accessList.Remove(node);
    }

    /// <summary>
    /// Gets cache statistics
    /// </summary>
    public CacheStatistics GetStatistics()
    {
        _lock.EnterReadLock();
        try
        {
            return new CacheStatistics
            {
                HitCount = HitCount,
                MissCount = MissCount,
                HitRate = HitRate,
                TotalAccessCount = TotalAccessCount,
                CurrentSize = _cache.Count,
                Capacity = _capacity,
                UtilizationPercentage = (double)_cache.Count / _capacity * 100,
                OldestEntryAge = _accessList.First != null ? DateTime.UtcNow - _accessList.First.Value.CreatedAt : TimeSpan.Zero,
                NewestEntryAge = _accessList.Last != null ? DateTime.UtcNow - _accessList.Last.Value.CreatedAt : TimeSpan.Zero,
                AverageAccessCount = _cache.Count > 0 ? _cache.Values.Average(v => v.Value.AccessCount) : 0
            };
        }
        finally
        {
            _lock.ExitReadLock();
        }
    }

    /// <summary>
    /// Gets all keys in the cache
    /// </summary>
    public IEnumerable<TKey> GetKeys()
    {
        _lock.EnterReadLock();
        try
        {
            return _cache.Keys.ToList();
        }
        finally
        {
            _lock.ExitReadLock();
        }
    }

    /// <summary>
    /// Gets all values in the cache
    /// </summary>
    public IEnumerable<TValue> GetValues()
    {
        _lock.EnterReadLock();
        try
        {
            return _cache.Values.Select(v => v.Value.Value).Where(v => v != null).ToList();
        }
        finally
        {
            _lock.ExitReadLock();
        }
    }

    /// <summary>
    /// Resizes the cache to a new capacity
    /// </summary>
    public void Resize(int newCapacity)
    {
        if (newCapacity <= 0)
            throw new ArgumentException("Capacity must be greater than 0", nameof(newCapacity));

        _lock.EnterWriteLock();
        try
        {
            while (_cache.Count > newCapacity)
            {
                EvictOne();
            }
            // Note: capacity field is readonly, so we can't change it
            // This method is for illustration - in practice you'd need to create a new cache
        }
        finally
        {
            _lock.ExitWriteLock();
        }
    }

    // IEnumerable implementation
    public IEnumerator<KeyValuePair<TKey, TValue>> GetEnumerator()
    {
        _lock.EnterReadLock();
        try
        {
            foreach (var kvp in _cache)
            {
                var entry = kvp.Value.Value;
                if (!entry.IsExpired)
                {
                    yield return new KeyValuePair<TKey, TValue>(kvp.Key, entry.Value);
                }
            }
        }
        finally
        {
            _lock.ExitReadLock();
        }
    }

    IEnumerator IEnumerable.GetEnumerator()
    {
        return GetEnumerator();
    }

    public void Dispose()
    {
        _lock?.Dispose();
    }
}

// Statistics class
public class CacheStatistics
{
    public int HitCount { get; set; }
    public int MissCount { get; set; }
    public double HitRate { get; set; }
    public long TotalAccessCount { get; set; }
    public int CurrentSize { get; set; }
    public int Capacity { get; set; }
    public double UtilizationPercentage { get; set; }
    public TimeSpan OldestEntryAge { get; set; }
    public TimeSpan NewestEntryAge { get; set; }
    public double AverageAccessCount { get; set; }

    public override string ToString()
    {
        return $"Cache Stats: Size={CurrentSize}/{Capacity} ({UtilizationPercentage:F1}%), " +
               $"Hit Rate={HitRate:P1} ({HitCount}/{TotalAccessCount}), " +
               $"Avg Access={AverageAccessCount:F1}";
    }
}

// Extension methods for easier usage
public static class LruCacheExtensions
{
    public static TValue GetOrDefault<TKey, TValue>(this LruCache<TKey, TValue> cache, TKey key, TValue defaultValue)
        where TKey : notnull
        where TValue : class
    {
        var value = cache.Get(key);
        return value ?? defaultValue;
    }

    public static bool TryGet<TKey, TValue>(this LruCache<TKey, TValue> cache, TKey key, out TValue value)
        where TKey : notnull
        where TValue : class
    {
        value = cache.Get(key);
        return value != null;
    }

    public static void AddRange<TKey, TValue>(this LruCache<TKey, TValue> cache, IEnumerable<KeyValuePair<TKey, TValue>> items)
        where TKey : notnull
        where TValue : class
    {
        foreach (var item in items)
        {
            cache.Add(item.Key, item.Value);
        }
    }
}
```