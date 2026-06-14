# CRDT

A C# implementation of Conflict-free Replicated Data Types (CRDTs) with a test harness for simulating distributed convergence, gossip-style replication, and eventual consistency.

> **Purpose:** Educational + experimental framework for understanding how modern distributed systems achieve consistency without coordination.

---

# Overview

CRDTs (Conflict-free Replicated Data Types) are data structures designed for distributed systems where nodes can:

* update state independently
* operate without coordination
* merge states deterministically
* converge automatically

This project implements core CRDT types and provides a **simulation test harness** to validate correctness under distributed conditions.

---

# Key Properties

All CRDTs in this library guarantee:

* ✔ Commutativity: A + B = B + A
* ✔ Associativity: (A + B) + C = A + (B + C)
* ✔ Idempotency: A + A = A
* ✔ Eventual consistency across replicas

---

# Architecture

```text id="crdt_readme_1"
DistributedSystem.Crdt
├── Abstractions
│   ├── ICrdt.cs
│   └── IMergeable.cs
│
├── Core
│   ├── GCounter.cs
│   ├── PNCounter.cs
│   ├── GSet.cs
│   ├── ORSet.cs
│   └── LwwRegister.cs
│
├── Models
│   ├── Timestamp.cs
│
└── TestHarness
    ├── Cluster
    ├── Network
    └── Scenarios
```

---

# Core Concepts

## 1. IMergeable

Defines the fundamental CRDT operation:

```csharp id="crdt_readme_2"
public interface IMergeable<T>
{
    void Merge(T other);
}
```

All CRDTs implement this interface to ensure deterministic convergence.

---

## 2. Timestamp

Used for conflict resolution in LWW CRDTs.

```csharp id="crdt_readme_3"
public readonly record struct Timestamp(
    long UnixTimeMilliseconds,
    string NodeId);
```

### Ordering rules:

1. Higher timestamp wins
2. If equal → lexicographic NodeId tie-break

This ensures deterministic global ordering.

---

# CRDT Implementations

## 1. G-Counter (Grow-only Counter)

Only supports increments.

### Operations:

* Increment
* Merge
* Read value

### Behavior:

Values only increase and always converge.

---

## 2. PN-Counter

Supports both increments and decrements.

Internally uses:

* G-Counter (increments)
* G-Counter (decrements)

---

## 3. G-Set (Grow-only Set)

Only supports additions.

### Properties:

* No removals allowed
* Simple merge via union

---

## 4. OR-Set (Observed-Remove Set)

Supports:

* Add
* Remove
* Merge

### Mechanism:

Each element is tagged with a unique identifier ("dot").

This allows correct remove semantics in distributed environments.

---

## 5. LWW Register (Last Write Wins)

A distributed key-value register.

### Conflict resolution:

Uses `Timestamp` ordering to determine latest write.

---

# Test Harness

The test harness simulates a distributed environment without real networking.

---

## Components

### 1. CrdtNode

Represents a single replica in the cluster.

```text id="crdt_readme_4"
Node = State + NodeId
```

---

### 2. InMemoryNetwork

Simulates:

* broadcast
* message delay
* out-of-order updates

Used to mimic gossip-style propagation.

---

### 3. CrdtCluster

Manages multiple nodes and synchronization.

---

# Execution Model

Unlike consensus systems (Paxos/Raft):

* no leader exists
* no quorum required
* updates are local
* merge ensures convergence

---

# Example: G-Counter

## Scenario

Three nodes independently update state:

```text id="crdt_readme_5"
Node A: +5
Node B: +3
Node C: +2
```

---

## After synchronization:

All nodes converge to:

```text id="crdt_readme_6"
10
```

---

# Example: OR-Set

### Operations:

```text id="crdt_readme_7"
A: add("x")
B: add("y")
A: remove("y")
```

### Result after merge:

```text id="crdt_readme_8"
{x}
```

---

# Example: LWW Register

### Conflicting writes:

```text id="crdt_readme_9"
Node A: value = "A" @ t=100
Node B: value = "B" @ t=200
```

### Result:

```text id="crdt_readme_10"
"B"
```

---

# Convergence Model

All CRDTs rely on eventual synchronization:

```text id="crdt_readme_11"
Node A ↔ Node B ↔ Node C
        ↘ merge ↙
       Converged State
```

No coordination required.

---

# CRDT vs Consensus

| Feature             | CRDT       | Paxos/Raft |
| ------------------- | ---------- | ---------- |
| Coordination        | None       | Required   |
| Consistency         | Eventual   | Strong     |
| Availability        | High       | Medium     |
| Partition tolerance | High       | Medium     |
| Complexity          | Low/Medium | High       |

---

# Test Harness Usage

## Create cluster

```csharp id="crdt_readme_12"
var cluster = new CrdtCluster<GCounter>();
```

## Add nodes

```csharp id="crdt_readme_13"
var n1 = cluster.AddNode("A", new GCounter());
var n2 = cluster.AddNode("B", new GCounter());
```

## Perform operations

```csharp id="crdt_readme_14"
n1.State.Increment("A", 5);
n2.State.Increment("B", 3);
```

## Simulate sync

```csharp id="crdt_readme_15"
cluster.SyncAll((a, b) => a.Merge(b));
```

---

# Design Principles

This library is designed around:

* Functional merge logic
* Deterministic conflict resolution
* Stateless transport simulation
* Separation of concerns
* Testable distributed behavior

---

# Limitations

This is an educational implementation.

Not included:

* real network transport
* persistence layer
* anti-entropy protocols (full gossip)
* vector clocks (optional enhancement)
* causal consistency tracking
* production-grade optimizations

---

# License

Educational use only. Not production hardened. Use at your own risk in real distributed environments.
