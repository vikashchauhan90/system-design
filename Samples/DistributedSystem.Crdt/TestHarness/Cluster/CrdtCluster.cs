namespace DistributedSystem.Crdt.TestHarness.Cluster;

using DistributedSystem.Crdt.TestHarness.Network;

public sealed class CrdtCluster<T>
{
    public List<CrdtNode<T>> Nodes { get; } = new();

    public InMemoryNetwork<T> Network { get; } = new();
    public CrdtNode<T> AddNode(string nodeId, T initialState)
    {
        var node = new CrdtNode<T>(nodeId, initialState);

        Nodes.Add(node);
        Network.Register(node);

        return node;
    }
    public void SyncAll(Action<T, T> mergeAction)
    {
        foreach (var node in Nodes)
        {
            foreach (var other in Nodes)
            {
                if (node.NodeId == other.NodeId)
                    continue;

                mergeAction(node.State, other.State);
            }
        }
    }
}
