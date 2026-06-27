# Vector Clock Conflict Resolution

A simple educational implementation of vector clocks for resolving concurrent updates in distributed systems.

## What this sample demonstrates

Vector clocks are a way to track causal relationships between events in a distributed system. Each node maintains a counter, and each update carries the current clock value.

When two replicas write concurrently, the system can detect whether the updates are:

- causally ordered,
- concurrent,
- or one is newer than the other.

## Core idea

This sample models a key-value store where each write includes a vector clock.

When a new value arrives:

1. If the clock is causally after the existing version, it replaces it.
2. If the clocks are concurrent, the implementation merges them and stores a combined value.
3. If the incoming version is older, it is ignored.

## Example usage

```csharp
var store = new VectorClockStore();

store.Put("user:42", "Alice", new Dictionary<string, int> { ["A"] = 1 });
store.Put("user:42", "Bob", new Dictionary<string, int> { ["B"] = 1 });
```

## When to use it

- Distributed caches
- Replicated data stores
- Conflict detection for concurrent writes
- Causal ordering in event-driven systems

## Trade-offs

- More informative than simple version numbers.
- Helps reason about concurrency and causality.
- Requires careful clock propagation between nodes.
