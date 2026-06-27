# Priority Queue

A simple educational implementation of a priority queue using a binary heap.

## What this sample demonstrates

A priority queue stores items with an associated priority and always exposes the item with the highest priority first. It is commonly used in scheduling, graph algorithms, and event simulations.

## Core idea

This implementation uses a binary heap:

- Enqueue inserts an item and bubbles it up to the correct spot.
- Dequeue removes the item with the smallest priority and bubbles down the replacement.
- Peek returns the next item without removing it.

## Example usage

```csharp
var queue = new PriorityQueue<string>();
queue.Enqueue("low", 3);
queue.Enqueue("high", 1);
queue.Enqueue("medium", 2);

var next = queue.Dequeue();
```

## When to use it

- Task scheduling
- Dijkstra's algorithm
- Best-first search
- Event-driven simulation

## Trade-offs

- Efficient for insertion and removal with $O(log n)$ complexity.
- Not ideal for random access by index.
- The implementation here is intentionally simple and easy to read.
