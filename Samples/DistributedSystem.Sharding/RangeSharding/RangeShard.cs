namespace DistributedSystem.Sharding.RangeSharding;

public sealed record RangeShard<TKey, TShard>(
    TKey Start,
    TKey End,
    TShard Shard)
    where TKey : IComparable<TKey>;
