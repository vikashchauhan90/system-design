using DistributedSystem.Crdt.Core;
using DistributedSystem.Crdt.TestHarness.Cluster;
using System;
using System.Collections.Generic;
using System.Text;

namespace DistributedSystem.Crdt.TestHarness.Scenarios;

internal class SetScenario
{
    public static void RunSetScenario()
    {
        var cluster =
            new CrdtCluster<ORSet<string>>();

        var n1 = cluster.AddNode("A", new ORSet<string>());
        var n2 = cluster.AddNode("B", new ORSet<string>());

        n1.State.Add("x");
        n2.State.Add("y");

        n1.State.Remove("y");

        cluster.SyncAll((a, b) => a.Merge(b));

        Console.WriteLine(string.Join(",", n1.State.Value));
    }
}
