using System;
using System.Buffers.Binary;

namespace DistributedSystem.CountMinSketch;

public sealed class CountMinSketch
{
    private const ulong FnvOffsetBasis = 14695981039346656037UL;
    private const ulong FnvPrime = 1099511628211UL;

    private readonly uint[,] _table;
    private readonly ulong[] _seeds;

    public int Width { get; }
    public int Depth { get; }

    public CountMinSketch(int width = 2048, int depth = 5)
    {
        if (width <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(width));
        }

        if (depth <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(depth));
        }

        Width = width;
        Depth = depth;

        _table = new uint[Depth, Width];
        _seeds = new ulong[Depth];

        for (var i = 0; i < Depth; i++)
        {
            _seeds[i] = 0x9E3779B97F4A7C15UL * (ulong)(i + 1);
        }
    }

    private CountMinSketch(
        int width,
        int depth,
        uint[,] table,
        ulong[] seeds)
    {
        Width = width;
        Depth = depth;
        _table = table;
        _seeds = seeds;
    }

    public void Add(string value, uint count = 1)
    {
        if (value is null)
        {
            throw new ArgumentNullException(nameof(value));
        }

        Add(System.Text.Encoding.UTF8.GetBytes(value), count);
    }

    public void Add(ReadOnlySpan<byte> data, uint count = 1)
    {
        for (var row = 0; row < Depth; row++)
        {
            var column = GetColumn(data, _seeds[row]);

            checked
            {
                _table[row, column] += count;
            }
        }
    }

    public uint EstimateCount(string value)
    {
        if (value is null)
        {
            throw new ArgumentNullException(nameof(value));
        }

        return EstimateCount(System.Text.Encoding.UTF8.GetBytes(value));
    }

    public uint EstimateCount(ReadOnlySpan<byte> data)
    {
        uint min = uint.MaxValue;

        for (var row = 0; row < Depth; row++)
        {
            var column = GetColumn(data, _seeds[row]);

            if (_table[row, column] < min)
            {
                min = _table[row, column];
            }
        }

        return min;
    }

    public void Merge(CountMinSketch other)
    {
        if (other is null)
        {
            throw new ArgumentNullException(nameof(other));
        }

        if (other.Width != Width || other.Depth != Depth)
        {
            throw new InvalidOperationException(
                "Cannot merge sketches with different dimensions.");
        }

        for (var row = 0; row < Depth; row++)
        {
            for (var column = 0; column < Width; column++)
            {
                checked
                {
                    _table[row, column] += other._table[row, column];
                }
            }
        }
    }

    public byte[] ToByteArray()
    {
        var headerSize = 8;
        var tableSize = Width * Depth * sizeof(uint);

        var buffer = new byte[headerSize + tableSize];

        BinaryPrimitives.WriteInt32LittleEndian(
            buffer.AsSpan(0, 4),
            Width);

        BinaryPrimitives.WriteInt32LittleEndian(
            buffer.AsSpan(4, 4),
            Depth);

        var offset = headerSize;

        for (var row = 0; row < Depth; row++)
        {
            for (var column = 0; column < Width; column++)
            {
                BinaryPrimitives.WriteUInt32LittleEndian(
                    buffer.AsSpan(offset, 4),
                    _table[row, column]);

                offset += 4;
            }
        }

        return buffer;
    }

    public static CountMinSketch FromByteArray(ReadOnlySpan<byte> data)
    {
        if (data.Length < 8)
        {
            throw new ArgumentException(
                "Serialized CountMinSketch data is too short.",
                nameof(data));
        }

        var width = BinaryPrimitives.ReadInt32LittleEndian(
            data.Slice(0, 4));

        var depth = BinaryPrimitives.ReadInt32LittleEndian(
            data.Slice(4, 4));

        var expectedLength = 8 + width * depth * sizeof(uint);

        if (data.Length != expectedLength)
        {
            throw new ArgumentException(
                "Serialized CountMinSketch data has invalid length.",
                nameof(data));
        }

        var table = new uint[depth, width];

        var offset = 8;

        for (var row = 0; row < depth; row++)
        {
            for (var column = 0; column < width; column++)
            {
                table[row, column] =
                    BinaryPrimitives.ReadUInt32LittleEndian(
                        data.Slice(offset, 4));

                offset += 4;
            }
        }

        var seeds = new ulong[depth];

        for (var i = 0; i < depth; i++)
        {
            seeds[i] = 0x9E3779B97F4A7C15UL * (ulong)(i + 1);
        }

        return new CountMinSketch(
            width,
            depth,
            table,
            seeds);
    }

    private int GetColumn(
        ReadOnlySpan<byte> data,
        ulong seed)
    {
        var hash = ComputeHash(data, seed);
        return (int)(hash % (ulong)Width);
    }

    private static ulong ComputeHash(
        ReadOnlySpan<byte> data,
        ulong seed)
    {
        var hash = FnvOffsetBasis ^ seed;

        foreach (var b in data)
        {
            hash ^= b;
            hash *= FnvPrime;
        }

        return hash;
    }
}
