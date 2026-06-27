# Lamport Clock

A simple educational implementation of Lamport clocks for ordering events in distributed systems.

## What this sample demonstrates

Lamport clocks provide a single monotonic counter that can order events across distributed processes. They do not capture full causal history like vector clocks, but they are much simpler to implement.

## Core idea

Each event increments the local clock. When a message is received from another process, the local clock is updated to the maximum of the two values, then incremented.

## Example usage

```csharp
var clock = new LamportClock();
var first = clock.Tick();
var second = clock.Tick();
var merged = clock.Merge(7);
```

## When to use it

- Simple event ordering
- Basic distributed tracing
- Lightweight causal ordering where a full vector clock is unnecessary

## Trade-offs

- Easier than vector clocks.
- Cannot tell whether two events are concurrent.
- Best suited for simple ordering rather than full conflict analysis.
