using System.Collections;

namespace DistributedSystem.HashTable;

public class SimpleHashTable : IEnumerable<KeyValuePair<string, byte[]?>>
{
    private const int MINIMUM_CAPACITY = 4;
    private const double LOAD_FACTOR_THRESHOLD = 0.75;
    private int _capacity;
    private int _count;
    private int _filledIndex;
    private Entity?[] _buckets;

    private bool Is75PercentFull => _filledIndex >= _buckets.Length * LOAD_FACTOR_THRESHOLD;
    public bool IsEmpty => _filledIndex == -1;
    public int Capacity => _capacity;
    public int Count => _count;
    public SimpleHashTable(int capacity)
    {
        _capacity = capacity > MINIMUM_CAPACITY ? capacity : MINIMUM_CAPACITY;
        _buckets = new Entity[_capacity];
        _count = 0;
        _filledIndex = -1;
    }
    public SimpleHashTable()
    {
        _capacity = MINIMUM_CAPACITY;
        _buckets = new Entity[_capacity];
        _count = 0;
        _filledIndex = -1;
    }


    public byte[]? GetKey(string key)
    {
        int hash = GetHash(key);
        int bucketIndex = hash % _buckets.Length;
        Entity? entity = _buckets[bucketIndex];
        while (entity != null)
        {
            if (entity.Key.Equals(key))
            {
                return entity.Value;
            }
            entity = entity.next;
        }

        return null;
    }

    public void Add(string key, byte[] value)
    {
        if (Is75PercentFull)
        {
            Resized();
        }

        int hash = GetHash(key);
        int bucketIndex = hash % _buckets.Length;

        // CASE 1: Bucket is empty - just add
        if (_buckets[bucketIndex] == null)
        {
            _buckets[bucketIndex] = new Entity { Key = key, Value = value };
            _filledIndex++;
            _count++;
            return;
        }

        // CASE 2: Bucket has entries - search for key
        Entity? current = _buckets[bucketIndex];
        Entity? previous = null;

        while (current != null)
        {
            if (current.Key.Equals(key))
            {
                // FOUND: Update existing value
                current.Value = value;
                return; // Count doesn't change
            }
            previous = current;
            current = current.next;
        }

        // CASE 3: Key not found - add new entry at end of chain
        Entity newEntity = new Entity
        {
            Key = key,
            Value = value,
            next = null
        };
        previous!.next = newEntity; // Add at end of chain
        _count++;
    }

    public void Remove(string key)
    {
        int hash = GetHash(key);
        int bucketIndex = hash % _buckets.Length;

        Entity? current = _buckets[bucketIndex];
        Entity? previous = null;

        while (current != null)
        {
            if (current.Key.Equals(key))
            {
                // Case 1: removing head of chain
                if (previous == null)
                {
                    _buckets[bucketIndex] = current.next;

                    // bucket becomes empty
                    if (_buckets[bucketIndex] == null)
                    {
                        _filledIndex--;
                    }

                }
                else
                {
                    // skip current node
                    previous.next = current.next;
                }
                _count--;

                return; // done
            }

            previous = current;
            current = current.next;
        }

    }
    public IEnumerator<KeyValuePair<string, byte[]?>> GetEnumerator()
    {
        foreach (var bucket in _buckets)
        {
            Entity? current = bucket;

            while (current != null)
            {
                yield return new KeyValuePair<string, byte[]?>(
                    current.Key,
                    current.Value
                );

                current = current.next;
            }
        }
    }

    private void Resized()
    {
        _capacity = _capacity * 2;
        var newBuckets = new Entity[_capacity];
        foreach (Entity? oldEntity in _buckets)
        {
            Entity? current = oldEntity;
            while (current != null)
            {
                Entity? next = current.next; // save next node
                int hash = GetHash(current.Key);
                int bucketIndex = hash % newBuckets.Length;

                // insert at head of new chain
                current.next = newBuckets[bucketIndex];
                newBuckets[bucketIndex] = current;

                current = next;
            }
        }
        _buckets = newBuckets;
    }
    IEnumerator IEnumerable.GetEnumerator()
    {
        return GetEnumerator();
    }

    private int GetHash(string key)
    {
        return (key.GetHashCode() & 0x7FFFFFFF);
    }


    private class Entity
    {
        public required string Key;
        public byte[]? Value;
        public Entity? next;
    }
}
