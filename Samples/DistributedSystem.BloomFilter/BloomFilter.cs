using System.Numerics;

namespace DistributedSystem.BloomFilter;

/// <summary>
/// A space-efficient probabilistic data structure for testing set membership.
/// Returns:
/// - "Definitely NOT in set" (100% certainty) if key is not present
/// - "MIGHT be in set" (with configurable false positive rate) if key might be present
/// </summary>
public sealed class BloomFilter
{
    private const ulong FnvOffsetBasis = 1469598103934665603UL;
    private const ulong FnvPrime = 1099511628211UL;

    private readonly byte[] _bits;
    private readonly int _hashFunctions;

    /// <summary>
    /// Number of bits in the filter
    /// </summary>
    public int Size { get; }

    /// <summary>
    /// Number of hash functions used
    /// </summary>
    public int HashFunctions => _hashFunctions;

    /// <summary>
    /// Memory usage in bytes
    /// </summary>
    public long MemoryUsageBytes => _bits.Length;

    /// <summary>
    /// Estimated false positive rate
    /// </summary>
    public double EstimatedFalsePositiveRate { get; }

    /// <summary>
    /// Creates a Bloom Filter with specified size and hash function count.
    /// </summary>
    /// <param name="sizeInBits">Number of bits in the filter (should be power of 2)</param>
    /// <param name="hashFunctions">Number of hash functions (typically 3-7 for optimal performance)</param>
    public BloomFilter(int sizeInBits, int hashFunctions = 3)
    {
        if (sizeInBits <= 0 || (sizeInBits & (sizeInBits - 1)) != 0)
        {
            throw new ArgumentException("Size must be a positive power of 2.", nameof(sizeInBits));
        }

        if (hashFunctions < 1 || hashFunctions > 16)
        {
            throw new ArgumentOutOfRangeException(nameof(hashFunctions), "Hash functions must be between 1 and 16.");
        }

        Size = sizeInBits;
        _hashFunctions = hashFunctions;
        _bits = new byte[sizeInBits / 8];
        EstimatedFalsePositiveRate = ComputeFalsePositiveRate(sizeInBits, hashFunctions);
    }

    /// <summary>
    /// Creates a Bloom Filter optimized for a specific capacity and desired false positive rate.
    /// </summary>
    /// <param name="expectedCapacity">Expected number of elements to add</param>
    /// <param name="falsePositiveRate">Desired false positive rate (e.g., 0.01 for 1%)</param>
    public static BloomFilter CreateOptimal(int expectedCapacity, double falsePositiveRate = 0.01)
    {
        if (expectedCapacity <= 0)
        {
            throw new ArgumentException("Expected capacity must be positive.", nameof(expectedCapacity));
        }

        if (falsePositiveRate <= 0 || falsePositiveRate >= 1)
        {
            throw new ArgumentOutOfRangeException(nameof(falsePositiveRate), "False positive rate must be between 0 and 1.");
        }

        // Calculate optimal bit array size: m = -1 / ln(2)^2 * n * ln(p)
        var bitsNeeded = (int)Math.Ceiling(-1.0 / (Math.Log(2) * Math.Log(2)) * expectedCapacity * Math.Log(falsePositiveRate));

        // Round up to nearest power of 2
        var powerOfTwo = 1 << (int)Math.Ceiling(Math.Log2(bitsNeeded));

        // Calculate optimal number of hash functions: k = ln(2) * m / n
        var optimalHashCount = Math.Max(1, (int)Math.Round(Math.Log(2) * powerOfTwo / expectedCapacity));
        optimalHashCount = Math.Min(16, optimalHashCount);

        return new BloomFilter(powerOfTwo, optimalHashCount);
    }

    /// <summary>
    /// Adds an element to the filter.
    /// </summary>
    public void Add(string value)
    {
        if (value is null)
        {
            throw new ArgumentNullException(nameof(value));
        }

        Add(System.Text.Encoding.UTF8.GetBytes(value));
    }

