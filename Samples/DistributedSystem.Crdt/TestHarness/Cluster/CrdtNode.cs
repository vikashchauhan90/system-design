namespace DistributedSystem.Crdt.TestHarness.Cluster;

public sealed class CrdtNode<T>
{
    public string NodeId { get; }

    public T State { get; set; }

    public CrdtNode(string nodeId, T initialState)
    {
        NodeId = nodeId;
        State = initialState;
    }
}
