using System.Collections;

namespace DistributedSystem.HashTable;

public class RehashingHashTable : IEnumerable<KeyValuePair<string, byte[]?>>
{
    private const int MINIMUM_CAPACITY = 4;
    private const double LOAD_FACTOR_THRESHOLD = 0.75;
    private int _capacity;
    private int _count;
    private Entity?[] _buckets;
    private Entity?[]? _oldBuckets;
    private int _rehashIndex;

    private bool IsRehashing => _oldBuckets != null;
    public int Capacity => _capacity;
    public int Count => _count;
    public RehashingHashTable(int capacity)
    {
        _capacity = capacity > MINIMUM_CAPACITY ? capacity : MINIMUM_CAPACITY;
        _buckets = new Entity[_capacity];
        _oldBuckets = null;
        _rehashIndex = 0;
        _count = 0;
    }
    public RehashingHashTable()
    {
        _capacity = MINIMUM_CAPACITY;
        _buckets = new Entity[_capacity];
        _oldBuckets = null;
        _rehashIndex = 0;
        _count = 0;
    }

    public void Add(string key, byte[] value)
    {
        MigrateStep(); // move one bucket

        if (!IsRehashing && Is75PercentFull())
        {
            StartResize();
        }

        InsertIntoTable(_buckets, key, value);
    }

    public byte[]? GetKey(string key)
    {
        MigrateStep();

        byte[]? value = Find(_buckets, key);

        if (value != null)
            return value;

        if (_oldBuckets != null)
            return Find(_oldBuckets, key);

        return null;
    }

    public void Remove(string key)
    {
        MigrateStep();

        if (RemoveFromTable(_buckets, key))
            return;

        if (_oldBuckets != null)
            RemoveFromTable(_oldBuckets, key);
    }

    private byte[]? Find(Entity?[] table, string key)
    {
        int hash = GetHash(key);
        int bucketIndex = hash % table.Length;

        Entity? current = table[bucketIndex];

        while (current != null)
        {
            if (current.Key == key)
                return current.Value;

            current = current.next;
        }

        return null;
    }
    private void InsertIntoTable(Entity?[] table,
                             string key,
                             byte[] value)
    {
        int hash = GetHash(key);
        int bucketIndex = hash % table.Length;

        Entity? current = table[bucketIndex];

        while (current != null)
        {
            if (current.Key == key)
            {
                current.Value = value;
                return;
            }

            current = current.next;
        }

        table[bucketIndex] = new Entity
        {
            Key = key,
            Value = value,
            next = table[bucketIndex]
        };

        _count++;
    }
    private bool RemoveFromTable(Entity?[] table, string key)
    {
        int hash = GetHash(key);
        int bucketIndex = hash % table.Length;

        Entity? current = table[bucketIndex];
        Entity? previous = null;

        while (current != null)
        {
            if (current.Key.Equals(key))
            {
                // removing head of chain
                if (previous == null)
                {
                    table[bucketIndex] = current.next;
                }
                else
                {
                    previous.next = current.next;
                }

                _count--;
                return true;
            }

            previous = current;
            current = current.next;
        }

        return false;
    }
    private void MigrateStep(int bucketsToMove = 1)
    {
        if (_oldBuckets == null)
            return;

        while (bucketsToMove > 0 &&
               _rehashIndex < _oldBuckets.Length)
        {
            Entity? current = _oldBuckets[_rehashIndex];

            while (current != null)
            {
                Entity? next = current.next;

                int hash = GetHash(current.Key);
                int index = hash % _buckets.Length;

                current.next = _buckets[index];
                _buckets[index] = current;

                current = next;
            }

            _oldBuckets[_rehashIndex] = null;
            _rehashIndex++;
            bucketsToMove--;
        }

        // Migration completed
        if (_rehashIndex >= _oldBuckets.Length)
        {
            _oldBuckets = null;
        }
    }
    private void StartResize()
    {
        _oldBuckets = _buckets;
        _capacity *= 2;
        _buckets = new Entity?[_capacity];
        _rehashIndex = 0;
    }

    private bool Is75PercentFull()
    {
        var filledIndexes = _buckets.Count(x => x != null);
        return filledIndexes >= _buckets.Length * LOAD_FACTOR_THRESHOLD;
    }

    public bool IsEmpty()
    {
        return _buckets.All(x => x == null)
            && (_oldBuckets?.All(x => x == null) ?? true);
    }

    private int GetHash(string key)
    {
        return (key.GetHashCode() & 0x7FFFFFFF);
    }
    public IEnumerator<KeyValuePair<string, byte[]?>> GetEnumerator()
    {
        throw new NotImplementedException();
    }

    IEnumerator IEnumerable.GetEnumerator()
    {
        return GetEnumerator();
    }
}