    /// <summary>
    /// Adds an element (byte array) to the filter.
    /// </summary>
    public void Add(ReadOnlySpan<byte> data)
    {
        var hashes = ComputeHashes(data);
        foreach (var hash in hashes)
        {
            var bitIndex = (int)(hash % (ulong)Size);
            var byteIndex = bitIndex / 8;
            var bitOffset = bitIndex % 8;
            _bits[byteIndex] |= (byte)(1 << bitOffset);
        }
    }

    /// <summary>
    /// Tests if an element might be in the filter.
    /// Returns false = definitely NOT in set.
    /// Returns true = MIGHT be in set (could be false positive).
    /// </summary>
    public bool MightContain(string value)
    {
        if (value is null)
        {
            throw new ArgumentNullException(nameof(value));
        }

        return MightContain(System.Text.Encoding.UTF8.GetBytes(value));
    }

    /// <summary>
    /// Tests if an element (byte array) might be in the filter.
    /// </summary>
    public bool MightContain(ReadOnlySpan<byte> data)
    {
        var hashes = ComputeHashes(data);
        foreach (var hash in hashes)
        {
            var bitIndex = (int)(hash % (ulong)Size);
            var byteIndex = bitIndex / 8;
            var bitOffset = bitIndex % 8;

            if ((_bits[byteIndex] & (1 << bitOffset)) == 0)
            {
                return false; // Definitely not in set
            }
        }

        return true; // Might be in set (false positive possible)
    }

    /// <summary>
    /// Merges another Bloom Filter into this one (logical OR of all bits).
    /// </summary>
    public void Merge(BloomFilter other)
    {
        if (other is null)
        {
            throw new ArgumentNullException(nameof(other));
        }

        if (other.Size != Size)
        {
            throw new InvalidOperationException("Cannot merge Bloom Filters with different sizes.");
        }

        if (other._hashFunctions != _hashFunctions)
        {
            throw new InvalidOperationException("Cannot merge Bloom Filters with different hash function counts.");
        }

        for (var i = 0; i < _bits.Length; i++)
        {
            _bits[i] |= other._bits[i];
        }
    }

    /// <summary>
    /// Returns the number of set bits (approximation of elements added).
    /// </summary>
    public int EstimateElementCount()
    {
        var setBits = _bits.Sum(b => BitOperations.PopCount(b));
        
        // Estimate: n = -(m/k) * ln(1 - X/m)
        // where m = bit array size, X = set bits, k = hash functions
        var m = (double)Size;
        var x = (double)setBits;
        var k = (double)_hashFunctions;

        if (x >= m)
        {
            return int.MaxValue; // Filter is full or over-full
        }

        var ratio = x / m;
        var estimate = -(m / k) * Math.Log(1 - ratio);
        return Math.Max(1, (int)estimate);
    }

    /// <summary>
    /// Serializes the filter to a byte array for storage/transmission.
    /// </summary>
    public byte[] ToByteArray()
    {
        var buffer = new byte[1 + 1 + _bits.Length];
        buffer[0] = (byte)_hashFunctions;
        buffer[1] = (byte)(Math.Log2(Size) & 0xFF);
        Array.Copy(_bits, 0, buffer, 2, _bits.Length);
        return buffer;
    }

    /// <summary>
    /// Deserializes a Bloom Filter from a byte array.
    /// </summary>
    public static BloomFilter FromByteArray(ReadOnlySpan<byte> data)
    {
        if (data.Length < 2)
        {
            throw new ArgumentException("Serialized Bloom Filter data is too short.", nameof(data));
        }

        var hashFunctions = data[0];
        var sizeBits = 1 << data[1];
        var expectedLength = 2 + (sizeBits / 8);

        if (data.Length != expectedLength)
        {
            throw new ArgumentException("Serialized Bloom Filter data has invalid length.", nameof(data));
        }

        var filter = new BloomFilter(sizeBits, hashFunctions);
        data.Slice(2).CopyTo(filter._bits);
        return filter;
    }

