# BloomFilter

A Bloom filter is a data structure designed to tell you, rapidly and memory-efficiently, whether an element is present in a set. The price paid for this efficiency is that a Bloom filter is a probabilistic data structure: it tells us that the element either definitely is not in the set or may be in the set.

The BitArray is the backbone of the Bloom filter. It’s a compact array of bits used to represent a set of n elements (where n is the size of the array). Each bit represents whether an element is in the set.

```C#

public class BloomFilter<T>
{
    private readonly BitArray bitArray;
    private readonly int size;
    private readonly int hashFunctionCount;

    public BloomFilter(int size, int hashFunctionCount)
    {
        this.size = size;
        this.hashFunctionCount = hashFunctionCount;
        bitArray = new BitArray(size);
    }

    public void Add(T item)
    {
        for (int i = 0; i < hashFunctionCount; i++)
        {
            int hash = GetHash(item, i);
            bitArray[hash % size] = true;
        }
    }

    public bool Contains(T item)
    {
        for (int i = 0; i < hashFunctionCount; i++)
        {
            int hash = GetHash(item, i);
            if (!bitArray[hash % size])
            {
                return false;
            }
        }
        return true;
    }

    private int GetHash(T item, int index)
    {
        return item is not null ? item.GetHashCode() * index : 0;
    }
}
```