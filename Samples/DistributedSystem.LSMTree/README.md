# LSM Tree

A small educational implementation of a Log-Structured Merge Tree (LSM-tree) that demonstrates how writes, flushes, SSTables, compaction, and recovery work together.

## Overview

This sample models a simplified key-value store with the following parts:

- MemTable: an in-memory sorted structure for recent writes
- CommitLog: a write-ahead log for durability
- SSTable: immutable sorted on-disk files for persistent storage
- Compaction: background merging of older SSTables to reduce read and storage overhead

## Key Concepts

### MemTable
The memtable holds recent writes in memory so new reads and writes are fast.

### Commit Log
Every write is appended to a commit log before the memtable is updated. This helps recovery after a crash.

### SSTable
When the memtable becomes full, it is flushed into an SSTable. Each SSTable stores sorted entries and metadata such as an index, summary, statistics, and a simple bloom filter.

### Compaction
When there are too many SSTables, older ones are merged into a newer table so the system stays compact and efficient.

## Example Usage

```csharp
using DistributedSystem.LSMTree;
using System.Text;

var dataDirectory = Path.Combine(Path.GetTempPath(), "lsm-demo");
using var tree = new LSMTree(dataDirectory, memTableMaxSize: 512);

tree.Add("user-1", Encoding.UTF8.GetBytes("alice"));
tree.Add("user-2", Encoding.UTF8.GetBytes("bob"));

tree.Delete("user-2");

var value = tree.Get("user-1");
Console.WriteLine(Encoding.UTF8.GetString(value ?? Array.Empty<byte>()));
```

## Build

```bash
dotnet build Samples/DistributedSystem.LSMTree/DistributedSystem.LSMTree.csproj
```

## Notes

This implementation is intentionally simple and designed for learning. A production-grade LSM-tree would add features such as more robust crash recovery, compression, snapshots, and more advanced compaction policies.
