using DistributedSystem.Sharding.Abstractions;

namespace DistributedSystem.Sharding.TimeSeriesSharding;

public sealed class TimeShardRouter
{
    private readonly Dictionary<string, Shard>
        _shards;

    public TimeShardRouter(
        Dictionary<string, Shard> shards)
    {
        _shards = shards;
    }

    public Shard GetShard(
        DateTime timestamp)
    {
        var key =
            $"{timestamp:yyyy-MM}";

        return _shards[key];
    }
}
