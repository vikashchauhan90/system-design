using DistributedSystem.GossipProtocol;

namespace DistributedSystem.GossipProtocol.TestHarness;

/// <summary>
/// Advanced Gossip Protocol scenarios
/// </summary>
public static class AdvancedGossipExamples
{
    public static void Main(string[] args)
    {
        Console.WriteLine("=== Advanced Gossip Protocol Scenarios ===\n");

        KafkaLikeClusterMembershipExample();
        PartitionRebalancingExample();
        CascadingFailureDetectionExample();
        NetworkPartitionExample();
        LoadRebalancingExample();
    }

    /// <summary>
    /// Kafka-like cluster membership with broker information
    /// </summary>
    private static void KafkaLikeClusterMembershipExample()
    {
        Console.WriteLine("1. Kafka-like Cluster Membership");
        Console.WriteLine("===============================\n");

        var cluster = new Dictionary<string, GossipProtocol>();

        // Create 5 Kafka brokers
        for (int i = 0; i < 5; i++)
        {
            var brokerId = $"broker-{i}";
            var gossip = new GossipProtocol(brokerId, $"kafka-broker-{i}", 9092 + i);

            // Set broker metadata
            gossip.UpdateMetadata("rack_id", $"rack-{i % 2}");
            gossip.UpdateMetadata("controller", i == 0 ? "true" : "false");
            gossip.UpdateMetadata("version", "2.8.0");

            cluster[brokerId] = gossip;
        }

        // Brokers join cluster
        var seedBrokers = cluster.Values.Select(g => g.Members.First()).ToList();
        foreach (var gossip in cluster.Values.Skip(1))
        {
            gossip.JoinCluster(seedBrokers);
        }

        // Disseminate information
        Console.WriteLine("Initial cluster formation:");
        for (int round = 0; round < 5; round++)
        {
            foreach (var gossip in cluster.Values)
            {
                var msg = gossip.GossipRound();
                if (msg != null)
                {
                    foreach (var peer in gossip.SelectRandomPeers())
                    {
                        if (cluster.TryGetValue(peer.NodeId, out var receiver))
                        {
                            receiver.ProcessGossipMessage(msg);
                        }
                    }
                }
            }

            if (cluster.Values.All(g => g.HasConverged()))
                break;
        }

        Console.WriteLine("\nBroker Information Learned:");
        foreach (var (brokerId, gossip) in cluster)
        {
            Console.WriteLine($"\n{brokerId}:");
            Console.WriteLine($"  Alive peers: {gossip.AlivePeers.Count()}");
            foreach (var peer in gossip.AlivePeers.Take(3))
            {
                Console.WriteLine($"    - {peer}");
            }

            var controller = gossip.GetMetadata(cluster.Keys.First(), "controller");
            Console.WriteLine($"  Controller: {controller}");
        }

        Console.WriteLine();
    }

    /// <summary>
    /// Partition rebalancing detection
    /// </summary>
    private static void PartitionRebalancingExample()
    {
        Console.WriteLine("2. Partition Rebalancing Detection");
        Console.WriteLine("==================================\n");

        var cluster = new Dictionary<string, GossipProtocol>();

        // Create 4 brokers with partition assignments
        for (int i = 0; i < 4; i++)
        {
            var brokerId = $"broker-{i}";
            var gossip = new GossipProtocol(brokerId, $"host-{i}", 9092 + i);

            // Initial partition assignment
            var partitions = string.Join(",", Enumerable.Range(i * 5, 5));
            gossip.UpdateMetadata("assigned_partitions", partitions);

            cluster[brokerId] = gossip;
        }

        var seedBrokers = cluster.Values.Select(g => g.Members.First()).ToList();
        foreach (var gossip in cluster.Values.Skip(1))
        {
            gossip.JoinCluster(seedBrokers);
        }

        // Simulate rebalancing on one broker
        Console.WriteLine("Initial state - Each broker owns 5 partitions:");
        foreach (var (brokerId, gossip) in cluster)
        {
            var partitions = gossip.GetMetadata(brokerId, "assigned_partitions");
            Console.WriteLine($"  {brokerId}: {partitions}");
        }

        // Broker-1 updates its assignment (scaled down due to failure)
        Console.WriteLine("\n>>> Broker-1 rebalances - losing partitions 5-9");
        cluster["broker-1"].UpdateMetadata("assigned_partitions", "");

        // Disseminate through gossip
        for (int round = 0; round < 5; round++)
        {
            foreach (var gossip in cluster.Values)
            {
                var msg = gossip.GossipRound();
                if (msg != null)
                {
                    foreach (var peer in gossip.SelectRandomPeers())
                    {
                        if (cluster.TryGetValue(peer.NodeId, out var receiver))
                        {
                            receiver.ProcessGossipMessage(msg);
                        }
                    }
                }
            }
        }

        Console.WriteLine("\nAfter rebalancing is gossiped:");
        foreach (var (brokerId, gossip) in cluster)
        {
            var partitions = gossip.GetMetadata("broker-1", "assigned_partitions");
            Console.WriteLine($"  {brokerId} knows: broker-1 now owns {partitions ?? "nothing"}");
        }

        Console.WriteLine();
    }

