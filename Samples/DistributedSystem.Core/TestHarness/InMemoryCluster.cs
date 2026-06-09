using System;
using System.Threading.Tasks;

namespace DistributedSystem.Core.Raft.TestHarness;

public static class InMemoryCluster
{
    public static async Task DemoAsync()
    {
        var dataFolder = Path.Combine(Directory.GetCurrentDirectory(), "raft-data");
        var storage = new DistributedSystem.Core.Raft.FilePersistentStorage(dataFolder);
        var network = new DistributedSystem.Core.Raft.InMemoryRaftNetwork(10);

        var ids = new[] { "A", "B", "C" };
        var nodes = new List<DistributedSystem.Core.Raft.RaftNode>();

        foreach (var id in ids)
        {
            var peers = ids.Where(x => x != id);
            var node = new DistributedSystem.Core.Raft.RaftNode(id, peers, network, storage);
            nodes.Add(node);
            network.Register(node);
        }

        Console.WriteLine("Cluster started, waiting for leader election...");
        DistributedSystem.Core.Raft.RaftNode? leader = null;
        for (int i = 0; i < 40; i++)
        {
            leader = nodes.FirstOrDefault(n => n.State == DistributedSystem.Core.Raft.NodeState.Leader);
            if (leader != null) break;
            await Task.Delay(200);
        }

        if (leader == null)
        {
            Console.WriteLine("No leader elected");
            return;
        }

        Console.WriteLine($"Leader elected: {leader.Id}");
        Console.WriteLine("Appending a command on leader...");
        await leader.AppendCommandAsync("set x=1");

        await Task.Delay(500);

        Console.WriteLine("Logs per node:");
        foreach (var n in nodes)
        {
            Console.WriteLine($"Node {n.Id} (state={n.State})");
            foreach (var e in n.Persistent.Log)
                Console.WriteLine($"  [{e.Index}] t={e.Term} cmd={e.Command}");
        }
    }
}
