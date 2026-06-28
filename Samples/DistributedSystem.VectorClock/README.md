# Vector Clock Conflict Resolution

A simple educational implementation of **Vector Clocks** for detecting and resolving concurrent updates in distributed systems.

## Overview

In distributed systems, multiple replicas can update the same piece of data independently. Without a shared global clock, it is difficult to determine whether one update happened before another or whether they occurred concurrently.

**Vector Clocks** solve this problem by tracking the logical history of updates across all participating nodes. Unlike Lamport clocks, which provide only a total ordering of events, vector clocks can determine whether:

* one update happened before another,
* one update is newer than another,
* or two updates occurred concurrently.

This ability makes vector clocks one of the foundational techniques for conflict detection in eventually consistent distributed databases.

---

## What this sample demonstrates

This sample implements a simple in-memory key-value store where every stored value is associated with a vector clock.

The implementation demonstrates how to:

* Maintain vector clocks for multiple replicas.
* Compare two versions of the same object.
* Detect causal relationships between updates.
* Identify concurrent writes.
* Resolve conflicts by merging concurrent versions.

The objective is to illustrate the algorithm rather than provide a production-ready distributed database.

---

## Understanding Vector Clocks

Each participating node maintains its own logical counter.

Instead of storing a single integer like a Lamport clock, every update carries a **vector** containing one counter per replica.

Example:

```text
Replica A : 5
Replica B : 2
Replica C : 4
```

is represented as:

```text
{
    A: 5,
    B: 2,
    C: 4
}
```

Whenever a replica performs a local write, it increments **its own** counter while leaving all other counters unchanged.

---

## How the Algorithm Works

### 1. Local Update

Replica **A** performs a write.

Before:

```text
{A:2, B:1}
```

After:

```text
{A:3, B:1}
```

Only Replica A's counter increases.

---

### 2. Sending Updates

When a value is replicated, its vector clock travels with the data.

Example:

```text
Value:
Alice

Clock:
{A:3, B:1}
```

---

### 3. Comparing Versions

When another replica receives an update, the vector clocks are compared.

There are three possible outcomes.

---

### Case 1 — Incoming Version is Newer

Existing:

```text
{A:2, B:1}
```

Incoming:

```text
{A:3, B:1}
```

Since every component is greater than or equal, and at least one is greater, the incoming version **causally succeeds** the existing version.

Result:

```text
Replace existing value.
```

---

### Case 2 — Incoming Version is Older

Existing:

```text
{A:4, B:2}
```

Incoming:

```text
{A:3, B:2}
```

The incoming version represents an older history.

Result:

```text
Ignore the update.
```

---

### Case 3 — Concurrent Updates

Replica A updates independently:

```text
{A:2, B:1}
```

Replica B updates independently:

```text
{A:1, B:2}
```

Neither vector dominates the other.

Result:

```text
Concurrent conflict
```

The system cannot determine which update is newer because neither update has observed the other.

---

## Conflict Resolution

This sample resolves concurrent updates by merging both values into a combined representation.

Example:

```text
Existing:

Alice
Clock:
{A:2,B:1}

Incoming:

Bob
Clock:
{A:1,B:2}
```

Merged result:

```text
Alice | Bob

Clock:
{A:2,B:2}
```

The merged clock is computed by taking the component-wise maximum:

```text
max(2,1) = 2
max(1,2) = 2
```

Result:

```text
{A:2,B:2}
```

Production systems may instead:

* Keep multiple sibling versions.
* Invoke an application-specific merge function.
* Use CRDTs to merge automatically.
* Ask clients to resolve the conflict.

---

## Example Usage

```csharp
var store = new VectorClockStore();

store.Put(
    "user:42",
    "Alice",
    new Dictionary<string, int>
    {
        ["A"] = 1
    });

store.Put(
    "user:42",
    "Bob",
    new Dictionary<string, int>
    {
        ["B"] = 1
    });

var current = store.Get("user:42");
```