    /// <summary>
    /// Cascading failure detection
    /// </summary>
    private static void CascadingFailureDetectionExample()
    {
        Console.WriteLine("3. Cascading Failure Detection");
        Console.WriteLine("=============================\n");

        var cluster = new Dictionary<string, GossipProtocol>();

        for (int i = 0; i < 10; i++)
        {
            var nodeId = $"node-{i}";
            var gossip = new GossipProtocol(nodeId, $"host-{i}", 9000 + i);
            cluster[nodeId] = gossip;
        }

        var seedMembers = cluster.Values.Select(g => g.Members.First()).ToList();
        foreach (var gossip in cluster.Values.Skip(1))
        {
            gossip.JoinCluster(seedMembers);
        }

        // Converge
        for (int i = 0; i < 5; i++)
        {
            foreach (var gossip in cluster.Values)
            {
                var msg = gossip.GossipRound();
                if (msg != null)
                {
                    foreach (var peer in gossip.SelectRandomPeers())
                    {
                        if (cluster.TryGetValue(peer.NodeId, out var receiver))
                        {
                            receiver.ProcessGossipMessage(msg);
                        }
                    }
                }
            }
        }

        // Simulate cascading failures
        Console.WriteLine("Simulating cascading failures: node-5 fails, triggering suspect of node-6, node-7");
        cluster["node-5"].ConfirmDead("node-5");

        // These nodes suspect the chain
        cluster["node-6"].SuspectPeer("node-7");
        cluster["node-7"].SuspectPeer("node-8");

        // Disseminate suspicions
        for (int round = 0; round < 3; round++)
        {
            Console.WriteLine($"\nRound {round + 1}:");
            foreach (var (nodeId, gossip) in cluster.OrderBy(x => x.Key).Take(5))
            {
                Console.WriteLine($"  {nodeId}: Suspected={gossip.SuspectedPeers.Count()} Dead={gossip.DeadPeers.Count()}");
            }

            foreach (var gossip in cluster.Values)
            {
                var msg = gossip.GossipRound();
                if (msg != null)
                {
                    foreach (var peer in gossip.SelectRandomPeers())
                    {
                        if (cluster.TryGetValue(peer.NodeId, out var receiver))
                        {
                            receiver.ProcessGossipMessage(msg);
                        }
                    }
                }
            }
        }

        Console.WriteLine();
    }

    /// <summary>
    /// Network partition scenario (split-brain)
    /// </summary>
    private static void NetworkPartitionExample()
    {
        Console.WriteLine("4. Network Partition Detection");
        Console.WriteLine("=============================\n");

        var clusterA = new Dictionary<string, GossipProtocol>();
        var clusterB = new Dictionary<string, GossipProtocol>();

        // Create partition A (3 nodes)
        for (int i = 0; i < 3; i++)
        {
            var nodeId = $"node-A{i}";
            clusterA[nodeId] = new GossipProtocol(nodeId, $"hostA-{i}", 9000 + i);
        }

        // Create partition B (2 nodes)
        for (int i = 0; i < 2; i++)
        {
            var nodeId = $"node-B{i}";
            clusterB[nodeId] = new GossipProtocol(nodeId, $"hostB-{i}", 9100 + i);
        }

        // Initially, let them see each other
        var allMembers = clusterA.Values.Concat(clusterB.Values).Select(g => g.Members.First()).ToList();
        foreach (var gossip in clusterA.Values.Concat(clusterB.Values))
        {
            gossip.JoinCluster(allMembers);
        }

        // Converge
        for (int i = 0; i < 5; i++)
        {
            foreach (var gossip in clusterA.Values.Concat(clusterB.Values))
            {
                var msg = gossip.GossipRound();
                if (msg != null)
                {
                    foreach (var peer in gossip.SelectRandomPeers())
                    {
                        var allCluster = clusterA.Concat(clusterB).ToDictionary(x => x.Key, x => x.Value);
                        if (allCluster.TryGetValue(peer.NodeId, out var receiver))
                        {
                            receiver.ProcessGossipMessage(msg);
                        }
                    }
                }
            }
        }

        Console.WriteLine("Before network partition:");
        Console.WriteLine($"  Partition A alive nodes: {clusterA.Values.Average(g => g.AlivePeers.Count()):F1}");
        Console.WriteLine($"  Partition B alive nodes: {clusterB.Values.Average(g => g.AlivePeers.Count()):F1}");

        // Simulate network partition - no communication between A and B
        Console.WriteLine("\n>>> Network partition occurs - A and B cannot communicate");

        for (int round = 0; round < 8; round++)
        {
            // A gossips internally
            foreach (var gossip in clusterA.Values)
            {
                var msg = gossip.GossipRound();
                if (msg != null)
                {
                    foreach (var peer in gossip.SelectRandomPeers())
                    {
                        if (clusterA.TryGetValue(peer.NodeId, out var receiver))
                        {
                            receiver.ProcessGossipMessage(msg);
                        }
                    }
                }
            }

            // B gossips internally
            foreach (var gossip in clusterB.Values)
            {
                var msg = gossip.GossipRound();
                if (msg != null)
                {
                    foreach (var peer in gossip.SelectRandomPeers())
                    {
                        if (clusterB.TryGetValue(peer.NodeId, out var receiver))
                        {
                            receiver.ProcessGossipMessage(msg);
                        }
                    }
                }
            }
        }

        Console.WriteLine("\nAfter network partition:");
        Console.WriteLine($"  Partition A alive nodes: {clusterA.Values.Average(g => g.AlivePeers.Count()):F1}");
        Console.WriteLine($"  Partition B alive nodes: {clusterB.Values.Average(g => g.AlivePeers.Count()):F1}");
        Console.WriteLine("  (Each partition thinks the other is dead)");

        Console.WriteLine();
    }

