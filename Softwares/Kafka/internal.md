# Kafka Raft & Storage Implementation Overview

This document describes the core Raft implementation and the related log storage architecture found in the Kafka source tree.

## Overview

Apache Kafka's KRaft mode uses an internal Raft-based metadata log to manage controller state and cluster metadata without relying on ZooKeeper.

Key implementation areas:
- `raft/` for Raft protocol and quorum persistence
- `metadata/` for controller coordination and metadata state machine integration
- `storage/` for low-level append-only log segment storage, sparse indexes, and file-based persistence

## Raft implementation (`raft/`)

### `KafkaRaftClient`
Location: `raft/src/main/java/org/apache/kafka/raft/KafkaRaftClient.java`

`KafkaRaftClient` is the Kafka-specific Raft client entrypoint. It implements the Raft client APIs used by the metadata controller and brokers, including:
- leader election and epoch tracking
- append/fetch semantics
- fetch from leader and follower replication
- snapshot handling
- local quorum state persistence

It is the bridge between Raft consensus and Kafka's broker networking and request processing.

### `KafkaRaftLog`
Location: `raft/src/main/java/org/apache/kafka/raft/internals/KafkaRaftLog.java`

`KafkaRaftLog` implements `RaftLog` on top of Kafka's `UnifiedLog`. It adapts Kafka's standard log storage to Raft semantics by exposing:
- `read(startOffset, isolation, maxTotalBatchBytes)` for committed or uncommitted log reads
- `appendAsLeader(records, epoch)` for leader appends
- `appendAsFollower(records, epoch)` for follower log replication
- `truncateTo(offset)` to roll back uncommitted entries safely
- `highWatermark()` and `updateHighWatermark()` for Raft commit tracking
- snapshot creation and loading support that is required for state machine catch-up

This adapter is critical because it allows Raft to reuse Kafka's existing append-only log internals while preserving Raft's offset, epoch, and snapshot semantics.

### `FileQuorumStateStore`
Location: `raft/src/main/java/org/apache/kafka/raft/FileQuorumStateStore.java`

The local election/quorum state is persisted to disk in JSON format. This includes:
- current term / epoch
- voted-for information
- quorum membership metadata

`FileQuorumStateStore` provides the durable local state needed to resume elections and prevent split-brain issues after broker restarts.

## Controller coordination (`metadata/`)

### `QuorumController`
Location: `metadata/src/main/java/org/apache/kafka/controller/QuorumController.java`

`QuorumController` is the active metadata controller implementation for KRaft.

Its responsibilities include:
- managing controller state and metadata operations
- registering a `RaftClient.Listener` (`QuorumMetaLogListener`) with the Raft client
- writing controller state changes to the metadata log via Raft
- handling leader changes, bootstrapping, and snapshot loading
- sequencing controller operations using a single-threaded event queue

### `appendRecords` and Raft write flow

`QuorumController` uses an internal helper `appendRecords(...)` to batch and write controller records to the Raft log. Key aspects:
- supports atomic and non-atomic controller results
- limits batch sizes for metadata log writes
- splits large result sets into multiple Raft append batches when needed
- converts controller operations into Raft log entries that are processed once committed

The controller writes through the Raft layer using methods like `raftClient.prepareAppend(controllerEpoch, records)` followed by `raftClient.schedulePreparedAppend()`. This two-step flow ensures the controller can prepare batches before instructing the Raft implementation to persist them.

### `RaftClient.Listener` integration

`QuorumMetaLogListener` in `QuorumController` handles Raft events:
- `handleCommit(BatchReader)` processes committed metadata log batches
- `handleLoadSnapshot(SnapshotReader)` restores state from committed snapshots
- `handleLoadBootstrap(SnapshotReader)` loads bootstrap snapshots during startup
- `handleLeaderChange(LeaderAndEpoch)` updates controller leadership state and triggers `claim()` / `renounce()` logic

