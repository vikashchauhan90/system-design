
# Apache Kafka Core Architecture Documentation

This document provides an analysis of the core implementation details for Kafka's Raft consensus protocol, storage mechanisms, and partitioning strategies.

## Table of Contents
1. [KRaft (Kafka Raft) Implementation](#kraft-raft-implementation)
2. [Storage Architecture](#storage-architecture)
3. [Partitioning and Message Routing](#partitioning-and-message-routing)

---

## 1. KRaft (Kafka Raft) Implementation

### Overview
Kafka uses the Raft consensus algorithm to manage metadata and elect leaders in KRaft mode (Kafka Raft mode), replacing ZooKeeper-based metadata management.

### Key Components

#### **KafkaRaftClient**
The core Raft client implementation located in `raft/src/main/java/org/apache/kafka/raft/KafkaRaftClient.java` . This component manages:
- Leader election
- Log replication
- State transitions (Leader, Follower, Candidate)

#### **QuorumState Management**
The `QuorumState` class manages the node's role and state within the Raft quorum :
- Tracks voter IDs and observer nodes
- Manages election timeout and fetch timeout configurations
- Persists state through `QuorumStateStore` for crash recovery

#### **Network Layer**
The dynamic network management allows KRaft replicas to send requests to any node, not just statically configured voters :
```java
// Dynamic voter discovery through Raft RPCs
// BeginQuorumEpoch request and Fetch response
```

### Raft Election Flow

1. **Initialization**: `KafkaRaftManager` creates `KafkaRaftClient` and `NetworkChannel` components
2. **Election Triggers**: Candidates send `Vote` requests; Leaders send `BeginQuorumEpoch` messages
3. **Leader Commitment**: New leaders must commit a `LeaderChange` record before exposing high watermark 

### Snapshot Management
KRaft implements sophisticated snapshot handling :
- `SnapshotReader` and `SnapshotWriter` interfaces for snapshot I/O
- Automatic snapshot loading when listener's next offset < log start offset
- Snapshots represent the state up to a specific log offset

### Metrics
Key metrics exposed for KRaft :
- Number of voters and observers in quorum
- Uncommitted voter changes
- Ignored static voter metrics

---

## 2. Storage Architecture

### Log-Based Storage Model

Kafka stores data as append-only logs. Each log is divided into segments with associated indexes .

#### **Log Segments**
- **Active Segment**: Single segment accepting new writes
- **Rolled Segments**: Read-only segments created periodically or when size threshold met

**Configuration parameters** :
| Parameter | Description |
|-----------|-------------|
| `log.segment.bytes` | Maximum segment size (default: 1GB) |
| `log.roll.ms` | Time before rolling active segment |
| `log.retention.ms` | Time-based retention |
| `log.retention.bytes` | Size-based retention |

#### **Segment Rolling Conditions**
A new segment is rolled when :
- Current segment reaches maximum size
- Time threshold (`segment.ms`) is exceeded
- Segment has non-zero data (prevents empty segment creation)

### Index Files

Kafka maintains two index types per segment:

#### **OffsetIndex**
- Maps logical offsets to physical byte positions
- Enables O(log N) message lookup by offset

#### **TimeIndex**
Maps timestamps to logical offsets :

**File Format** :
```
[8 bytes timestamp][4 bytes relative offset]  // 12 bytes per entry
```

**Key characteristics** :
- Sparse indexing (not every message has an entry)
- Monotonically increasing timestamps guarantee
- Binary search for timestamp lookup
- Uses Murmur2 hash for offset calculation
- `lastEntry` returns highest timestamp ≤ target timestamp

**TimeIndex Operations** :
```scala
def lookup(targetTimestamp: Long): TimestampOffset
def lastEntry: TimestampOffset  // Highest timestamp in index
def truncateTo(offset: Long)    // Remove entries after offset
```

### Data Retention Policies

Both time and size-based retention can be combined :
- **Time-based**: When `log.retention.ms` exceeded, old segments deleted
- **Size-based**: When total log size exceeds `log.retention.bytes`
- Whichever threshold reached first triggers cleanup

**Special cases**:
- `log.retention.ms = -1`: No time limit (monitor disk usage!)
- `log.segment.delete.delay.ms`: Delay before file deletion

### Segment Truncation Behavior

When a segment is truncated :
- `firstAppendTime` is reset to None if segment size becomes 0
- Prevents immediate time-based rolling of empty segments
- Log maintains proper offset continuity

---

## 3. Partitioning and Message Routing

### Partitioner Logic

The partitioner determines which partition receives each message .

#### **With Message Keys**
Kafka applies Murmur2 hash to the key :
```java
int hash = murmur2(keyBytes);
int partition = Math.abs(hash) % numPartitions;
```

**Properties**:
- Same key always → same partition (ordering guarantee)
- Even key distribution prevents hot partitions
- Partition count changes affect routing

#### **Without Message Keys**

Behavior varies by Kafka version :

| Version | Partitioner | Behavior |
|---------|------------|----------|
| < 2.4 | Round-robin | Cycles through partitions sequentially |
| ≥ 2.4 | Sticky | Sticks to one partition until batch is full |

### Sticky Partitioner (Kafka 2.4+)

**How it works** :
1. Randomly select an available partition
2. Send all messages to chosen partition until batch fills
3. Switch to different partition for next batch
4. Repeat for optimal batching

**Benefits** :
- Higher throughput (better batch utilization)
- Fewer network requests
- Better compression ratios
- Even long-term distribution

### Load Balancing Considerations

**Hot key problem** :
- If one key dominates traffic, all messages go to same partition
- Creates overloaded broker and consumer
- Solution: Redesign keys or implement custom partitioner

**Ordering guarantees** :
- Per-key ordering maintained within partition
- No cross-partition ordering
- Use same key for related messages requiring ordering

### Custom Partitioners

Implement `org.apache.kafka.clients.producer.Partitioner` interface for custom routing logic when default behavior doesn't meet requirements .

---

## Important Source Locations

| Component | Path |
|-----------|------|
| Raft Client | `raft/src/main/java/org/apache/kafka/raft/KafkaRaftClient.java` |
| TimeIndex | `core/src/main/scala/kafka/log/TimeIndex.scala` |
| OffsetIndex | `core/src/main/scala/kafka/log/OffsetIndex.scala` |
| Raft Manager | `core/src/main/scala/kafka/raft/RaftManager.scala` |
| Quorum State | `raft/src/main/java/org/apache/kafka/raft/QuorumState.java` |

## Key Configuration Parameters Summary

| Parameter | Purpose | Default |
|-----------|---------|---------|
| `controller.quorum.voters` | Static voter list | - |
| `controller.quorum.bootstrap.servers` | Dynamic endpoint discovery | - |
| `log.segment.bytes` | Max segment size | 1GB |
| `log.retention.ms` | Time-based retention | 168h |
| `log.retention.bytes` | Size-based retention | -1 (unlimited) |
| `segment.bytes` | Topic-level segment size | Inherits broker |

 