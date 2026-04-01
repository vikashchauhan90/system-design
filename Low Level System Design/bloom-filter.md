# BloomFilter

A Bloom filter is a data structure designed to tell you, rapidly and memory-efficiently, whether an element is present in a set. The price paid for this efficiency is that a Bloom filter is a probabilistic data structure: it tells us that the element either definitely is not in the set or may be in the set.

The BitArray is the backbone of the Bloom filter. It’s a compact array of bits used to represent a set of n elements (where n is the size of the array). Each bit represents whether an element is in the set.

```C#

using System;
using System.Collections;
using System.Security.Cryptography;
using System.Text;

public class BloomFilter<T>
{
    private readonly BitArray _bitArray;
    private readonly int _size;
    private readonly int _hashFunctionCount;
    private readonly HashAlgorithm _hashAlgorithm;
    private readonly int _seed1;
    private readonly int _seed2;

    /// <summary>
    /// Creates a Bloom Filter with optimal size based on expected items and false positive rate
    /// </summary>
    /// <param name="expectedItems">Expected number of items to be added</param>
    /// <param name="falsePositiveRate">Desired false positive rate (e.g., 0.01 for 1%)</param>
    public BloomFilter(int expectedItems, double falsePositiveRate = 0.01)
    {
        // Calculate optimal size: m = -n * ln(p) / (ln(2)^2)
        _size = (int)Math.Ceiling(-expectedItems * Math.Log(falsePositiveRate) / Math.Pow(Math.Log(2), 2));
        
        // Calculate optimal number of hash functions: k = (m/n) * ln(2)
        _hashFunctionCount = (int)Math.Ceiling((_size / (double)expectedItems) * Math.Log(2));
        
        _bitArray = new BitArray(_size);
        _hashAlgorithm = SHA256.Create();
        _seed1 = new Random().Next(1, int.MaxValue);
        _seed2 = new Random().Next(1, int.MaxValue);
        
        Console.WriteLine($"Bloom Filter created with size: {_size}, hash functions: {_hashFunctionCount}");
    }

    /// <summary>
    /// Creates a Bloom Filter with manual size and hash function count
    /// </summary>
    public BloomFilter(int size, int hashFunctionCount)
    {
        if (size <= 0)
            throw new ArgumentException("Size must be positive", nameof(size));
        if (hashFunctionCount <= 0)
            throw new ArgumentException("Hash function count must be positive", nameof(hashFunctionCount));
        
        _size = size;
        _hashFunctionCount = hashFunctionCount;
        _bitArray = new BitArray(size);
        _hashAlgorithm = SHA256.Create();
        _seed1 = new Random().Next(1, int.MaxValue);
        _seed2 = new Random().Next(1, int.MaxValue);
    }

    public void Add(T item)
    {
        if (item == null)
            throw new ArgumentNullException(nameof(item));
        
        var (hash1, hash2) = GetDoubleHash(item);
        
        for (int i = 0; i < _hashFunctionCount; i++)
        {
            // Double hashing technique: h(i) = (hash1 + i * hash2) % size
            int hash = (hash1 + i * hash2) % _size;
            if (hash < 0) hash += _size;
            
            _bitArray[hash] = true;
        }
    }

    public bool Contains(T item)
    {
        if (item == null)
            throw new ArgumentNullException(nameof(item));
        
        var (hash1, hash2) = GetDoubleHash(item);
        
        for (int i = 0; i < _hashFunctionCount; i++)
        {
            // Double hashing technique: h(i) = (hash1 + i * hash2) % size
            int hash = (hash1 + i * hash2) % _size;
            if (hash < 0) hash += _size;
            
            if (!_bitArray[hash])
                return false;
        }
        
        return true;
    }

    /// <summary>
    /// Gets two independent hash values using multiple techniques
    /// </summary>
    private (int hash1, int hash2) GetDoubleHash(T item)
    {
        string itemString = item.ToString();
        byte[] bytes = Encoding.UTF8.GetBytes(itemString);
        
        // Method 1: Using SHA256 to get a good distribution
        byte[] hashBytes = _hashAlgorithm.ComputeHash(bytes);
        int hash1 = BitConverter.ToInt32(hashBytes, 0);
        
        // Method 2: Using built-in hash code with seeds for second hash
        int hash2 = item.GetHashCode();
        
        // Combine with seeds to make hash functions more independent
        hash1 = CombineHash(hash1, _seed1);
        hash2 = CombineHash(hash2, _seed2);
        
        // Ensure positive values
        return (Math.Abs(hash1), Math.Abs(hash2));
    }

    /// <summary>
    /// Combines hash with seed using murmur-style mixing
    /// </summary>
    private int CombineHash(int hash, int seed)
    {
        unchecked
        {
            int h = hash ^ seed;
            h = (h ^ (h >> 16)) * 0x85EBCA77;
            h = (h ^ (h >> 13)) * 0xC2B2AE35;
            h = h ^ (h >> 16);
            return h;
        }
    }

    /// <summary>
    /// Alternative hash function using MurmurHash3 style
    /// </summary>
    private int MurmurHash3(T item, int seed)
    {
        string itemString = item.ToString();
        byte[] data = Encoding.UTF8.GetBytes(itemString);
        
        const uint c1 = 0xcc9e2d51;
        const uint c2 = 0x1b873593;
        const int r1 = 15;
        const int r2 = 13;
        const uint m = 5;
        const uint n = 0xe6546b64;
        
        uint hash = (uint)seed;
        
        int length = data.Length;
        int currentIndex = 0;
        
        while (length >= 4)
        {
            uint k = (uint)(data[currentIndex++] | data[currentIndex++] << 8 | 
                           data[currentIndex++] << 16 | data[currentIndex++] << 24);
            
            k *= c1;
            k = (k << r1) | (k >> (32 - r1));
            k *= c2;
            
            hash ^= k;
            hash = (hash << r2) | (hash >> (32 - r2));
            hash = hash * m + n;
            
            length -= 4;
        }
        
        // Handle remaining bytes
        uint remaining = 0;
        switch (length)
        {
            case 3:
                remaining = (uint)(data[currentIndex + 2] << 16);
                goto case 2;
            case 2:
                remaining |= (uint)(data[currentIndex + 1] << 8);
                goto case 1;
            case 1:
                remaining |= (uint)data[currentIndex];
                remaining *= c1;
                remaining = (remaining << r1) | (remaining >> (32 - r1));
                remaining *= c2;
                hash ^= remaining;
                break;
        }
        
        hash ^= (uint)data.Length;
        
        // Finalization
        hash ^= hash >> 16;
        hash *= 0x85EBCA6B;
        hash ^= hash >> 13;
        hash *= 0xC2B2AE35;
        hash ^= hash >> 16;
        
        return (int)hash;
    }

    /// <summary>
    /// Gets the current false positive probability
    /// </summary>
    public double GetCurrentFalsePositiveProbability()
    {
        int bitsSet = 0;
        for (int i = 0; i < _size; i++)
        {
            if (_bitArray[i])
                bitsSet++;
        }
        
        double fillRatio = bitsSet / (double)_size;
        return Math.Pow(1 - Math.Exp(-_hashFunctionCount * fillRatio), _hashFunctionCount);
    }

    /// <summary>
    /// Gets the estimated number of items in the filter
    /// </summary>
    public int GetEstimatedItemCount()
    {
        int bitsSet = 0;
        for (int i = 0; i < _size; i++)
        {
            if (_bitArray[i])
                bitsSet++;
        }
        
        double fillRatio = bitsSet / (double)_size;
        if (fillRatio == 0) return 0;
        if (fillRatio == 1) return int.MaxValue;
        
        return (int)Math.Ceiling(-_size * Math.Log(1 - fillRatio) / _hashFunctionCount);
    }

    /// <summary>
    /// Clears the Bloom filter
    /// </summary>
    public void Clear()
    {
        _bitArray.SetAll(false);
    }

    /// <summary>
    /// Gets the current fill ratio
    /// </summary>
    public double GetFillRatio()
    {
        int bitsSet = 0;
        for (int i = 0; i < _size; i++)
        {
            if (_bitArray[i])
                bitsSet++;
        }
        return bitsSet / (double)_size;
    }

    /// <summary>
    /// Gets filter statistics
    /// </summary>
    public BloomFilterStatistics GetStatistics()
    {
        return new BloomFilterStatistics
        {
            Size = _size,
            HashFunctionCount = _hashFunctionCount,
            FillRatio = GetFillRatio(),
            EstimatedItemCount = GetEstimatedItemCount(),
            CurrentFalsePositiveRate = GetCurrentFalsePositiveProbability(),
            BitsSet = GetBitsSetCount()
        };
    }

    private int GetBitsSetCount()
    {
        int count = 0;
        for (int i = 0; i < _size; i++)
        {
            if (_bitArray[i])
                count++;
        }
        return count;
    }
}

public class BloomFilterStatistics
{
    public int Size { get; set; }
    public int HashFunctionCount { get; set; }
    public double FillRatio { get; set; }
    public int EstimatedItemCount { get; set; }
    public double CurrentFalsePositiveRate { get; set; }
    public int BitsSet { get; set; }

    public override string ToString()
    {
        return $"Bloom Filter Stats: Size={Size}, Hash Functions={HashFunctionCount}, " +
               $"Fill Ratio={FillRatio:P1}, Estimated Items={EstimatedItemCount}, " +
               $"False Positive Rate={CurrentFalsePositiveRate:P2}";
    }
}
 
```