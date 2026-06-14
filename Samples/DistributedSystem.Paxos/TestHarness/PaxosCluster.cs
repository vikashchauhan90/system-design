using DistributedSystem.Paxos.Models;

namespace DistributedSystem.Paxos.TestHarness;

public sealed class PaxosCluster
{
    public IReadOnlyList<PaxosNode> Nodes { get; }

    public InMemoryMessageBus MessageBus { get; }

    public PaxosCluster(int replicaCount)
    {
        var nodes = new List<PaxosNode>();

        for (int i = 0; i < replicaCount; i++)
        {
            nodes.Add(
                new PaxosNode($"node-{i + 1}"));
        }

        Nodes = nodes;

        MessageBus = new InMemoryMessageBus(nodes);
    }

    public BallotNumber NextBallot(
        string nodeId,
        long value)
    {
        return new BallotNumber(value, nodeId);
    }
}
