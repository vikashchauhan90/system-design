# HashTable

```C#

using System;
using System.Collections;
using System.Collections.Generic;

public class HashTable<TKey, TValue> : IEnumerable<KeyValuePair<TKey, TValue>>
{
    private const int InitialCapacity = 16;
    private const float LoadFactor = 0.75f;
    private const float ShrinkFactor = 0.3f;
    private const int MinCapacity = 4;

    private Entry<TKey, TValue>[] _buckets;
    private int _count;
    private int _version; // For enumeration

    public HashTable() : this(InitialCapacity)
    {
    }

    public HashTable(int capacity)
    {
        if (capacity < 0)
            throw new ArgumentOutOfRangeException(nameof(capacity));
        
        capacity = GetPrime(capacity);
        _buckets = new Entry<TKey, TValue>[capacity];
        _count = 0;
        _version = 0;
    }

    public int Count => _count;
    
    public TValue this[TKey key]
    {
        get => GetValue(key);
        set => AddOrUpdate(key, value);
    }

    public void Add(TKey key, TValue value)
    {
        if (key == null)
            throw new ArgumentNullException(nameof(key));
        
        // Check if resize needed
        if (_count >= _buckets.Length * LoadFactor)
        {
            Resize(_buckets.Length * 2);
        }

        int index = GetIndex(key);
        
        // Check if key already exists in the bucket
        Entry<TKey, TValue> current = _buckets[index];
        while (current != null)
        {
            if (current.Key.Equals(key))
            {
                throw new ArgumentException($"An element with the same key '{key}' already exists");
            }
            current = current.Next;
        }
        
        // Add new entry at the beginning of the chain (more efficient)
        Entry<TKey, TValue> newEntry = new Entry<TKey, TValue>(key, value)
        {
            Next = _buckets[index]
        };
        _buckets[index] = newEntry;
        _count++;
        _version++;
    }

    public bool AddOrUpdate(TKey key, TValue value)
    {
        if (key == null)
            throw new ArgumentNullException(nameof(key));
        
        // Check if resize needed
        if (_count >= _buckets.Length * LoadFactor)
        {
            Resize(_buckets.Length * 2);
        }

        int index = GetIndex(key);
        
        // Check if key exists and update
        Entry<TKey, TValue> current = _buckets[index];
        while (current != null)
        {
            if (current.Key.Equals(key))
            {
                current.Value = value;
                _version++;
                return false; // Updated existing
            }
            current = current.Next;
        }
        
        // Add new entry
        Entry<TKey, TValue> newEntry = new Entry<TKey, TValue>(key, value)
        {
            Next = _buckets[index]
        };
        _buckets[index] = newEntry;
        _count++;
        _version++;
        return true; // Added new
    }

    public bool TryGetValue(TKey key, out TValue value)
    {
        if (key == null)
            throw new ArgumentNullException(nameof(key));
        
        int index = GetIndex(key);
        Entry<TKey, TValue> current = _buckets[index];
        
        while (current != null)
        {
            if (current.Key.Equals(key))
            {
                value = current.Value;
                return true;
            }
            current = current.Next;
        }
        
        value = default(TValue);
        return false;
    }

    public TValue GetValue(TKey key)
    {
        if (TryGetValue(key, out TValue value))
            return value;
        
        throw new KeyNotFoundException($"The key '{key}' was not found in the hash table.");
    }

    public bool ContainsKey(TKey key)
    {
        if (key == null)
            throw new ArgumentNullException(nameof(key));
        
        int index = GetIndex(key);
        Entry<TKey, TValue> current = _buckets[index];
        
        while (current != null)
        {
            if (current.Key.Equals(key))
                return true;
            current = current.Next;
        }
        
        return false;
    }

    public bool Remove(TKey key)
    {
        if (key == null)
            throw new ArgumentNullException(nameof(key));
        
        int index = GetIndex(key);
        Entry<TKey, TValue> current = _buckets[index];
        Entry<TKey, TValue> previous = null;
        
        while (current != null)
        {
            if (current.Key.Equals(key))
            {
                if (previous == null)
                {
                    // Remove first node
                    _buckets[index] = current.Next;
                }
                else
                {
                    // Remove middle or last node
                    previous.Next = current.Next;
                }
                
                _count--;
                _version++;
                
                // Shrink if necessary
                if (_count > MinCapacity && _count <= _buckets.Length * ShrinkFactor)
                {
                    Resize(Math.Max(MinCapacity, _buckets.Length / 2));
                }
                
                return true;
            }
            
            previous = current;
            current = current.Next;
        }
        
        return false;
    }

    public void Clear()
    {
        if (_count > 0)
        {
            Array.Clear(_buckets, 0, _buckets.Length);
            _count = 0;
            _version++;
        }
    }

    public bool ContainsValue(TValue value)
    {
        EqualityComparer<TValue> comparer = EqualityComparer<TValue>.Default;
        
        foreach (var entry in _buckets)
        {
            Entry<TKey, TValue> current = entry;
            while (current != null)
            {
                if (comparer.Equals(current.Value, value))
                    return true;
                current = current.Next;
            }
        }
        
        return false;
    }

    private void Resize(int newSize)
    {
        newSize = GetPrime(newSize);
        Entry<TKey, TValue>[] oldBuckets = _buckets;
        _buckets = new Entry<TKey, TValue>[newSize];
        _count = 0;
        
        // Rehash all entries
        for (int i = 0; i < oldBuckets.Length; i++)
        {
            Entry<TKey, TValue> current = oldBuckets[i];
            while (current != null)
            {
                Entry<TKey, TValue> next = current.Next;
                
                // Rehash to new bucket
                int newIndex = GetIndex(current.Key);
                current.Next = _buckets[newIndex];
                _buckets[newIndex] = current;
                _count++;
                
                current = next;
            }
        }
        
        _version++;
    }

    private int GetIndex(TKey key)
    {
        uint hashCode = (uint)key.GetHashCode();
        return (int)(hashCode % (uint)_buckets.Length);
    }

    // Prime number table for better distribution
    private static readonly int[] Primes = 
    {
        3, 7, 11, 17, 23, 29, 37, 47, 59, 71, 89, 107, 131, 163, 197, 239, 293, 353, 431, 521, 631, 761, 919,
        1103, 1327, 1597, 1931, 2333, 2801, 3371, 4049, 4861, 5839, 7013, 8419, 10103, 12143, 14591,
        17519, 21023, 25229, 30293, 36353, 43627, 52361, 62851, 75431, 90523, 108631, 130363, 156437,
        187751, 225307, 270371, 324449, 389357, 467237, 560689, 672827, 807403, 968897, 1162687, 1395263,
        1674319, 2009191, 2411033, 2893249, 3471899, 4166287, 4999559, 5999471, 7199369
    };

    private static int GetPrime(int min)
    {
        if (min < 0)
            throw new ArgumentException("Capacity must be non-negative");
        
        foreach (int prime in Primes)
        {
            if (prime >= min)
                return prime;
        }
        
        // Fallback to manual prime calculation for large numbers
        for (int i = min | 1; i < int.MaxValue; i += 2)
        {
            if (IsPrime(i))
                return i;
        }
        
        return min;
    }

    private static bool IsPrime(int candidate)
    {
        if ((candidate & 1) == 0)
            return candidate == 2;
        
        int limit = (int)Math.Sqrt(candidate);
        for (int divisor = 3; divisor <= limit; divisor += 2)
        {
            if (candidate % divisor == 0)
                return false;
        }
        
        return true;
    }

    // IEnumerable implementation
    public IEnumerator<KeyValuePair<TKey, TValue>> GetEnumerator()
    {
        int version = _version;
        
        for (int i = 0; i < _buckets.Length; i++)
        {
            Entry<TKey, TValue> current = _buckets[i];
            while (current != null)
            {
                if (version != _version)
                    throw new InvalidOperationException("Collection was modified");
                
                yield return new KeyValuePair<TKey, TValue>(current.Key, current.Value);
                current = current.Next;
            }
        }
    }

    IEnumerator IEnumerable.GetEnumerator()
    {
        return GetEnumerator();
    }

    // Get all keys
    public IEnumerable<TKey> Keys
    {
        get
        {
            foreach (var entry in this)
            {
                yield return entry.Key;
            }
        }
    }

    // Get all values
    public IEnumerable<TValue> Values
    {
        get
        {
            foreach (var entry in this)
            {
                yield return entry.Value;
            }
        }
    }

    private class Entry<TKey, TValue>
    {
        public TKey Key { get; }
        public TValue Value { get; set; }
        public Entry<TKey, TValue> Next { get; set; }

        public Entry(TKey key, TValue value)
        {
            Key = key;
            Value = value;
            Next = null;
        }
    }
}

```

The reason we use a linked list array (an array of linked lists) in a hashtable is to handle collisions. A collision occurs when two different keys produce the same hash value. By using a linked list at each index of the array, we can store multiple keys (and their associated values) that hash to the same index.