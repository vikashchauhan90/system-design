namespace DistributedSystem.Partitioning.Abstractions;

public interface IPartitioner<in TKey>
{
    int GetPartition(TKey key);
}
