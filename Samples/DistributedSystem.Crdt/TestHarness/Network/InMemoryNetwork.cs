using DistributedSystem.Crdt.TestHarness.Cluster;

namespace DistributedSystem.Crdt.TestHarness.Network;

public sealed class InMemoryNetwork<T>
{
    private readonly List<CrdtNode<T>> _nodes = new();
    public void Register(CrdtNode<T> node)
    {
        _nodes.Add(node);
    }
    public async Task BroadcastAsync(
    string sourceNodeId,
    Action<T, T> mergeAction,
    int delayMs = 0)
    {
        var source = _nodes.First(n => n.NodeId == sourceNodeId);

        foreach (var node in _nodes)
        {
            if (node.NodeId == sourceNodeId)
                continue;

            await Task.Delay(delayMs);

            mergeAction(node.State, source.State);
        }
    }
}
