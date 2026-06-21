using DistributedSystem.Sharding.Abstractions;
using DistributedSystem.Partitioning.HashPartitioning;
using DistributedSystem.Partitioning.Core;

namespace DistributedSystem.Sharding.HashSharding;

public sealed class HashShardRouter
    : IShardRouter<string>
{
    private readonly IReadOnlyList<Shard> _shards;
    private readonly HashPartitioner _partitioner;

    public HashShardRouter(
        IReadOnlyList<Shard> shards)
    {
        _shards = shards;

        _partitioner =
            new HashPartitioner(
                shards.Count,
                new Fnv1aHashFunction());
    }

    public Shard GetShard(string key)
    {
        var partition =
            _partitioner.GetPartition(key);

        return _shards[partition];
    }
}
