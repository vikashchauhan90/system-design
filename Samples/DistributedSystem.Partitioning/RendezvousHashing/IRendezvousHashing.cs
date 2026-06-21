namespace DistributedSystem.Partitioning.RendezvousHashing;

public interface IRendezvousHashing
{
    void AddNode(string nodeId);

    void RemoveNode(string nodeId);

    string GetNode(string key);
}
