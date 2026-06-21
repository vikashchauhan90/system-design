using DistributedSystem.Partitioning.Abstractions;

namespace DistributedSystem.Partitioning.Core;

public sealed class Fnv1aHashFunction : IHashFunction
{
    private const ulong OffsetBasis = 14695981039346656037UL;
    private const ulong Prime = 1099511628211UL;

    public ulong ComputeHash(ReadOnlySpan<byte> data)
    {
        var hash = OffsetBasis;

        foreach (var b in data)
        {
            hash ^= b;
            hash *= Prime;
        }

        return hash;
    }
}
