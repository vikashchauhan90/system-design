namespace DistributedSystem.Partitioning.RangePartitioning;

public sealed record PartitionRange<T>(
    T Start,
    T End,
    int Partition)
    where T : IComparable<T>;
