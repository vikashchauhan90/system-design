using System.Collections;
using System.Security.Cryptography;
using System.Text;

namespace DistributedSystem.LSMTree;

internal sealed class BloomFilter
{
    private readonly int _bitCount;
    private readonly int _hashCount;
    private readonly BitArray _bits;

    public BloomFilter(int expectedItemCount, double falsePositiveRate = 0.01)
    {
        if (expectedItemCount <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(expectedItemCount));
        }

        if (falsePositiveRate <= 0 || falsePositiveRate >= 1)
        {
            throw new ArgumentOutOfRangeException(nameof(falsePositiveRate));
        }

        _bitCount = Math.Max(64, (int)Math.Ceiling(-expectedItemCount * Math.Log(falsePositiveRate) / Math.Pow(Math.Log(2), 2)));
        _hashCount = Math.Max(1, (int)Math.Round((_bitCount / (double)expectedItemCount) * Math.Log(2)));
        _bits = new BitArray(_bitCount);
    }

    private BloomFilter(int bitCount, int hashCount, BitArray bits)
    {
        _bitCount = bitCount;
        _hashCount = hashCount;
        _bits = bits;
    }

    public void Add(string value)
    {
        foreach (var bitIndex in GetHashIndices(value))
        {
            _bits[bitIndex] = true;
        }
    }

    public bool MightContain(string value)
    {
        foreach (var bitIndex in GetHashIndices(value))
        {
            if (!_bits[bitIndex])
            {
                return false;
            }
        }

        return true;
    }

    public byte[] ToByteArray()
    {
        using var stream = new MemoryStream();
        using (var writer = new BinaryWriter(stream, Encoding.UTF8, leaveOpen: true))
        {
            writer.Write(_bitCount);
            writer.Write(_hashCount);
            var bytes = new byte[(_bitCount + 7) / 8];
            _bits.CopyTo(bytes, 0);
            writer.Write(bytes.Length);
            writer.Write(bytes);
        }

        return stream.ToArray();
    }

    public static BloomFilter FromByteArray(byte[] payload)
    {
        using var stream = new MemoryStream(payload);
        using var reader = new BinaryReader(stream, Encoding.UTF8, leaveOpen: false);

        var bitCount = reader.ReadInt32();
        var hashCount = reader.ReadInt32();
        var byteLength = reader.ReadInt32();
        var bytes = reader.ReadBytes(byteLength);
        var bits = new BitArray(bytes);

        return new BloomFilter(bitCount, hashCount, bits);
    }

    private IEnumerable<int> GetHashIndices(string value)
    {
        var bytes = Encoding.UTF8.GetBytes(value);
        var hash = SHA256.HashData(bytes);
        var hash1 = BitConverter.ToInt32(hash, 0);
        var hash2 = BitConverter.ToInt32(hash, 4);

        for (var i = 0; i < _hashCount; i++)
        {
            var combined = (hash1 + (i * hash2)) & 0x7fffffff;
            yield return combined % _bitCount;
        }
    }
}
