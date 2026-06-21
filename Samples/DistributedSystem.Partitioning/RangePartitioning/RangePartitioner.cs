namespace DistributedSystem.Partitioning.RangePartitioning;

public sealed class RangePartitioner<T>
    : IRangePartitioner<T>
    where T : IComparable<T>
{
    private readonly List<PartitionRange<T>>
        _ranges = new();

    public void AddRange(
        T start,
        T end,
        int partition)
    {
        _ranges.Add(
            new PartitionRange<T>(
                start,
                end,
                partition));
    }

    public int GetPartition(T value)
    {
        foreach (var range in _ranges)
        {
            if (value.CompareTo(range.Start) >= 0 &&
                value.CompareTo(range.End) <= 0)
            {
                return range.Partition;
            }
        }

        throw new InvalidOperationException(
            $"No partition found for {value}");
    }
}
