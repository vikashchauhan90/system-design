using DistributedSystem.Sharding.Abstractions;

namespace DistributedSystem.Sharding.GeoSharding;

public sealed class GeoShardRouter
{
    private readonly Dictionary<string, Shard>
        _regions;

    public GeoShardRouter(
        Dictionary<string, Shard> regions)
    {
        _regions = regions;
    }

    public Shard GetShardByRegion(
        string region)
    {
        return _regions[region];
    }
}
