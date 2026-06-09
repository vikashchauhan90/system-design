# DistributedSystem.Core (Raft)

This project contains a minimal, self-contained Raft algorithm core suitable for experimentation and unit testing. It intentionally omits persistence and many robustness details; use it as a starting point.

Build:

```powershell
dotnet build Samples/raft/DistributedSystem.Core/DistributedSystem.Core.csproj
```

Next steps:
- Add an `IRaftNetwork` test implementation to run nodes in-memory.
- Add durable persistence for `PersistentState`.
- Implement AppendEntries leader heartbeat loop and log replication.

Demo (in-memory):

You can use the included test harness `InMemoryCluster.DemoAsync()` to run a small 3-node cluster using file persistence and the in-memory network.

Example (from a console app or script):

```csharp
await DistributedSystem.Core.Raft.TestHarness.InMemoryCluster.DemoAsync();
```

Data files will be written to a `raft-data` folder under the current working directory by the default `FilePersistentStorage` used in the demo.
