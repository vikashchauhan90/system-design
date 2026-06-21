namespace DistributedSystem.Sharding.Abstractions;

public sealed record Shard(
    string Id,
    string ConnectionString);