Since both updates were made independently, the implementation detects a concurrent write and merges the values.

---

## Example Timeline

```text
Replica A

Write Alice

Clock
{A:1,B:0}

---------------------------->

Replica B

Write Bob

Clock
{A:0,B:1}
```

Later both replicas synchronize.

Comparison:

```text
{A:1,B:0}

vs

{A:0,B:1}
```

Neither vector is greater.

Result:

```text
Concurrent
```

The merge strategy is executed.

---

## Vector Clock Comparison

Given two vector clocks:

```text
V1

{A:3,B:1,C:2}

V2

{A:3,B:2,C:2}
```

Since every component of **V1** is less than or equal to **V2**, and one component is strictly greater in **V2**, we conclude:

```text
V1 happened before V2
```

Now consider:

```text
V1

{A:4,B:1}

V2

{A:3,B:2}
```

Neither vector is greater than the other.

Result:

```text
Concurrent updates
```

---

## Time Complexity

Assuming **N** replicas:

| Operation             | Complexity |
| --------------------- | ---------: |
| Read                  |       O(1) |
| Write                 |       O(1) |
| Compare Vector Clocks |       O(N) |
| Merge Vector Clocks   |       O(N) |

Space complexity is **O(N)** per stored version because each replica contributes one counter.

---

## Advantages

* Detects true causal relationships.
* Identifies concurrent updates.
* Preserves distributed history.
* No synchronized physical clocks required.
* Well suited for eventually consistent systems.
* Enables application-specific conflict resolution.

---

## Limitations

Vector clocks grow with the number of replicas.

For a system with many nodes:

```text
Replica A
Replica B
Replica C
...
Replica N
```

each stored version must contain one counter per replica.

This increases:

* Memory usage
* Network bandwidth
* Comparison cost

Large-scale systems often replace vector clocks with optimized alternatives such as **Version Vectors**, **Dotted Version Vectors**, or **Hybrid Logical Clocks**.

---

## Lamport Clock vs. Vector Clock

| Feature                         | Lamport Clock | Vector Clock |
| ------------------------------- | ------------- | ------------ |
| Logical ordering                | Yes           | Yes          |
| Detects causality               | Yes           | Yes          |
| Detects concurrent events       | No            | Yes          |
| Memory usage                    | O(1)          | O(N)         |
| Suitable for conflict detection | Limited       | Excellent    |

Lamport clocks answer:

> "Which event should come first?"

Vector clocks answer:

> "Did one event happen before the other, or did they occur independently?"

---

## When to Use Vector Clocks

Vector clocks are a good choice when:

* Multiple replicas update the same data.
* Concurrent writes must be detected.
* Event ordering is important.
* Application-specific merge logic is required.
* Eventual consistency is acceptable.

Typical use cases include:

* Replicated key-value stores
* Distributed databases
* Peer-to-peer systems
* Distributed caches
* Event sourcing
* Offline-first applications
* Conflict-aware synchronization

---

## When Not to Use Them

Vector clocks are less suitable when:

* There are hundreds or thousands of replicas.
* Memory efficiency is critical.
* Strong consistency is required.
* A total ordering of operations is sufficient.
* Conflicts should be prevented instead of detected.

Alternative approaches include:

* Last-Write-Wins (LWW)
* Version Vectors
* Dotted Version Vectors
* Hybrid Logical Clocks (HLC)
* CRDTs
* Raft or Paxos for strong consistency

---

## Summary

Vector clocks extend logical clocks by recording one counter per replica, allowing distributed systems to determine not only the order of events but also whether updates occurred concurrently. This capability makes them a powerful mechanism for conflict detection in eventually consistent systems. While they require more storage and communication than Lamport clocks, they provide significantly richer information about causality, enabling intelligent conflict resolution strategies such as merging, sibling versions, or application-specific reconciliation.
