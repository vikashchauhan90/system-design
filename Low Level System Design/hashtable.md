# HashTable

```C#

public class HashTable<TKey, TValue>
{
    private const int InitialCapacity = 16;
    private const float LoadFactor = 0.75f;

    private Entry<TKey, TValue>[] entries;
    private int count;

    public HashTable()
    {
        entries = new Entry<TKey, TValue>[InitialCapacity];
        count = 0;
    }

    public void Add(TKey key, TValue value)
    {
        if ((float)count / entries.Length >= LoadFactor)
        {
            Resize();
        }

        int index = GetIndex(key);
        Entry<TKey, TValue> current = entries[index];

        if (current == null)
        {
            entries[index] = new Entry<TKey, TValue>(key, value);
            count++;
        }
        else
        {
            //traverse linked list to check if key is already exists.
            while (current.Next != null)
            {
                if (current.Key.Equals(key))
                {
                    throw new ArgumentException("An element with the same key already exists in the hashtable.");
                }
                current = current.Next;
            }

            current.Next = new Entry<TKey, TValue>(key, value);
            count++;
        }
    }

    public bool ContainsKey(TKey key)
    {
        int index = GetIndex(key);
        Entry<TKey, TValue> current = entries[index];

        while (current != null)
        {
            if (current.Key.Equals(key))
            {
                return true;
            }
            current = current.Next;
        }

        return false;
    }

    public TValue GetValue(TKey key)
    {
        int index = GetIndex(key);
        Entry<TKey, TValue> current = entries[index];

        while (current != null)
        {
            if (current.Key.Equals(key))
            {
                return current.Value;
            }
            current = current.Next;
        }

        throw new KeyNotFoundException("The specified key was not found in the hashtable.");
    }

    public bool Remove(TKey key)
    {
        int index = GetIndex(key);
        Entry<TKey, TValue> current = entries[index];
        Entry<TKey, TValue> previous = null;

        while (current != null)
        {
            if (current.Key.Equals(key))
            {
                if (previous == null)
                {
                    entries[index] = current.Next;
                }
                else
                {
                    previous.Next = current.Next;
                }
                count--;
                return true;
            }

            previous = current;
            current = current.Next;
        }

        return false;
    }

    public int Count => count;

    private void Resize()
    {
        Entry<TKey, TValue>[] oldEntries = entries;
        entries = new Entry<TKey, TValue>[oldEntries.Length * 2];
        count = 0;

        foreach (Entry<TKey, TValue> entry in oldEntries)
        {
            Entry<TKey, TValue> current = entry;
            while (current != null)
            {
                Add(current.Key, current.Value);
                current = current.Next;
            }
        }
    }

    private int GetIndex(TKey key)
    {
        int hashCode = key.GetHashCode();
        int index = hashCode % entries.Length;
        return index < 0 ? index + entries.Length : index;
    }

    private class Entry<TKey, TValue>
    {
        public TKey Key { get; }
        public TValue Value { get; }
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