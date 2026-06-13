using System.Diagnostics;
using DistributedSystem.GossipProtocol;

namespace GossipProtocolExamples;

/// <summary>
/// Gossip Protocol Simulator - Demonstrates information dissemination in a cluster
/// </summary>
public sealed class GossipSimulator
{
    private readonly Dictionary<string, GossipProtocol> _cluster;
    private readonly int _gossipIntervalMs;
    private readonly Random _random;
    private int _roundNumber;

    public int ClusterSize => _cluster.Count;
    public int RoundNumber => _roundNumber;

    public GossipSimulator(int clusterSize, int gossipIntervalMs = 100)
    {
        _cluster = new Dictionary<string, GossipProtocol>();
        _gossipIntervalMs = gossipIntervalMs;
        _random = new Random();
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

/// <summary>
/// Examples demonstrating Gossip Protocol
/// </summary>
public static class GossipProtocolExamples
{
    public static void Main(string[] args)
    {
        Console.WriteLine("=== Gossip Protocol Examples ===\n");

        BasicClusterFormationExample();
        FailureDetectionExample();
        InformationDisseminationExample();
        ConvergenceAnalysisExample();
        LargeClusterSimulationExample();
        MetadataSpreadingExample();
    }

    /// <summary>
    /// Basic cluster formation
    /// </summary>
    private static void BasicClusterFormationExample()
    {
        Console.WriteLine("1. Basic Cluster Formation");
        Console.WriteLine("========================\n");

        var simulator = new GossipSimulator(5);

        Console.WriteLine($"Created cluster with {simulator.ClusterSize} nodes\n");

        // Run a few gossip rounds
        for (int i = 0; i < 3; i++)
        {
            simulator.GossipRound();
            simulator.PrintClusterState();
        }

        Console.WriteLine();
    }

    /// <summary>
    /// Failure detection and suspicion
    /// </summary>
    private static void FailureDetectionExample()
    {
        Console.WriteLine("2. Failure Detection Example");
        Console.WriteLine("==========================\n");

        var simulator = new GossipSimulator(5);

        // Initial state
        Console.WriteLine("Initial state:");
        simulator.PrintClusterState();

        // Run rounds until convergence
        for (int i = 0; i < 10; i++)
        {
            simulator.GossipRound();
            if (simulator.CheckConvergence())
            {
                Console.WriteLine($"\nConverged after {i + 1} rounds");
                break;
            }
        }

        // Simulate node failure
        Console.WriteLine("\n>>> Simulating failure of node-2...");
        simulator.SimulateNodeFailure("node-2");

        // Run rounds to disseminate failure information
        for (int i = 0; i < 5; i++)
        {
            simulator.GossipRound();
        }

        Console.WriteLine("\nAfter failure dissemination:");
        simulator.PrintClusterState();

        // Simulate recovery
        Console.WriteLine("\n>>> Simulating recovery of node-2...");
        simulator.SimulateNodeRecovery("node-2");

        for (int i = 0; i < 5; i++)
        {
            simulator.GossipRound();
        }

        Console.WriteLine("\nAfter recovery dissemination:");
        simulator.PrintClusterState();

        Console.WriteLine();
    }

    /// <summary>
    /// Information dissemination timing
    /// </summary>
    private static void InformationDisseminationExample()
    {
        Console.WriteLine("3. Information Dissemination Timing");
        Console.WriteLine("===================================\n");

        var simulator = new GossipSimulator(10);

        Console.WriteLine("Tracking how fast information spreads through cluster...\n");

        var previousDepth = 0;
        var maxDepth = 0;

        for (int round = 0; round < 10; round++)
        {
            simulator.GossipRound();

            var depth = simulator._cluster.Values.First().GetDisseminationDepth();
            var convergence = simulator._cluster.Values.Average(g => g.GetConvergencePercentage());

            if (depth > maxDepth) maxDepth = depth;

            Console.WriteLine($"Round {round + 1:D2}: Depth={depth:D2}/{simulator.ClusterSize:D2} | Convergence={convergence:F1}%");

            if (simulator.CheckConvergence())
            {
                Console.WriteLine($"\n✓ Converged after {round + 1} rounds");
                Console.WriteLine($"  Expected: O(log {simulator.ClusterSize}) ≈ {Math.Ceiling(Math.Log2(simulator.ClusterSize))} rounds");
                break;
            }
        }

        Console.WriteLine();
    }

    /// <summary>
    /// Convergence analysis with different cluster sizes
    /// </summary>
    private static void ConvergenceAnalysisExample()
    {
        Console.WriteLine("4. Convergence Analysis");
        Console.WriteLine("======================\n");

        Console.WriteLine("Cluster Size | Rounds to Converge | Expected O(log N)");
        Console.WriteLine("-------------|-------------------|-------------------");

        var clusterSizes = new[] { 5, 10, 20, 50, 100 };

        foreach (var size in clusterSizes)
        {
            var simulator = new GossipSimulator(size);
            var rounds = 0;

            for (int i = 0; i < 50; i++)
            {
                simulator.GossipRound();
                rounds++;
                if (simulator.CheckConvergence())
                    break;
            }

            var expected = Math.Ceiling(Math.Log2(size));
            Console.WriteLine($"{size,12} | {rounds,18} | {expected,17}");
        }

        Console.WriteLine();
    }

    /// <summary>
    /// Large cluster simulation with statistics
    /// </summary>
    private static void LargeClusterSimulationExample()
    {
        Console.WriteLine("5. Large Cluster Simulation (100 nodes)");
        Console.WriteLine("======================================\n");

        var simulator = new GossipSimulator(100);

        Console.WriteLine("Simulating information dissemination in 100-node cluster...\n");

        var sw = Stopwatch.StartNew();

        for (int round = 0; round < 20; round++)
        {
            simulator.GossipRound();

            if ((round + 1) % 3 == 0)
            {
                var (maxDepth, avgDepth, convergence) = simulator.GetStats();
                Console.WriteLine($"Round {round + 1:D2}: Avg Depth={avgDepth:F1} | Max Depth={maxDepth} | Convergence={convergence:F1}%");

                if (simulator.CheckConvergence())
                {
                    sw.Stop();
                    Console.WriteLine($"\n✓ Converged after {round + 1} rounds in {sw.ElapsedMilliseconds}ms");
                    Console.WriteLine($"  O(log 100) = {Math.Log2(100):F2} ≈ {Math.Ceiling(Math.Log2(100))} rounds");
                    break;
                }
            }
        }

        Console.WriteLine();
    }

    /// <summary>
    /// Metadata spreading through cluster
    /// </summary>
    private static void MetadataSpreadingExample()
    {
        Console.WriteLine("6. Metadata Spreading Example");
        Console.WriteLine("============================\n");

        var simulator = new GossipSimulator(8);

        // Node-0 updates its metadata
        Console.WriteLine("Node-0 updates its partition assignment: [0, 1, 2, 3]");
        simulator._cluster["node-0"].UpdateMetadata("partitions", "[0,1,2,3]");
        simulator._cluster["node-0"].UpdateMetadata("load", "1000");

        Console.WriteLine("\nSpread through gossip rounds:");

        for (int round = 0; round < 8; round++)
        {
            simulator.GossipRound();

            var nodesAware = simulator._cluster
                .Count(kvp => kvp.Value.GetMetadata("node-0", "partitions") != null);

            Console.WriteLine($"Round {round + 1}: {nodesAware}/{simulator.ClusterSize} nodes aware of metadata");

            if (nodesAware == simulator.ClusterSize)
            {
                Console.WriteLine("✓ All nodes learned the metadata");
                break;
            }
        }

        Console.WriteLine();
    }
}
