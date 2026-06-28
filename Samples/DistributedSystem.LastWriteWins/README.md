# Last-Write-Wins (LWW) Pattern

A simple educational implementation of the **Last-Write-Wins (LWW)** conflict resolution strategy used in distributed systems.

## Overview

In distributed systems, multiple replicas of the same data may receive updates independently. If two or more replicas modify the same record before synchronizing, the system must determine which update should become the authoritative value. This situation is known as a **write conflict**.

The **Last-Write-Wins (LWW)** strategy resolves conflicts by assigning each write a version number or timestamp. When conflicting versions of the same record are compared, the update with the **highest version** (or the **most recent timestamp**) replaces the older one.

LWW is one of the simplest conflict resolution strategies and is widely used in eventually consistent databases where losing intermediate updates is an acceptable trade-off for simplicity and performance.

---

## What this sample demonstrates

This sample implements a lightweight in-memory key-value store that uses Last-Write-Wins semantics.

The implementation demonstrates:

* Maintaining version information alongside stored values.
* Resolving concurrent updates using version numbers.
* Replacing outdated values during synchronization.
* Simple conflict resolution without distributed consensus.

The goal is to illustrate the core algorithm rather than provide a production-ready implementation.

---

## How Last-Write-Wins Works

Each stored value consists of:

* **Key**
* **Value**
* **Version** (or timestamp)

When a write operation arrives:

1. If the key does not exist, insert the value.
2. If the incoming version is greater than the stored version, replace the existing value.
3. If the incoming version is lower, ignore the update.
4. If both versions are equal, use a deterministic tie-breaker (for example, the latest arrival, replica ID, or lexicographical comparison).

Example:

```
Replica A:
user:42
Version = 5
Name = Alice

Replica B:
user:42
Version = 7
Name = Alicia

Result:

user:42
Version = 7
Name = Alicia
```

The higher version wins, regardless of which replica originally created it.

---

## Example Usage

```csharp
var store = new LastWriteWinsStore();

store.Put("user:42", "Alice", 1);
store.Put("user:42", "Alicia", 2);

var current = store.Get("user:42");

// Output:
// Alicia (Version 2)
```

If an older update arrives later:

```csharp
store.Put("user:42", "Alice", 1);
```

The update is ignored because its version is older than the current version.

---

## Conflict Resolution Example

Suppose two replicas modify the same user independently.

```
Replica A
---------
user:42 = Alice
Version = 8

Replica B
---------
user:42 = Alicia
Version = 10
```

After synchronization:

```
Winner:
user:42 = Alicia
Version = 10
```

Replica B's update becomes the final state because it has the higher version.

---

## Version Numbers vs. Timestamps

This sample uses **version numbers** because they are deterministic and easy to understand.

Production systems may instead use:

* Physical timestamps
* Hybrid Logical Clocks (HLC)
* Lamport clocks
* Logical sequence numbers

The conflict resolution rule remains the same: **the newer version wins**.

---

## Time Complexity

| Operation | Complexity |
| --------- | ---------: |
| Put       |       O(1) |
| Get       |       O(1) |
| Update    |       O(1) |

Space complexity is **O(n)**, where *n* is the number of stored keys.

---

## Advantages

* Very easy to implement.
* Constant-time conflict resolution.
* No merge logic required.
* Works well for eventually consistent systems.
* Suitable for high-throughput distributed key-value stores.

---

## Limitations

LWW assumes that the newest update should always replace older ones.

This can lead to **lost updates** when two clients modify different parts of the same logical object concurrently.

Example:

```
Client A:
Change email

Client B:
Change phone number

↓

Last write wins

↓

One change may be discarded.
```

For applications that require preserving all concurrent changes, algorithms such as **Vector Clocks**, **CRDTs**, or **Operational Transformation (OT)** provide richer conflict resolution.

---

## When to Use Last-Write-Wins

LWW is a good choice when:

* Conflicts are rare.
* Overwriting older values is acceptable.
* Simplicity is preferred over complex merge logic.
* High availability and eventual consistency are priorities.
* Data behaves like a simple register (one value per key).

Typical use cases include:

* Distributed caches
* Replicated key-value stores
* User preferences
* Session data
* Metadata services
* Configuration storage

---

## When Not to Use It

Avoid LWW when:

* Every concurrent update must be preserved.
* Business rules require merging changes.
* Financial or transactional data must never be overwritten.
* Collaborative editing is required.
* Data consistency is more important than availability.

In these scenarios, consider using:

* Vector Clocks
* Version Vectors
* CRDTs
* Operational Transformation (OT)
* Consensus algorithms such as Raft or Paxos

---

## Related Patterns

| Pattern                    | Purpose                                       |
| -------------------------- | --------------------------------------------- |
| Lamport Clock              | Orders distributed events                     |
| Vector Clock               | Detects causality and concurrent updates      |
| Version Vector             | Replica-aware version tracking                |
| Hybrid Logical Clock       | Combines physical and logical time            |
| CRDT                       | Automatically merges concurrent updates       |
| Operational Transformation | Resolves concurrent document edits            |
| Raft / Paxos               | Achieves strong consistency through consensus |

---

## Summary

Last-Write-Wins is one of the simplest conflict resolution strategies in distributed systems. By associating each update with a version or timestamp, replicas can independently determine the authoritative value without coordination. While this approach is efficient and widely used in eventually consistent systems, it may discard valid concurrent updates, making it unsuitable for applications that require semantic merging or strong consistency.
