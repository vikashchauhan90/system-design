using DistributedSystem.Crdt.Core;
using DistributedSystem.Crdt.TestHarness.Cluster;
using System;
using System.Collections.Generic;
using System.Text;

namespace DistributedSystem.Crdt.TestHarness.Scenarios;

internal class CounterScenario
{
    public static async Task RunCounterScenario()
    {
        var cluster =
            new CrdtCluster<GCounter>();

        var n1 = cluster.AddNode("A", new GCounter());
        var n2 = cluster.AddNode("B", new GCounter());
        var n3 = cluster.AddNode("C", new GCounter());

        n1.State.Increment("A", 5);
        n2.State.Increment("B", 3);
        n3.State.Increment("C", 2);

        //Simulate gossip / sync
        cluster.SyncAll((a, b) => a.Merge(b));

        Console.WriteLine(n1.State.Value);
        Console.WriteLine(n2.State.Value);
        Console.WriteLine(n3.State.Value);

    }
}