    /// <summary>
    /// Load rebalancing via gossip
    /// </summary>
    private static void LoadRebalancingExample()
    {
        Console.WriteLine("5. Load Rebalancing via Gossip");
        Console.WriteLine("=============================\n");

        var cluster = new Dictionary<string, GossipProtocol>();

        // Create nodes with different loads
        for (int i = 0; i < 6; i++)
        {
            var nodeId = $"node-{i}";
            var gossip = new GossipProtocol(nodeId, $"host-{i}", 9000 + i);

            // Initial loads (high variance)
            var load = i == 0 ? 9000 : i == 1 ? 8500 : 2000;
            gossip.UpdateMetadata("load", load);
            gossip.UpdateMetadata("partition_count", i == 0 ? 450 : i == 1 ? 425 : 200);

            cluster[nodeId] = gossip;
        }

        var seedMembers = cluster.Values.Select(g => g.Members.First()).ToList();
        foreach (var gossip in cluster.Values.Skip(1))
        {
            gossip.JoinCluster(seedMembers);
        }

        Console.WriteLine("Initial Load Distribution:");
        foreach (var (nodeId, gossip) in cluster)
        {
            var load = gossip.GetMetadata(nodeId, "load");
            var partitions = gossip.GetMetadata(nodeId, "partition_count");
            Console.WriteLine($"  {nodeId}: Load={load}, Partitions={partitions}");
        }

        // Disseminate load information
        for (int round = 0; round < 5; round++)
        {
            foreach (var gossip in cluster.Values)
            {
                var msg = gossip.GossipRound();
                if (msg != null)
                {
                    foreach (var peer in gossip.SelectRandomPeers())
                    {
                        if (cluster.TryGetValue(peer.NodeId, out var receiver))
                        {
                            receiver.ProcessGossipMessage(msg);
                        }
                    }
                }
            }
        }

        Console.WriteLine("\nLoad Distribution Known by node-3:");
        var node3 = cluster["node-3"];
        foreach (var (nodeId, _) in cluster)
        {
            var load = node3.GetMetadata(nodeId, "load");
            var partitions = node3.GetMetadata(nodeId, "partition_count");
            Console.WriteLine($"  {nodeId}: Load={load}, Partitions={partitions}");
        }

        // Simulate rebalancing decision
        Console.WriteLine("\nRebalancing triggered:");
        Console.WriteLine("  node-0 rebalances: 450 → 200 partitions (moved 250 to node-4)");
        cluster["node-0"].UpdateMetadata("partition_count", 200);
        cluster["node-4"].UpdateMetadata("partition_count", 450);

        // Disseminate new state
        for (int round = 0; round < 3; round++)
        {
            foreach (var gossip in cluster.Values)
            {
                var msg = gossip.GossipRound();
                if (msg != null)
                {
                    foreach (var peer in gossip.SelectRandomPeers())
                    {
                        if (cluster.TryGetValue(peer.NodeId, out var receiver))
                        {
                            receiver.ProcessGossipMessage(msg);
                        }
                    }
                }
            }
        }

        Console.WriteLine("\nUpdated Load Distribution Known by node-3:");
        foreach (var (nodeId, _) in cluster)
        {
            var partitions = node3.GetMetadata(nodeId, "partition_count");
            Console.WriteLine($"  {nodeId}: Partitions={partitions}");
        }

        Console.WriteLine();
    }
}
