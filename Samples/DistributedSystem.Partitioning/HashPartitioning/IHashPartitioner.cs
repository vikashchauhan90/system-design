namespace DistributedSystem.Partitioning.HashPartitioning;

public interface IHashPartitioner<in TKey>
{
    int GetPartition(TKey key);
}
