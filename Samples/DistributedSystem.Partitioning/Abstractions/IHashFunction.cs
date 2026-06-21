namespace DistributedSystem.Partitioning.Abstractions;

public interface IHashFunction
{
    ulong ComputeHash(ReadOnlySpan<byte> data);
}
