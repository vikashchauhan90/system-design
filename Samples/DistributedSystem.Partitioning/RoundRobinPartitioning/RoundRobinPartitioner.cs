namespace DistributedSystem.Partitioning.RoundRobinPartitioning;

public sealed class RoundRobinPartitioner
    : IRoundRobinPartitioner
{
    private readonly int _partitionCount;
    private int _current;

    public RoundRobinPartitioner(int partitionCount)
    {
        if (partitionCount <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(partitionCount));
        }

        _partitionCount = partitionCount;
    }

    public int Next()
    {
        var partition = _current;

        _current =
            (_current + 1) % _partitionCount;

        return partition;
    }
}
