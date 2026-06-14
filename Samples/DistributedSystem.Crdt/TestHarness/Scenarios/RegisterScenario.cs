using DistributedSystem.Crdt.Models;
using DistributedSystem.Crdt.TestHarness.Cluster;
using System;
using System.Collections.Generic;
using System.Text;

namespace DistributedSystem.Crdt.TestHarness.Scenarios;

internal class RegisterScenario
{
    public static void RunRegisterScenario()
    {
        var cluster =
            new CrdtCluster<LwwRegister<string>>();

        var n1 = cluster.AddNode("A", new LwwRegister<string>());
        var n2 = cluster.AddNode("B", new LwwRegister<string>());

        n1.State.Set("A-value",
    new Timestamp(100, "A"));

        n2.State.Set("B-value",
            new Timestamp(200, "B"));

        cluster.SyncAll((a, b) => a.Merge(b));

        Console.WriteLine(n1.State.Value); // B-value
    }
}
