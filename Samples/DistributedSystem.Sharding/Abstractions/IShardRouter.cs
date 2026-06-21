namespace DistributedSystem.Sharding.Abstractions;

public interface IShardRouter<in TKey>
{
    Shard GetShard(TKey key);
}
