using System.Diagnostics;

namespace DistributedSystem.GossipProtocol.TestHarness;

/// <summary>
/// Gossip Protocol Simulator - Demonstrates information dissemination in a cluster
/// </summary>
public sealed class GossipSimulator
{
    private readonly Dictionary<string, GossipProtocol> _cluster;

    private int _roundNumber;

    public int ClusterSize => _cluster.Count;
    public int RoundNumber => _roundNumber;

    public GossipSimulator(int clusterSize)
    {
        _cluster = new Dictionary<string, GossipProtocol>();
        _roundNumber = 0;

        // Create cluster nodes
        for (int i = 0; i < clusterSize; i++)
        {
            var nodeId = $"node-{i}";
            var gossip = new GossipProtocol(nodeId, "localhost", 9000 + i);
            _cluster[nodeId] = gossip;
        }

        // Each node learns about all other nodes initially
        var allMembers = _cluster.Values.Select(g => g.Members.First()).ToList();
        foreach (var gossip in _cluster.Values)
        {
            gossip.JoinCluster(allMembers);
        }
    }

    /// <summary>
    /// Runs a single gossip round
    /// </summary>
    public void GossipRound()
    {
        _roundNumber++;

        // Step 1: Each node creates a gossip message
        var messages = new Dictionary<string, GossipMessage>();
        foreach (var (nodeId, gossip) in _cluster)
        {
            var message = gossip.GossipRound();
            if (message != null)
            {
                messages[nodeId] = message;
            }
        }

        // Step 2: Each node sends to F random peers
        foreach (var (senderId, message) in messages)
        {
            var sender = _cluster[senderId];
            var peers = sender.SelectRandomPeers();

            foreach (var peer in peers)
            {
                if (_cluster.TryGetValue(peer.NodeId, out var receiver))
                {
                    receiver.ProcessGossipMessage(message);
                }
            }
        }
    }

    /// <summary>
    /// Simulates a node failure
    /// </summary>
    public void SimulateNodeFailure(string nodeId)
    {
        // Mark failed node as suspected by others
        foreach (var (_, gossip) in _cluster)
        {
            gossip.SuspectPeer(nodeId);
        }
    }

    /// <summary>
    /// Simulates a node recovery
    /// </summary>
    public void SimulateNodeRecovery(string nodeId)
    {
        if (_cluster.TryGetValue(nodeId, out var node))
        {
            foreach (var (_, gossip) in _cluster)
            {
                if (gossip.NodeId != nodeId)
                {
                    gossip.ResurrectPeer(nodeId);
                }
            }
        }
    }

    /// <summary>
    /// Gets the state of a node
    /// </summary>
    public string GetNodeState(string nodeId)
    {
        if (_cluster.TryGetValue(nodeId, out var node))
        {
            return node.ToString();
        }
        return "Node not found";
    }

    /// <summary>
    /// Prints cluster state
    /// </summary>
    public void PrintClusterState()
    {
        Console.WriteLine($"\n=== Cluster State (Round {_roundNumber}) ===");
        foreach (var (nodeId, gossip) in _cluster.OrderBy(x => x.Key))
        {
            Console.WriteLine(gossip.ToString());
        }
    }

    /// <summary>
    /// Checks convergence
    /// </summary>
    public bool CheckConvergence()
    {
        return _cluster.Values.All(g => g.HasConverged());
    }

    /// <summary>
    /// Runs simulation for N rounds
    /// </summary>
    public void RunSimulation(int rounds, Action<int>? onRound = null)
    {
        var sw = Stopwatch.StartNew();

        for (int i = 0; i < rounds; i++)
        {
            GossipRound();
            onRound?.Invoke(i + 1);

            if (CheckConvergence())
            {
                break;
            }
        }

        sw.Stop();
        Console.WriteLine($"Simulation completed in {sw.ElapsedMilliseconds}ms");
    }

    /// <summary>
    /// Gets dissemination statistics
    /// </summary>
    public (int maxDepth, double avgDepth, double convergence) GetStats()
    {
        var depths = _cluster.Values.Select(g => g.GetDisseminationDepth()).ToList();
        var convergence = _cluster.Values.Average(g => g.GetConvergencePercentage());

        return (depths.Max(), depths.Average(), convergence);
    }
}
