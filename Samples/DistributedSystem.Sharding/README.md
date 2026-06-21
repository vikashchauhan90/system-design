# AppendLog

This folder contains a simplified append-only log system inspired by Kafka's storage layer.

Key components:
- `LogSegment` — manages physical segment files and lifecycle operations.
- `OffsetIndex` — a sparse offset-to-position index for fast seek.
- `TimeIndex` — a sparse timestamp-to-offset index for time-based fetches.
- `TransactionIndex` — a transaction metadata index for transactional append support.

The append-only model keeps data immutable once written, with truncation only allowed at safe offset boundaries.

## Usage

Create a `LogSegment` and append `RecordBatch` objects. The segment writes binary log data to a `*.log` file and maintains sparse index files to support fast seek.

The system uses a configurable index interval (default 4KB) so that every time the segment advances by that threshold, a new offset index and time index entry is created.

## Demo harness

A sample harness is available in `TestHarness/AppendLogDemo.cs`. It demonstrates:

- appending batches of binary records
- index entry creation every `IndexIntervalBytes`
- searching by offset using the offset index plus linear scan
- reporting index summary metrics

Example usage:

```csharp
DistributedSystem.AppendLog.TestHarness.AppendLogDemo.RunDemo();
```

## Files

- `LogSegment.cs`
- `OffsetIndex.cs`
- `TimeIndex.cs`
- `TransactionIndex.cs`
- `RecordBatch.cs`
- `IndexEntry.cs`

---

## Concepts

- **Append-only**: data is appended sequentially to binary segment files.
- **Sparse index**: only a subset of offsets and timestamps are indexed to keep the index compact.
- **Index interval**: after roughly 4KB of new log bytes, the segment writes a new offset index and time index entry.
- **Segment roll**: a new segment begins when the current one exceeds size or time limits.
- **Search flow**: the offset index provides the closest seek position, and a linear scan from that position locates the exact batch for a requested offset.
- **Truncation**: segments can be truncated safely to an offset boundary during recovery.

## Configuration

`AppendLogConfig` controls segment behavior:

- `IndexIntervalBytes` — bytes between sparse index entries (default `4096`).
- `MaxSegmentBytes` — when the segment is considered full and should roll.
- `MaxSegmentAge` — older segments may be rotated or cleaned.
- `OffsetIndexInterval` — fallback offset sparsity when loading existing indexes.
- `TimeIndexMaxEntries` — maximum entries in the time index.
- `TimeIndexSparseInterval` — time spacing for time-based sparse entries.

## Search and retrieval

When the log is searched by offset, the segment uses the offset index to find the closest indexed offset and its physical file position. From that position, the implementation performs a linear scan over batch headers until it finds the batch containing the requested offset.

This combines the speed of the sparse index with the accuracy of sequential batch scanning.
