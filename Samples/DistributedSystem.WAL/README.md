# Write-Ahead Log (WAL)

A persistence mechanism that logs all modifications before applying them to in-memory data structures, ensuring crash recovery and durability.

## Overview

A Write-Ahead Log (WAL) is a technique used to provide ACID properties in database systems and other stateful services. By writing operations to persistent storage before applying them to in-memory state, a system can recover from crashes without data loss.

## Use Cases

- **Database Systems**: PostgreSQL, SQLite, MySQL (InnoDB) WAL for crash recovery
- **Key-Value Stores**: RocksDB, LevelDB for durability
- **Message Brokers**: Kafka's segment logs for message persistence
- **File Systems**: fsync and journaling for consistency
- **Transaction Logs**: Financial systems for audit trails
- **Replication**: Replicating state changes to replicas
- **Event Sourcing**: Immutable event logs for state reconstruction
- **Cache Systems**: Redis AOF (append-only file) for persistence

## How It Works

### Basic Flow

1. **Receive Operation**: Client requests a state change (e.g., SET key=value)
2. **Write to Log**: Append operation to persistent log file before applying
3. **Fsync**: Force log to disk via `WriteThrough` flag
4. **Apply to Memory**: Once safely logged, apply to in-memory data structures
5. **Return Success**: Confirm operation to client

### Recovery Process

1. **Crash Occurs**: System stops unexpectedly
2. **Restart System**: Read the WAL log from disk
3. **Scan Entries**: Replay entries sequentially
4. **Rebuild State**: Re-apply logged operations to in-memory state
5. **Resume Operations**: Continue from the log's last known position

### Entry Format

Each WAL entry contains:
- **Payload Length** (8 bytes): Size of the operation data
- **Sequence Number** (8 bytes): Monotonic entry identifier
- **Timestamp** (8 bytes): When the operation was logged
- **Payload** (variable): The actual operation data

## Implementation Details

```csharp
// Create a Write-Ahead Log
var directory = "./wal-data";
using var wal = new WriteAheadLog(directory);

// Append entries (automatically persisted)
wal.Append("user-123-login");
wal.Append("user-456-purchase-100");
wal.Append("user-789-logout");

// Query last sequence number
var lastSeq = wal.LastSequenceNumber;  // 3

// Recover all entries (useful after restart)
var entries = wal.Recover();
foreach (var entry in entries)
{
    Console.WriteLine($"[{entry.SequenceNumber}] {entry.PayloadText}");
}

// Truncate entries before sequence number (cleanup)
wal.Truncate(beforeSequenceNumber: 2);  // Keeps only seq >= 2
```

## Key Features

- **Durability**: Write-through fsync ensures operations survive crashes
- **Atomicity**: Entire entries are logged before application
- **Sequential**: Monotonic sequence numbers for ordering
- **Recoverable**: Full state reconstruction from log
- **Truncatable**: Safe cleanup of old entries while preserving recovery
- **Thread-Safe**: Lock-based serialization for concurrent access

## Architecture

### Components

- **WalEntry**: A single logged operation with sequence number, timestamp, and payload
- **WriteAheadLog**: Main class managing log file and recovery
- **Recovery**: Scanning log file to rebuild state
- **Truncation**: Creating new log file with only retained entries

### File Layout

```
[Header: length][Header: seqnum][Header: timestamp][Payload...]
[Header: length][Header: seqnum][Header: timestamp][Payload...]
...
```

Each entry is self-contained and can be read sequentially. Corrupt entries (partial writes) are detected and recovery stops.

## Operational Patterns

### Pattern 1: Append-on-Write
```
Client Request -> Write to WAL -> Fsync -> Apply to Memory -> ACK
```

### Pattern 2: Batch Writes
```
Collect Operations -> Batch Write to WAL -> Fsync -> Batch Apply -> ACK
```

### Pattern 3: Checkpoint + Cleanup
```
Take Snapshot -> Truncate WAL before Snapshot -> Archive Old Log
```

## Configuration Options

Currently supported:
- **Directory**: Location where WAL file is stored
- **Filename**: Name of the log file (default: "wal.log")
- **WriteThrough**: Fsync behavior (enabled for durability)

## Recovery Guarantees

- **Complete Entries**: Only fully written entries are recovered
- **Partial Tail**: Incomplete entries at end of file are safely skipped
- **No Data Loss**: All fsync'd entries are recovered
- **Ordering**: Entries recovered in exact write order

## Performance Characteristics

- **Append**: O(1) - write header + payload, then fsync
- **Recover**: O(n) - scan entire log sequentially
- **Truncate**: O(n) - copy retained entries to new file

### Optimization Strategies

1. **Batch Writes**: Group multiple operations before fsync
2. **Asynchronous Fsync**: Background thread for fsync (risk: crash between write and sync)
3. **Segmented Logs**: Multiple log files for rotation
4. **Compression**: Compress old log segments

## Limitations

- **I/O Bound**: Fsync on every write is slow; batching is essential
- **Disk Space**: Log grows indefinitely without cleanup
- **Single Writer**: Current implementation doesn't support parallel writers
- **No Compression**: Stores full operation data

## Improvements & Alternatives

- **Segmented WAL**: Multiple log files with rolling rotation
- **Asynchronous Fsync**: Background persistence thread
- **Delta Encoding**: Store only changes, not full payloads
- **Compression**: Gzip or snappy compression for old segments
- **Memory-Mapped Files**: Faster I/O for very high throughput
- **RocksDB WAL**: Production-grade implementation used by many systems

## Demo Usage

```csharp
DistributedSystem.WAL.WalDemo.RunDemo();
```

Output shows:
- Appending three WAL entries
- Recovering all entries with sequences and timestamps
- WAL file path and metadata

## Real-World Examples

- **PostgreSQL**: XLOG directory with WAL segments
- **SQLite**: WAL mode for better concurrency
- **Kafka**: Segment logs with index files
- **RocksDB**: Log files for crash recovery
- **Redis**: AOF (append-only file) persistence

## References

- [Write-Ahead Logging](https://en.wikipedia.org/wiki/Write-ahead_logging)
- [ACID Properties](https://en.wikipedia.org/wiki/ACID)
- [PostgreSQL WAL](https://www.postgresql.org/docs/current/wal-intro.html)
- [SQLite WAL Mode](https://www.sqlite.org/wal.html)
- [Designing Data-Intensive Applications](https://dataintensive.systems/) - Chapter 3 on durability
