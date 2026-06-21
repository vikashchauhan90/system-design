namespace DistributedSystem.Partitioning.ConsistentHashing;

public interface IConsistentHashRing
{
    void AddNode(string nodeId);

    void RemoveNode(string nodeId);

    string GetNode(string key);
}
