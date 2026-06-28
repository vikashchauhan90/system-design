# Lamport Clock

A simple educational implementation of **Lamport clocks**, one of the fundamental logical clock algorithms used to order events in distributed systems.

## Overview

In a distributed system, there is no single global clock that all machines can trust. Each process has its own local clock, and network latency makes physical timestamps unreliable for determining the order of events.

Lamport clocks solve this problem by assigning every event a **logical timestamp**. These timestamps establish a consistent ordering of events across multiple processes while respecting causality.

Lamport clocks do **not** measure real time. Instead, they provide a logical notion of "happened before" that allows distributed systems to reason about event ordering without synchronized clocks.

---

## What this sample demonstrates

This sample implements a simple Lamport clock that supports:

* Generating logical timestamps for local events.
* Updating the local clock when messages are received.
* Maintaining a monotonically increasing logical clock.
* Preserving causal ordering between distributed events.

The goal is to demonstrate the algorithm rather than build a complete distributed messaging system.

---

## How Lamport Clocks Work

Each process maintains a single integer counter.

The algorithm follows three simple rules:

### 1. Local Event

Before every local event, increment the clock.

```text
clock = clock + 1
```

---

### 2. Sending a Message

Before sending a message:

1. Increment the clock.
2. Attach the current clock value to the outgoing message.

```text
clock = clock + 1

Send(message, timestamp = clock)
```

---

### 3. Receiving a Message

When a message arrives:

```text
clock = max(localClock, receivedClock) + 1
```

This guarantees that the receiving event is always ordered after the sending event.

---

## Example

Suppose two processes communicate.

```text
Process A                 Process B
---------                 ---------

Clock = 0                 Clock = 0

Tick → 1

Send(M,1)
        -------------------->

                      max(0,1)+1 = 2

                      Receive M

                      Tick → 3
```

Final logical clocks:

```text
Process A = 1

Process B = 3
```

Although these numbers are not timestamps in real time, they preserve the causal relationship:

```text
Send(M)
    happened before
Receive(M)
```

---

## Example Usage

```csharp
var clock = new LamportClock();

// Local events
var first = clock.Tick();     // 1
var second = clock.Tick();    // 2

// Receive a message carrying timestamp 7
var merged = clock.Merge(7);  // 8
```

After calling `Merge(7)`, the local clock becomes:

```text
max(2,7) + 1 = 8
```

Any future events continue from that value.

---

## Ordering Example

Consider three events.

```text
Event A : Clock = 2

Event B : Clock = 5

Event C : Clock = 9
```

The Lamport timestamps indicate:

```text
A happened before B

B happened before C
```

However, two events with timestamps:

```text
Process 1 : 6

Process 2 : 6
```

are **not necessarily concurrent**, nor does the algorithm indicate which occurred first. Lamport clocks provide a consistent ordering but cannot detect whether events were truly independent.

---

## Time Complexity

| Operation  | Complexity |
| ---------- | ---------: |
| Tick       |       O(1) |
| Merge      |       O(1) |
| Read Clock |       O(1) |

Space complexity is **O(1)** since each process stores only a single integer counter.

---

## Advantages

* Extremely simple to implement.
* Constant memory usage.
* Efficient timestamp generation.
* Preserves causal ordering.
* Does not require synchronized physical clocks.
* Minimal communication overhead.

---

## Limitations

Lamport clocks cannot determine whether two events occurred concurrently.

For example:

```text
Process A
Clock = 5

Process B
Clock = 8
```

The timestamps alone do **not** imply that the event with timestamp 5 happened before the event with timestamp 8. The difference may simply reflect that the processes executed independently.

As a result, Lamport clocks cannot detect conflicting concurrent updates.

To determine true causal relationships between independent events, algorithms such as **Vector Clocks** or **Version Vectors** are required.

---

## Lamport Clock vs. Physical Time

A Lamport timestamp is **not** a wall-clock timestamp.

For example:

```text
Physical Time

10:00:01
10:00:02
10:00:03
```

is unrelated to:

```text
Lamport Time

1
7
13
```

Logical timestamps represent event ordering, not elapsed time.

---

## When to Use Lamport Clocks

Lamport clocks are a good choice when:

* You need a consistent ordering of distributed events.
* Physical clocks cannot be trusted.
* Memory usage should remain minimal.
* Detecting concurrent updates is not required.
* The system only needs to preserve causal ordering.

Common use cases include:

* Distributed logging
* Event sequencing
* Message ordering
* Distributed tracing
* Basic distributed coordination
* Educational implementations of logical clocks

---

## When Not to Use Them

Lamport clocks are not sufficient when:

* Concurrent updates must be detected.
* Multiple replicas modify the same object independently.
* Conflict resolution depends on causality.
* Merge algorithms require knowledge of independent events.

In these cases, consider using:

* Vector Clocks
* Version Vectors
* Dotted Version Vectors
* Hybrid Logical Clocks (HLC)
* CRDTs

---

## Comparison with Other Clock Algorithms

| Algorithm            | Detects Causality | Detects Concurrent Events | Memory |
| -------------------- | ----------------- | ------------------------- | -----: |
| Lamport Clock        | Yes               | No                        |   O(1) |
| Vector Clock         | Yes               | Yes                       |   O(N) |
| Version Vector       | Yes               | Yes                       |   O(R) |
| Hybrid Logical Clock | Partial           | Limited                   |   O(1) |

---

## Summary

Lamport clocks provide a lightweight mechanism for ordering events in distributed systems using logical timestamps instead of physical time. By maintaining a single monotonically increasing counter per process and exchanging timestamps with messages, they preserve causal ordering with minimal overhead. Their simplicity makes them ideal for event ordering and distributed tracing, but they cannot distinguish concurrent events, making more expressive algorithms such as Vector Clocks necessary for advanced conflict detection and resolution.