    /// <summary>
    /// Clears all bits in the filter.
    /// </summary>
    public void Clear()
    {
        Array.Clear(_bits, 0, _bits.Length);
    }

    /// <summary>
    /// Computes multiple hash values for the input data.
    /// Uses double hashing technique: h_i(x) = h1(x) + i*h2(x)
    /// </summary>
    private ulong[] ComputeHashes(ReadOnlySpan<byte> data)
    {
        var hash1 = FnvHash(data);
        var hash2 = MurmurHash(data);

        var hashes = new ulong[_hashFunctions];
        for (var i = 0; i < _hashFunctions; i++)
        {
            hashes[i] = hash1 + (ulong)i * hash2;
        }

        return hashes;
    }

    /// <summary>
    /// FNV-1a hash function.
    /// </summary>
    private static ulong FnvHash(ReadOnlySpan<byte> data)
    {
        var hash = FnvOffsetBasis;
        foreach (var b in data)
        {
            hash ^= b;
            hash *= FnvPrime;
        }

        return hash;
    }

    /// <summary>
    /// Simple MurmurHash implementation for generating second hash.
    /// </summary>
    private static ulong MurmurHash(ReadOnlySpan<byte> data)
    {
        const ulong c1 = 0xff51afd7ed558ccdUL;
        const ulong c2 = 0xc4ceb9fe1a85ec53UL;

        ulong h64 = 0;

        // Process 8 bytes at a time
        var len = data.Length;
        int nblocks = len / 8;

        for (int i = 0; i < nblocks; i++)
        {
            var block = System.BitConverter.ToUInt64(data.Slice(i * 8, 8));
            h64 ^= MixBits(block * c1) * c2;
            h64 = RotateLeft(h64, 31);
        }

        // Process remaining bytes
        var tail = data.Slice(nblocks * 8);
        ulong k1 = 0;

        switch (len & 7)
        {
            case 7: k1 ^= (ulong)tail[6] << 48; goto case 6;
            case 6: k1 ^= (ulong)tail[5] << 40; goto case 5;
            case 5: k1 ^= (ulong)tail[4] << 32; goto case 4;
            case 4: k1 ^= (ulong)tail[3] << 24; goto case 3;
            case 3: k1 ^= (ulong)tail[2] << 16; goto case 2;
            case 2: k1 ^= (ulong)tail[1] << 8; goto case 1;
            case 1:
                k1 ^= (ulong)tail[0];
                h64 ^= MixBits(k1);
                break;
        }

        h64 ^= (ulong)len;
        return FinalMix(h64);
    }

    private static ulong MixBits(ulong k)
    {
        k ^= k >> 33;
        k *= 0xff51afd7ed558ccdUL;
        k ^= k >> 33;
        return k;
    }

    private static ulong FinalMix(ulong h64)
    {
        h64 ^= h64 >> 33;
        h64 *= 0xff51afd7ed558ccdUL;
        h64 ^= h64 >> 33;
        return h64;
    }

    private static ulong RotateLeft(ulong x, int r)
    {
        return (x << r) | (x >> (64 - r));
    }

    /// <summary>
    /// Computes theoretical false positive rate: (1 - e^(-k*n/m))^k
    /// where n = estimated items, m = bits, k = hash functions
    /// </summary>
    private static double ComputeFalsePositiveRate(int sizeInBits, int hashFunctions)
    {
        // Using assumption of m bits and k hash functions with no items yet
        // FP rate = (1 - e^(-k/m * ln(2)))^k ≈ 0.6185^(m/k)
        var mBits = (double)sizeInBits;
        var k = (double)hashFunctions;
        var exponent = -k / mBits * Math.Log(2);
        return Math.Pow(1 - Math.Exp(exponent), k);
    }
}
