# AppendLog

This folder contains a simplified append-only log system inspired by Kafka's storage layer.

Key components:
- `LogSegment` — manages physical segment files and lifecycle operations.
- `OffsetIndex` — a sparse offset-to-position index for fast seek.
- `TimeIndex` — a sparse timestamp-to-offset index for time-based fetches.
- `TransactionIndex` — a transaction metadata index for transactional append support.

The append-only model keeps data immutable once written, with truncation only allowed at safe offset boundaries.

## Usage

Create a `LogSegment` and append `RecordBatch` objects. The segment maintains sparse indexes and can roll when size or time thresholds are reached.

## Files

- `LogSegment.cs`
- `OffsetIndex.cs`
- `TimeIndex.cs`
- `TransactionIndex.cs`
- `RecordBatch.cs`
- `IndexEntry.cs`

---

## Concepts

- **Append-only**: data is appended sequentially to segment files.
- **Sparse index**: only a subset of offsets and timestamps are indexed.
- **Segment roll**: a new segment begins when the current one is too large or too old.
- **Truncation**: segments can be truncated safely to an offset boundary during recovery.
