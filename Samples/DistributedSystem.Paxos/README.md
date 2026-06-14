# Paxos

A lightweight educational implementation of the Paxos consensus algorithm in C# with support for leader election, quorum-based voting, durable acceptor state, and a test harness for cluster simulation.

> **Status:** Early implementation focused on Paxos leader election and core protocol concepts. Multi-Paxos log replication, persistence providers, and advanced failover scenarios can be added incrementally.

---

# Overview

Paxos is a distributed consensus algorithm that enables a cluster of nodes to agree on a value even in the presence of node failures and network interruptions.

This project provides:

* Paxos leader election
* Prepare / Promise phase
* Accept / Accepted phase
* Quorum calculation
* Durable acceptor state abstraction
* In-memory state store
* In-memory transport for testing
* Cluster simulation harness

---

# Solution Structure

```text
DistributedSystem.Paxos
├── Abstractions
│   ├── IMessageBus.cs
│   ├── IPaxosNode.cs
│   └── IStateStore.cs
│
├── Core
│   ├── Acceptor.cs
│   └── Proposer.cs
│
├── Messages
│   ├── PrepareRequest.cs
│   ├── PrepareResponse.cs
│   ├── AcceptRequest.cs
│   ├── AcceptResponse.cs
│   └── Heartbeat.cs
│
├── Models
│   ├── BallotNumber.cs
│   ├── NodeState.cs
│   └── PaxosState.cs
│
├── Storage
│   └── InMemoryStateStore.cs
│
└── PaxosNode.cs
```

---

# Paxos Terminology

## Proposer

A proposer attempts to convince the cluster to accept a value.

Example:

```text
Node-1 wants to become leader
```

The proposer initiates:

1. Prepare phase
2. Accept phase

---

## Acceptor

An acceptor votes on proposals.

Acceptors maintain persistent state:

```text
Highest Promised Ballot
Highest Accepted Ballot
Accepted Value
```

This state must survive restarts.

---

## Learner

A learner discovers which value has been accepted by a quorum.

In this initial implementation, learner responsibilities are simplified and embedded within the election flow.

---

# Ballot Numbers

Each proposal is identified by a globally ordered ballot.

Example:

```text
(1, node-1)
(2, node-2)
(3, node-1)
```

The tuple:

```text
(Number, NodeId)
```

ensures deterministic ordering.

Examples:

```text
(5,node-1) > (4,node-3)
(5,node-2) > (5,node-1)
```

---

# Leader Election Flow

## Step 1: Prepare Phase

Candidate sends:

```text
Prepare(ballot=10)
```

to all replicas.

Example:

```text
Node-1
   │
   ├── Prepare(10) ──► Node-2
   ├── Prepare(10) ──► Node-3
   ├── Prepare(10) ──► Node-4
   └── Prepare(10) ──► Node-5
```

---

## Step 2: Promise Phase

Each replica responds:

```text
Promise
```

if it has not already promised a higher ballot.

Example:

```text
Node-2 -> Promise(10)
Node-3 -> Promise(10)
Node-4 -> Promise(10)
Node-5 -> Promise(10)
```

---

## Step 3: Quorum Validation

The proposer counts promises.

For a 5-node cluster:

```text
2/3 quorum = 4 votes
```

Election proceeds only if enough votes are received.

---

## Step 4: Accept Phase

Candidate sends:

```text
Accept(ballot=10, value="Leader:Node-1")
```

to all replicas.

---

## Step 5: Accepted Responses

Replicas acknowledge:

```text
Accepted(ballot=10)
```

Once quorum is reached:

```text
Node-1 becomes leader
```

---

# Quorum Rules

This implementation uses:

```text
≥ 2/3 replicas
```

Examples:

| Replicas | Required Votes |
| -------- | -------------- |
| 3        | 2              |
| 5        | 4              |
| 6        | 4              |
| 9        | 6              |

Quorum is calculated using:

```csharp
(int)Math.Ceiling(replicas * 2d / 3d)
```

---

# Persistent State

Paxos safety depends on durable acceptor state.

The following information is persisted:

```text
Promised Ballot
Accepted Ballot
Accepted Value
```

State is represented by:

```csharp
public sealed record PaxosState(
    BallotNumber PromisedBallot,
    BallotNumber? AcceptedBallot,
    string? AcceptedValue);
```

---

# State Store

Persistence is abstracted using:

```csharp
public interface IStateStore
{
    Task<PaxosState> LoadAsync(
        CancellationToken cancellationToken);

    Task SaveAsync(
        PaxosState state,
        CancellationToken cancellationToken);
}
```

Benefits:

* File storage
* SQLite
* RocksDB
* Azure Storage
* Amazon DynamoDB
* Custom persistence providers

can be added without changing Paxos logic.

---

# In-Memory State Store

For testing:

```csharp
public sealed class InMemoryStateStore : IStateStore
{
}
```

This implementation stores Paxos state entirely in memory and is not durable across process restarts.

---

# Message Transport

Network communication is abstracted through:

```csharp
public interface IMessageBus
{
    Task<IReadOnlyCollection<PrepareResponse>>
        SendPrepareAsync(...);

    Task<IReadOnlyCollection<AcceptResponse>>
        SendAcceptAsync(...);

    Task BroadcastHeartbeatAsync(...);
}
```

Possible implementations:

* In-memory
* TCP
* HTTP
* gRPC
* Azure Service Bus
* RabbitMQ
* Kafka

---

# Test Harness

The project includes an in-memory cluster simulator.

Components:

```text
PaxosCluster
InMemoryMessageBus
PaxosNode
```

This allows local testing without networking infrastructure.

---

# Example

Create a cluster:

```csharp
var cluster = new PaxosCluster(5);
```

Create a proposer:

```csharp
var proposer =
    new Proposer(
        cluster.MessageBus,
        cluster.Nodes.Count);
```

Run election:

```csharp
var elected =
    await proposer.ElectLeaderAsync(
        "node-1",
        cluster.NextBallot("node-1", 1),
        CancellationToken.None);
```

Output:

```text
Leader elected: True
```

---

# Failure Recovery

When a leader fails:

1. Heartbeats stop
2. Followers detect timeout
3. New candidate starts Prepare phase
4. Quorum grants promises
5. Candidate becomes leader

Example:

```text
Leader node-1 fails
↓
Node-2 starts election
↓
Node-2 receives quorum
↓
Node-2 becomes leader
```

---

# Current Limitations

Current implementation focuses on election and acceptance.

Not yet implemented:

* Multi-Paxos log replication
* Persistent disk storage
* Snapshotting
* Cluster membership changes
* Log compaction
* Leader lease optimization
* Read-index support
* Network partition simulation
* Catch-up replication

---

# Future Roadmap

## Phase 1

* Basic Paxos election
* Durable acceptor state
* In-memory transport

## Phase 2

* Multi-Paxos
* Log replication
* Commit index

## Phase 3

* Snapshot support
* State machine replication
* Recovery from crashes

## Phase 4

* gRPC transport
* Production persistence providers
* Metrics and monitoring

---

# References

* Leslie Lamport, "Paxos Made Simple"
* Paxos Made Moderately Complex
* Multi-Paxos Protocol
* Distributed Systems: Principles and Paradigms

---

# License

This project is intended for educational and experimental use. Review and harden the implementation before deploying in production environments.
