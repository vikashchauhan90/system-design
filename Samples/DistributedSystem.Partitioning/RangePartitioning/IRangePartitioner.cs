namespace DistributedSystem.Partitioning.RangePartitioning;

public interface IRangePartitioner<T>
{
    int GetPartition(T value);

    void AddRange(
        T start,
        T end,
        int partition);
}
