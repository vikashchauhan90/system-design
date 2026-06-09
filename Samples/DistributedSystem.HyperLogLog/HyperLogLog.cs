using System.Buffers.Binary;
using System.Numerics;

namespace DistributedSystem.HyperLogLog;

public sealed class HyperLogLog
{
    private const ulong FnvOffsetBasis = 1469598103934665603UL;
    private const ulong FnvPrime = 1099511628211UL;

    private readonly byte[] _registers;

    public int Precision { get; }
    public int RegisterCount { get; }

    public HyperLogLog(int precision = 14)
    {
        if (precision < 4 || precision > 16)
        {
            throw new ArgumentOutOfRangeException(nameof(precision), "Precision must be between 4 and 16.");
        }

        Precision = precision;
        RegisterCount = 1 << precision;
        _registers = new byte[RegisterCount];
    }

    private HyperLogLog(int precision, byte[] registers)
    {
        Precision = precision;
        RegisterCount = 1 << precision;
        _registers = registers;
    }

    public void Add(string value)
    {
        if (value is null)
        {
            throw new ArgumentNullException(nameof(value));
        }

        Add(System.Text.Encoding.UTF8.GetBytes(value));
    }

    public void Add(ReadOnlySpan<byte> data)
    {
        var hash = ComputeHash(data);
        var index = (int)(hash & ((1UL << Precision) - 1UL));
        var remaining = hash >> Precision;
        var rank = LeadingZeroCount(remaining) + 1;

        if (rank > _registers[index])
        {
            _registers[index] = (byte)rank;
        }
    }

    public void Merge(HyperLogLog other)
    {
        if (other is null)
        {
            throw new ArgumentNullException(nameof(other));
        }

        if (other.Precision != Precision)
        {
            throw new InvalidOperationException("Cannot merge HyperLogLog instances with different precision.");
        }

        for (var i = 0; i < RegisterCount; i++)
        {
            if (other._registers[i] > _registers[i])
            {
                _registers[i] = other._registers[i];
            }
        }
    }

    public double Count()
    {
        var m = (double)RegisterCount;
        var sum = 0.0;
        var zeroRegisters = 0;

        foreach (var register in _registers)
        {
            sum += Math.Pow(2.0, -register);
            if (register == 0)
            {
                zeroRegisters++;
            }
        }

        var alpha = EstimateAlpha(m);
        var rawEstimate = alpha * m * m / sum;

        if (rawEstimate <= 2.5 * m && zeroRegisters > 0)
        {
            return m * Math.Log(m / zeroRegisters);
        }

        var twoTo32 = 4294967296.0;
        if (rawEstimate > twoTo32 / 30.0)
        {
            return -twoTo32 * Math.Log(1.0 - rawEstimate / twoTo32);
        }

        return rawEstimate;
    }

    public byte[] ToByteArray()
    {
        var buffer = new byte[1 + _registers.Length];
        buffer[0] = (byte)Precision;
        Array.Copy(_registers, 0, buffer, 1, _registers.Length);
        return buffer;
    }

    public static HyperLogLog FromByteArray(ReadOnlySpan<byte> data)
    {
        if (data.Length < 2)
        {
            throw new ArgumentException("Serialized HyperLogLog data is too short.", nameof(data));
        }

        var precision = data[0];
        var registerCount = 1 << precision;
        if (data.Length != 1 + registerCount)
        {
            throw new ArgumentException("Serialized HyperLogLog data has invalid length.", nameof(data));
        }

        var registers = new byte[registerCount];
        data.Slice(1).CopyTo(registers);
        return new HyperLogLog(precision, registers);
    }

    private static ulong ComputeHash(ReadOnlySpan<byte> data)
    {
        var hash = FnvOffsetBasis;
        foreach (var value in data)
        {
            hash ^= value;
            hash *= FnvPrime;
        }

        return hash;
    }

    private static int LeadingZeroCount(ulong value)
    {
        if (value == 0)
        {
            return 64;
        }

        return BitOperations.LeadingZeroCount(value);
    }

    private static double EstimateAlpha(double m)
    {
        return m switch
        {
            16.0 => 0.673,
            32.0 => 0.697,
            64.0 => 0.709,
            _ => 0.7213 / (1.0 + 1.079 / m)
        };
    }
}
