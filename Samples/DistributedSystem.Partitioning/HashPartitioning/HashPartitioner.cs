using System.Text;
using DistributedSystem.Partitioning.Abstractions;

namespace DistributedSystem.Partitioning.HashPartitioning;

public sealed class HashPartitioner
    : IHashPartitioner<string>,
      IPartitioner<string>
{
    private readonly int _partitionCount;
    private readonly IHashFunction _hashFunction;

    public HashPartitioner(
        int partitionCount,
        IHashFunction hashFunction)
    {
        _partitionCount = partitionCount;
        _hashFunction = hashFunction;
    }

    public int GetPartition(string key)
    {
        var hash =
            _hashFunction.ComputeHash(
                Encoding.UTF8.GetBytes(key));

        return (int)(hash % (ulong)_partitionCount);
    }
}
