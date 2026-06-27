# Hash Table

A collection of hash table implementations that demonstrate how different designs trade off simplicity, memory usage, and rehashing behavior.

## Overview

This sample contains several implementations of the same core abstraction:

- SimpleHashTable: a basic open-addressing hash table
- ListPackHashTable: a bucketed approach that stores entries in lists
- HybridHashTable: a mixed strategy that combines direct storage with overflow handling
- RehashingHashTable: a version that grows and rehashes when load increases

## Key Concepts

- **Hashing**: mapping a key to a bucket index
- **Collision handling**: resolving cases where multiple keys land in the same bucket
- **Resize strategy**: increasing capacity when the table becomes too full
- **Load factor**: a measure of how full the table is

## Example Usage

```csharp
using DistributedSystem.HashTable;

var table = new SimpleHashTable();
table.Put("user-1", 101);
table.Put("user-2", 202);

Console.WriteLine(table.Get("user-1"));
Console.WriteLine(table.ContainsKey("user-2"));
```

## Run the Sample

The project is a library sample, so you typically reference it from a small console app or test harness.

```bash
dotnet build Samples/DistributedSystem.HashTable/DistributedSystem.HashTable.csproj
```

## Notes

These implementations are educational and intended to show the trade-offs behind practical hash table design rather than to be drop-in production replacements.
