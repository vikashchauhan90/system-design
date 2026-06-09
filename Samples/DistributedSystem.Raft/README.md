# DistributedSystem (Raft)

This project contains a minimal, self-contained Raft algorithm core suitable for experimentation and unit testing. It intentionally omits many production features; use it as a starting point to understand core Raft concepts.

## Overview

Raft is a consensus algorithm built around three main concepts:

- **Leader election** using a monotonically increasing `term`.
- **Log replication** to keep commands consistent across nodes.
- **Safety** by requiring a majority before committing entries.

This sample stores a copy of each node's state locally, including its current term, vote, and the replicated log. That local copy is what helps nodes decide leader elections and maintain consistency.

## What the log contains

The Raft log in this implementation is a sequence of `LogEntry` records. Each entry contains:

- `Index` — the position in the log.
- `Term` — the term when the entry was created.
- `Command` — an application command string.

The `Command` is not node state. It is the actual operation or data that the replicated state machine should apply.

For example, if Raft is used to replicate a key-value store, a command might be `set x=1` or `delete y`.

The node state is separate and includes:

- `PersistentState.CurrentTerm` — the Raft election term.
- `PersistentState.VotedFor` — which candidate this node voted for.
- `RaftNode.State` — the current role: follower, candidate, or leader.

### Important: the log does not store "leader status"

The log is not a status store for the leader. It stores commands or operations that should eventually be applied to a replicated state machine.

Leader status is separate:

- `PersistentState.CurrentTerm` tracks the current Raft term.
- `PersistentState.VotedFor` tracks which candidate this node voted for in the current term.
- `RaftNode.State` is the node's runtime role: `Follower`, `Candidate`, or `Leader`.

Those values are used to elect a leader and to determine whether the node should accept or reject requests.

## How leader election works in this sample

Each node starts as a follower and waits for a heartbeat or election timeout.

- If a follower sees no leader heartbeat before its timeout, it becomes a candidate.
- The candidate increments `CurrentTerm` and requests votes from peers.
- If the candidate receives a majority of votes, it becomes the leader.
- The leader starts sending periodic heartbeats via `AppendEntries` RPCs.

The sample uses `CurrentTerm` as Raft's logical time. In Raft, there is no separate `epoch` value; `term` is the equivalent concept.

## How log replication works

Once a leader is elected:

1. The leader appends new commands to its local log.
2. It sends `AppendEntries` RPCs to followers, including the log entries and the previous log index/term.
3. Followers verify the previous entry matches, append new entries, and update their commit index.
4. When a majority has replicated an entry, the leader can consider it committed.

This sample implements a simplified replication flow:

- Followers accept entries only if the previous index and term match.
- Leaders maintain `NextIndex` and `MatchIndex` for each follower.
- A heartbeat loop periodically reprobes followers and retries replication.

## Persistence

This implementation includes durable persistence through `IPersistentStorage`:

- `FilePersistentStorage` writes node state to disk as JSON.
- Each node loads its persisted state at startup.
- `CurrentTerm`, `VotedFor`, and the log are persisted.

That means a restarted node can resume its term and log history rather than starting from scratch.

## Running the demo

Build the project:

```powershell
dotnet build Samples/raft/DistributedSystem.Raft/DistributedSystem.Raft.csproj
```

The demo harness is `InMemoryCluster.DemoAsync()`:

```csharp
await DistributedSystem.Raft.TestHarness.InMemoryCluster.DemoAsync();
```

It starts a simple 3-node cluster using the in-memory network and file persistence. The demo writes state to a `raft-data` folder in the current working directory.

## Notes

- This sample is intentionally simplified. It is suitable for learning rather than production use.
- A real Raft implementation must handle more edge cases such as leader election timeouts, log compaction, snapshotting, and robust conflict resolution.
