using DistributedSystem.Sharding.Abstractions;

namespace DistributedSystem.Sharding.DirectorySharding;

public sealed class DirectoryShardRouter
    : IShardRouter<string>
{
    private readonly Dictionary<string, Shard>
        _directory;

    public DirectoryShardRouter(
        Dictionary<string, Shard> directory)
    {
        _directory = directory;
    }

    public Shard GetShard(string key)
    {
        return _directory[key];
    }
}