This listener is the key integration point where Raft commitment becomes controller state transitions.

## Storage internals (`storage/`)

Kafka's log storage layer is built around append-only segment files and sparse index files.

### `LogSegment`
Location: `storage/src/main/java/org/apache/kafka/storage/internals/log/LogSegment.java`

A `LogSegment` represents one physical segment of a partition log, including:
- the segment data file containing message batches
- the offset index (`offsetIndex`) mapping base offsets to physical positions
- the time index (`timeIndex`) mapping timestamps to offsets
- the transaction index for transactional append support

`LogSegment` handles segment lifecycle operations such as:
- appending new records
- determining whether a segment should roll based on size and time
- truncating to a safe offset boundary
- exposing index metadata for reads and recovery

### `OffsetIndex`
Location: `storage/src/main/java/org/apache/kafka/storage/internals/log/OffsetIndex.java`

`OffsetIndex` is a sparse index file. It maps a subset of record offsets to physical file positions, allowing fast seek into the segment without indexing every message.

Key behavior:
- `lookup(targetOffset)` returns the nearest index position for a requested offset
- `append(offset, position)` records index entries periodically
- `truncateTo(offset)` truncates the index during log truncation
- `lastOffset()` returns the highest indexed offset

This sparse format keeps index files compact while still enabling efficient random access.

### `TimeIndex`
Location: `storage/src/main/java/org/apache/kafka/storage/internals/log/TimeIndex.java`

`TimeIndex` maps timestamps to offsets for time-based fetch requests.

Key behavior:
- `lookup(timestamp)` finds the first offset whose batch timestamp is >= requested time
- `maybeAppend(offset, timestamp)` adds sparse time entries
- `isFull()` detects when the time index must be rolled or closed
- `truncateTo(offset)` truncates the index when the log is truncated

This index supports features like consumer fetches by time and retention-based cleanup.

## How these pieces fit together

- The Raft client (`raft/`) provides consensus and durable metadata replication.
- `KafkaRaftLog` reuses Kafka's `UnifiedLog` to persist Raft entries in a normal Kafka log format.
- The metadata controller (`metadata/`) listens to Raft commits and applies them as cluster metadata operations.
- Low-level storage (`storage/`) provides the actual on-disk log and index format used by both regular partition logs and the Raft-backed metadata log.

## Important concepts

- `highWatermark()` is the Raft commit point; data below it is guaranteed to be durable and replicated.
- `prepareAppend()` / `schedulePreparedAppend()` separate batch preparation from actual log write scheduling.
- `truncateTo()` is used to safely roll back uncommitted Raft entries on election/leadership changes.
- `SnapshotReader` / `SnapshotWriter` support loading and storing state snapshots for fast recovery.
- Sparse indexes keep log reads efficient without indexing every record.

## Paths of interest

- Raft protocol and implementation
  - `raft/src/main/java/org/apache/kafka/raft/KafkaRaftClient.java`
  - `raft/src/main/java/org/apache/kafka/raft/internals/KafkaRaftLog.java`
  - `raft/src/main/java/org/apache/kafka/raft/FileQuorumStateStore.java`

- Controller coordination
  - `metadata/src/main/java/org/apache/kafka/controller/QuorumController.java`

- Log storage internals
  - `storage/src/main/java/org/apache/kafka/storage/internals/log/LogSegment.java`
  - `storage/src/main/java/org/apache/kafka/storage/internals/log/OffsetIndex.java`
  - `storage/src/main/java/org/apache/kafka/storage/internals/log/TimeIndex.java`

## Summary

Kafka's Raft implementation is closely integrated with its storage layer. The `raft/` code provides consensus and quorum persistence, while `metadata/` contains the controller that converts Raft commits into cluster metadata state. The `storage/` layer provides the underlying append-only segment format and sparse indexes that make both regular topic logs and the internal metadata log efficient and durable.
