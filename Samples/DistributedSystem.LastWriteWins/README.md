# Last-Write-Wins Pattern

A simple educational implementation of the Last-Write-Wins conflict resolution strategy.

## What this sample demonstrates

Last-Write-Wins is a common conflict resolution strategy used in distributed systems and eventually consistent stores. When two replicas update the same record concurrently, the write with the highest version or latest timestamp wins.

## Core idea

This sample models a key-value store where each update includes a version number. When a new value arrives:

1. If the key does not exist, it is inserted.
2. If the key exists, the update with the higher version replaces the older value.
3. If versions are equal, the newer write is accepted.

## Example usage

```csharp
var store = new LastWriteWinsStore();

store.Put("user:42", "Alice", 1);
store.Put("user:42", "Alicia", 2);

var current = store.Get("user:42");
```

## When to use it

- Caching layers
- Replicated key-value stores
- Eventual consistency systems
- Simple conflict resolution where “newer” is acceptable

## Trade-offs

- Easy to implement and reason about.
- Can silently overwrite newer logical changes if timestamps are not carefully assigned.
- Not suitable when business semantics require richer merge logic.
