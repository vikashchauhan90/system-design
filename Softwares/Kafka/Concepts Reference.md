# Apache Kafka: Complete Distributed Systems Algorithms & Concepts Reference

## Document Overview

This document provides a comprehensive analysis of all major algorithms, data structures, and distributed systems concepts implemented in Apache Kafka. It covers the core storage engine, consensus mechanisms, replication strategies, and advanced optimization techniques.

---

## Table of Contents

1. [Core Storage Algorithms](#core-storage-algorithms)
2. [Consensus & Coordination Algorithms](#consensus--coordination-algorithms)
3. [Replication & High Availability](#replication--high-availability)
4. [Message Routing & Distribution](#message-routing--distribution)
5. [Transaction & Exactly-Once Semantics](#transaction--exactly-once-semantics)
6. [Membership & Failure Detection](#membership--failure-detection)
7. [Optimization Data Structures](#optimization-data-structures)
8. [Complete System Interaction Diagram](#complete-system-interaction-diagram)

---

## Core Storage Algorithms

### 1. Append-Only Log

**Purpose**: Foundation of Kafka's storage model

**How it works**:
- All writes are appended sequentially to the end of a log file
- No in-place updates or deletions (until retention cleanup)
- Writes are always sequential, never random access

**Benefits**:
- Maximizes disk I/O throughput (sequential writes are much faster than random)
- Enables O(1) write complexity
- Simplifies replication (followers just replicate the append sequence)

**Implementation Location**:
- `core/src/main/scala/kafka/log/Log.scala`
- `core/src/main/scala/kafka/log/LogSegment.scala`

### 2. Segmented Log Rolling

**Purpose**: Prevent unbounded log file growth

**How it works**:
- Each partition log is divided into multiple segments
- Only one segment is "active" and accepts writes
- Segments are rolled (closed and new one created) based on:
  - Size threshold (`log.segment.bytes`, default: 1GB)
  - Time threshold (`log.roll.ms`)
  - Any non-empty segment condition (prevents empty segment creation)

**Segment Lifecycle**:
1. **Active Segment**: Accepting new writes
2. **Rolled Segment**: Read-only, available for reads
3. **Deleted Segment**: Removed by retention policy

**Configuration**:
```properties
log.segment.bytes=1073741824  # 1GB
log.roll.ms=604800000         # 7 days
```

### 3. Sparse Offset Index

**Purpose**: Enable O(log N) message lookup by offset

**How it works**:
- Maps logical message offsets to physical byte positions in segment files
- Index entries are "sparse" (not every message has an entry)
- Default offset index interval: `log.index.interval.bytes = 4096` bytes

**File Format**:
```
[8 bytes relative offset][4 bytes physical position]  # 12 bytes per entry
```

**Key Properties**:
- Monotonically increasing offsets
- Binary search for offset lookup
- Memory-mapped for fast access

**Implementation**: `core/src/main/scala/kafka/log/OffsetIndex.scala`

### 4. Time-Based Index

**Purpose**: Enable time-based message lookup

**How it works**:
- Maps timestamps to logical offsets
- Each entry: `[8 bytes timestamp][4 bytes relative offset]` (12 bytes total)
- Supports timestamp queries for consumer offset reset

**Operations**:
```scala
def lookup(targetTimestamp: Long): TimestampOffset  // Find offset for timestamp
def lastEntry: TimestampOffset                      // Highest timestamp in index
def truncateTo(offset: Long)                        // Remove entries after offset
```

**Implementation**: `core/src/main/scala/kafka/log/TimeIndex.scala`

### 5. Retention & Cleanup Policies

**Time-Based Retention**:
- Delete segments older than `log.retention.ms` (default: 168 hours / 7 days)
- `log.retention.ms = -1` disables time-based retention

**Size-Based Retention**:
- Delete oldest segments when total log size exceeds `log.retention.bytes`
- `log.retention.bytes = -1` disables size-based retention

**Cleanup Process**:
- Log cleaner runs as a background thread
- Segments are deleted after `log.segment.delete.delay.ms` to prevent race conditions
- `firstAppendTime` is reset if truncated segment becomes empty

---

## Consensus & Coordination Algorithms

### 6. Raft Consensus Algorithm (KRaft Mode)

**Purpose**: Leader election and log replication for Kafka's internal metadata

**Implementation Components**:

| Component | Responsibility |
|-----------|---------------|
| `KafkaRaftClient` | Core Raft logic (leader election, log replication, state transitions) |
| `QuorumState` | Manages node role (Leader/Follower/Candidate) and epoch tracking |
| `QuorumStateStore` | Persistent storage for crash recovery |
| `NetworkChannel` | Raft RPC communication |

**Raft State Machine**:
1. **Follower**: Responds to leader heartbeats, accepts log entries
2. **Candidate**: Initiates election after timeout, requests votes
3. **Leader**: Accepts writes, replicates to followers, sends heartbeats

**Election Flow**:
```
Follower Timeout → Candidate → RequestVotes → Majority Response → Leader
```

**Leader Commitment**:
- New leaders must commit a `LeaderChange` record before exposing high watermark
- Prevents commitment of entries from previous terms

**Key Configuration**:
```properties
controller.quorum.voters=1@host1:9093,2@host2:9093,3@host3:9093
controller.quorum.election.timeout.ms=1000
```

**Implementation**: `raft/src/main/java/org/apache/kafka/raft/KafkaRaftClient.java`

### 7. Zab Consensus Protocol (ZooKeeper Mode - Legacy)

**Purpose**: ZooKeeper's native consensus protocol for cluster coordination

**Phases**:
1. **Discovery**: Peer discovery and leader election
2. **Synchronization**: Follower catches up with leader's log
3. **Broadcast**: Atomic broadcast of transactions to all followers

**Differences from Raft**:
- Zab uses a leader-lease mechanism instead of heartbeats
- Zab has a more complex recovery phase
- Kafka is migrating entirely to Raft (KRaft) for simplicity

### 8. Quorum-Based Voting

**Purpose**: Prevent split-brain scenarios in cluster coordination

**How it works**:
- Controllers form a quorum (odd number: 3, 5, or 7 nodes)
- Majority (⌊n/2⌋ + 1) must agree on any decision
- Example: With 3 controllers, need 2 votes for any state change

**Applications in Kafka**:
- Controller leader election
- Metadata log commits (KRaft mode)
- Partition leadership changes (via controller)

**Split-Brain Prevention**:
- Two controllers cannot both think they're leader
- Ephemeral node creation requires majority
- Session timeouts require quorum validation

---

## Replication & High Availability

### 9. Leader-Based Replication (Partition Level)

**Purpose**: Provides fault tolerance for topic partitions

**How it works**:
- Each partition has one **Leader** broker handling all reads/writes
- Zero or more **Follower** brokers passively replicate data
- Followers pull data from leader (not push)

**Replication Flow**:
```
Producer → Leader Broker → [append to local log]
                        ↓
            Follower 1 ← Fetch (pull)
            Follower 2 ← Fetch (pull)
```

**Advantages**:
- Simple consistency model (all writes go to leader)
- No conflict resolution needed
- High throughput (leader coordinates all writes)

**Trade-offs**:
- Leader becomes single point of write availability
- Followers are slightly behind leader (eventual consistency)

### 10. In-Sync Replicas (ISR)

**Purpose**: Dynamic quorum for partition availability decisions

**How it works**:
- ISR contains replicas that are "caught up" with the leader
- A replica is in ISR if:
  - It has fetched messages in the last `replica.lag.time.max.ms`
  - It hasn't fallen behind by more than `replica.lag.max.messages` (deprecated)

**ISR Management**:
- Leader tracks ISR membership
- Followers that fall behind are removed from ISR
- Lagging followers rejoin when caught up

**Write Acknowledgement Levels**:

| acks Value | Behavior | Durability |
|------------|----------|------------|
| `acks=0` | Producer doesn't wait for any acknowledgment | None (at-most-once) |
| `acks=1` | Leader writes to local log and confirms | Low (leader loss = data loss) |
| `acks=all` | Waits for all ISR replicas | Highest (no data loss) |

**Configuration**:
```properties
min.insync.replicas=2  # Minimum ISR size for acks=all
replica.lag.time.max.ms=10000
```

### 11. Leaderless Replication (Comparison)

**Note**: Kafka does NOT use leaderless replication (unlike Cassandra or DynamoDB). This is included for comparison.

| Aspect | Leader-Based (Kafka) | Leaderless (Cassandra) |
|--------|---------------------|------------------------|
| Write Path | All writes to leader | Any replica can accept writes |
| Consistency | Easy to reason about | Requires quorum calculations |
| Conflict Resolution | Not needed (single writer) | Last-write-wins or custom |
| Read Repair | Not applicable | Required for consistency |
| Kafka's Use Case | Partition replication | Not used |

---

## Message Routing & Distribution

### 12. Consistent Hashing for Partition Assignment

**Purpose**: Deterministically map message keys to partitions

**Algorithm**:
```java
// Murmur2 hash for partition calculation
int partition = Math.abs(murmur2(keyBytes)) % numPartitions;
```

**Properties**:
- Same key → same partition (every time, unless partition count changes)
- Well-distributed keys → balanced load across partitions
- Deterministic → reproducible routing

**Key-to-Partition Guarantees**:
- **Per-key ordering**: All messages with same key arrive in order
- **No cross-partition ordering**: Different keys may arrive out of order
- **Hot key problem**: One key with high volume over-loads a single partition

**Implementation**: `clients/src/main/java/org/apache/kafka/clients/producer/internals/Partitioner.java`

### 13. Round-Robin Partitioner (Kafka < 2.4)

**Purpose**: Even distribution when no message key is provided

**How it works**:
- Cycles through all available partitions sequentially
- Each message goes to next partition in sequence

**Example** (3 partitions):
```
Message 1 → Partition 0
Message 2 → Partition 1
Message 3 → Partition 2
Message 4 → Partition 0
Message 5 → Partition 1
...
```

**Limitations**:
- Each message sent individually (poor batching)
- Many small network requests
- Suboptimal throughput

### 14. Sticky Partitioner (Kafka ≥ 2.4)

**Purpose**: Optimize batch utilization for key-less messages

**How it works**:
1. Randomly select an available partition
2. Send all messages to that partition until batch is full
3. Roll to a different partition for the next batch

**Benefits**:
- Larger batch sizes (better compression)
- Fewer network requests
- Higher throughput (30-50% improvement in benchmarks)
- Still achieves long-term even distribution

**Example** (Batch size = 100 messages):
```
Batch 1 (100 messages) → Partition 1
Batch 2 (100 messages) → Partition 0
Batch 3 (100 messages) → Partition 2
...
```

### 15. Custom Partitioners

**Use cases**: When default partitioning doesn't meet business requirements

**Common custom strategies**:
- Geographical routing (messages from EU → specific partitions)
- Load-aware routing (avoid hot partitions)
- Business-rule routing (VIP customers → dedicated partitions)
- Key transformation (hash of compound key)

**Implementation**:
```java
public class CustomPartitioner implements Partitioner {
    @Override
    public int partition(String topic, Object key, byte[] keyBytes,
                         Object value, byte[] valueBytes, Cluster cluster) {
        // Custom logic here
        return calculatedPartition;
    }
}
```

---

## Transaction & Exactly-Once Semantics

### 16. Two-Phase Commit (2PC) for Transactions

**Purpose**: Enable atomic writes across multiple partitions

**Components**:
- **Transaction Coordinator**: Special broker managing transaction state
- **Transaction Log**: Internal topic storing transaction status
- **Producer ID (PID)**: Unique identifier for each transactional producer
- **Epoch**: Monotonic number to fence out old producer instances

**Transaction Flow**:

```
Phase 1: Prepare
├── Producer sends messages to multiple partition leaders
├── Producer sends PrepareCommit to Coordinator
└── Coordinator requests all participants to prepare

Phase 2: Commit/Abort
├── Coordinator collects acknowledgements from all participants
├── If all OK: Coordinator writes COMMIT marker to transaction log
├── If any failure: Coordinator writes ABORT marker
└── Producer receives final status
```

**Exactly-Once Guarantees**:
- **Idempotent production**: Duplicate writes are detected and dropped (using PID + sequence numbers)
- **Atomic transactions**: Multiple partitions updated atomically
- **Consumer exactly-once**: Using `read_committed` isolation level

**Configuration**:
```properties
transaction.state.log.replication.factor=3
transaction.state.log.min.isr=2
transaction.max.timeout.ms=900000
```

### 17. Idempotent Producer

**Purpose**: Prevent duplicate messages on retry

**How it works**:
- Each producer gets a unique Producer ID (PID)
- Messages have monotonically increasing sequence numbers per partition
- Broker tracks last committed sequence number per PID
- Duplicate sequence numbers are rejected

**Sequence Number Space**:
```
(PID, Partition) → [Sequence Number]
Produced messages: 0, 1, 2, 3, 4, 5...
                  ↑ If sequence 3 is received twice, second is rejected
```

**Enable**:
```properties
enable.idempotence=true
acks=all  # Required
max.in.flight.requests.per.connection=5  # Or less
```

---

## Membership & Failure Detection

### 18. Gossip Protocol

**Purpose**: Decentralized cluster membership dissemination

**How it works**:
- Each broker periodically "gossips" with random peers
- Shared information: heartbeats, new brokers, failed brokers, configuration
- Information spreads through cluster like epidemic

**Protocol Properties**:
- **Decentralized**: No single point of failure for membership
- **Scalable**: O(log N) dissemination time, O(1) messages per node
- **Eventually consistent**: All nodes learn state within O(log N) rounds

**Information Gossiped**:
- Broker heartbeats and last contact time
- New brokers joining cluster
- Brokers suspected/confirmed dead
- Partition rebalancing events

### 19. Heartbeat-Based Failure Detection

**Purpose**: Detect broker failures quickly

**Heartbeat Types**:

| Heartbeat | Source | Destination | Interval | Purpose |
|-----------|--------|-------------|----------|---------|
| Broker-to-Controller | All brokers | Controller | 6 seconds | Liveness detection |
| Follower-to-Leader | Partition followers | Partition leader | `replica.fetch.wait.max.ms` | Replication liveness |
| Consumer Group | Consumer | Group Coordinator | `heartbeat.interval.ms` | Consumer liveness |

**Failure Detection**:
- **Session timeout**: `session.timeout.ms` (default: 45 seconds)
- **Max heartbeat interval**: `max.heartbeat.interval.ms` (consumer)
- **ZooKeeper session timeout**: `zookeeper.session.timeout.ms` (legacy mode)

**Suspicion vs. Confirmation**:
- After missed heartbeat: Mark as "suspected"
- After timeout period expires: Mark as "dead"
- Trigger leader re-election for dead broker's partitions

---

## Optimization Data Structures

### 20. Bloom Filters for Log Compaction

**Purpose**: Speed up key lookup during log compaction

**How it works**:
- Probabilistic data structure testing set membership
- Can say: "Key definitely NOT in segment" (100% certainty)
- Can say: "Key MIGHT be in segment" (configurable false positive rate)

**Compaction Use Case**:
When cleaning a compacted topic, the cleaner needs to know which keys have their latest value in which segment. Instead of scanning all segments:

```scala
if (bloomFilter.mightContain(key)) {
    // Actually check the segment (may be false positive)
    val position = segment.findOffsetByKey(key)
} else {
    // Skip this segment entirely (definitely not here)
}
```

**Space/Accuracy Trade-off**:
- Default: ~10 bytes per key
- False positive rate: ~1% (configurable)
- 10x faster than scanning segment metadata

**Implementation**: `log.cleaner.enable=true` (default enabled)

### 21. Zero-Copy Data Transfer (sendfile)

**Purpose**: Eliminate unnecessary data copying between kernel and user space

**Traditional I/O Path**:
```
Disk → Kernel Buffer → User Buffer → Socket Buffer → Network
        (copy 1)        (copy 2)        (copy 3)
```

**Zero-Copy Path**:
```
Disk → Kernel Buffer → Socket Buffer → Network
        (copy 1)        (DMA copy 2)
```

**System Call**:
```c
sendfile(socket_fd, file_fd, offset, length);
```

**Benefits**:
- 65-70% reduction in CPU usage for data transfer
- Reduced memory bandwidth consumption
- Faster consumer fetch responses

**When Kafka uses it**:
- Consumer fetch requests for already-batched data
- Replication between brokers
- Log segment reading (non-compressed messages)

### 22. Memory-Mapped Files (MappedByteBuffer)

**Purpose**: Efficient access to index files

**How it works**:
- File is mapped directly into process virtual memory
- OS handles paging data in/out of physical RAM
- Reads become memory accesses (no system call overhead)

**Kafka's Usage**:
- **OffsetIndex**: Memory-mapped for fast offset-to-position lookup
- **TimeIndex**: Memory-mapped for fast timestamp-to-offset lookup
- **Producer state snapshots**: Memory-mapped for quick recovery

**Trade-offs**:
- ✅ Extremely fast for random access
- ✅ OS-managed caching
- ❌ Not efficient for append-heavy workloads (log segments use regular I/O)
- ❌ Can cause "mmap failed" errors for very large files (>2GB on 32-bit)

**Configuration**:
```properties
log.index.size.max.bytes=10485760  # 10MB max index size
```

### 23. Write-Ahead Log (WAL)

**Purpose**: Ensure durability before acknowledging writes

**How it works**:
1. Producer sends message to leader
2. Leader writes to kernel page cache (not necessarily to disk)
3. **On acks=all**: Leader forces flush to disk (fsync)
4. Leader sends acknowledgment to producer
5. Followers fetch and write to their own logs

**Durability Guarantees**:
- **acks=0**: No WAL (at-most-once)
- **acks=1**: Leader WAL only (may lose data if leader crashes before followers)
- **acks=all**: Leader + all ISR WAL (no data loss)

**Flushing Configuration**:
```properties
log.flush.interval.messages=Long.MAX_VALUE  # Don't force flush based on count
log.flush.interval.ms=Long.MAX_VALUE         # Don't force flush based on time
# OS handles flushing via page cache + background flush
```

---

## Complete System Interaction Diagram

```
┌─────────────────────────────────────────────────────────────────────────────┐
│                           PRODUCER LAYER                                     │
│  ┌──────────────┐  ┌──────────────┐  ┌──────────────┐                       │
│  │  Sticky/RR    │  │  Consistent  │  │ Idempotent   │                       │
│  │  Partitioner  │──│    Hashing   │──│  Producer    │                       │
│  └──────────────┘  └──────────────┘  └──────────────┘                       │
└───────────────────────────────────┬─────────────────────────────────────────┘
                                    │
                                    ▼
┌─────────────────────────────────────────────────────────────────────────────┐
│                           BROKER LAYER                                       │
│                                                                              │
│  ┌────────────────────────────────────────────────────────────┐             │
│  │              LEADER BROKER (Partition X)                    │             │
│  │  ┌──────────┐  ┌──────────┐  ┌──────────┐  ┌──────────┐   │             │
│  │  │  WAL     │→ │  Append  │→ │  Segment │→ │  Index   │   │             │
│  │  │  Write   │  │   Log    │  │   Roll   │  │  Update  │   │             │
│  │  └──────────┘  └──────────┘  └──────────┘  └──────────┘   │             │
│  │                                                             │             │
│  │  ┌────────────────────────────────────────────────────┐    │             │
│  │  │         ISR Management (Quorum)                    │    │             │
│  │  │  Follower 1 [ISR]  •  Follower 2 [ISR]  •  Follower 3 [Non-ISR]│     │
│  │  └────────────────────────────────────────────────────┘    │             │
│  └────────────────────────────────────────────────────────────┘             │
│                                                                              │
│  ┌────────────────────────────────────────────────────────────┐             │
│  │           CONTROLLER BROKER (KRaft/ZooKeeper)              │             │
│  │  ┌──────────┐  ┌──────────┐  ┌──────────┐  ┌──────────┐   │             │
│  │  │   Raft   │  │  Leader  │  │  Epoch   │  │  Quorum  │   │             │
│  │  │ Consensus│  │ Election │  │ Tracking │  │  Voting  │   │             │
│  │  └──────────┘  └──────────┘  └──────────┘  └──────────┘   │             │
│  └────────────────────────────────────────────────────────────┘             │
│                                                                              │
│  ┌────────────────────────────────────────────────────────────┐             │
│  │        TRANSACTION COORDINATOR BROKER                       │             │
│  │  ┌──────────┐  ┌──────────┐  ┌──────────┐                  │             │
│  │  │   2PC    │  │ Producer │  │   TXN    │                  │             │
│  │  │ Protocol │  │   ID     │  │   Log    │                  │             │
│  │  └──────────┘  └──────────┘  └──────────┘                  │             │
│  └────────────────────────────────────────────────────────────┘             │
└───────────────────────────────────┬─────────────────────────────────────────┘
                                    │
                                    ▼
┌─────────────────────────────────────────────────────────────────────────────┐
│                           STORAGE LAYER                                      │
│                                                                              │
│  Partition X Log                                                             │
│  ┌─────────────────────────────────────────────────────────────────────┐    │
│  │ Segment 0 (Active)      Segment 1            Segment 2               │    │
│  │ ┌─────────┬─────────┐   ┌─────────┬───────┐  ┌─────────┬─────────┐   │    │
│  │ │Append   │ Offset  │   │ Read-   │ Time  │  │ Compac- │ Bloom   │   │    │
│  │ │ Only    │ Index   │   │ Only    │ Index │  │ ted     │ Filter  │   │    │
│  │ └─────────┴─────────┘   └─────────┴───────┘  └─────────┴─────────┘   │    │
│  └─────────────────────────────────────────────────────────────────────┘    │
│                                                                              │
│  Index Files (Memory-Mapped)                                                 │
│  ┌────────────────────────────────────────────────────────────┐             │
│  │ OffsetIndex: [offset→position] • TimeIndex: [timestamp→offset]          │
│  └────────────────────────────────────────────────────────────┘             │
└───────────────────────────────────┬─────────────────────────────────────────┘
                                    │
                                    ▼
┌─────────────────────────────────────────────────────────────────────────────┐
│                           CONSUMER LAYER                                     │
│  ┌──────────────┐  ┌──────────────┐  ┌──────────────┐                       │
│  │   Zero-Copy  │  │   Offset     │  │ Transaction  │                       │
│  │   sendfile   │  │   Lookup     │  │   Isolation  │                       │
│  └──────────────┘  └──────────────┘  └──────────────┘                       │
└─────────────────────────────────────────────────────────────────────────────┘
```

---

## Algorithm Summary Table

| # | Algorithm/Concept | Primary Purpose | Kafka Component |
|---|------------------|-----------------|-----------------|
| 1 | Append-Only Log | Foundation storage model | LogSegment |
| 2 | Segmented Log Rolling | Manage log file size | LogManager |
| 3 | Sparse Offset Index | O(log N) offset lookup | OffsetIndex |
| 4 | Time-Based Index | Time-range queries | TimeIndex |
| 5 | Time/Size Retention | Data lifecycle management | LogCleaner |
| 6 | Raft Consensus | Metadata leader election | KafkaRaftClient |
| 7 | Zab Protocol (legacy) | ZooKeeper coordination | ZooKeeper |
| 8 | Quorum-Based Voting | Split-brain prevention | Controller/Quorum |
| 9 | Leader-Based Replication | Partition HA | PartitionLeader |
| 10 | In-Sync Replicas (ISR) | Dynamic replication quorum | ReplicaManager |
| 11 | Consistent Hashing | Key→Partition mapping | Partitioner |
| 12 | Round-Robin Partitioner | Even keyless distribution | DefaultPartitioner (old) |
| 13 | Sticky Partitioner | Optimized batch distribution | StickyPartitioner (new) |
| 14 | Two-Phase Commit (2PC) | Atomic cross-partition writes | TransactionCoordinator |
| 15 | Idempotent Producer | Duplicate detection | ProducerStateManager |
| 16 | Gossip Protocol | Cluster membership | BrokerMetadata |
| 17 | Heartbeat Detection | Failure detection | ControllerHeartbeat |
| 18 | Bloom Filters | Fast log compaction | LogCleaner |
| 19 | Zero-Copy (sendfile) | Efficient data transfer | SocketServer |
| 20 | Memory-Mapped Files | Fast index access | MappedByteBuffer |
| 21 | Write-Ahead Log (WAL) | Durability guarantee | Log.flush() |

---

## Source Code Reference Locations

| Component | Primary Source Path |
|-----------|---------------------|
| Raft Implementation | `raft/src/main/java/org/apache/kafka/raft/` |
| Log Management | `core/src/main/scala/kafka/log/` |
| Index Files | `core/src/main/scala/kafka/log/OffsetIndex.scala`<br>`core/src/main/scala/kafka/log/TimeIndex.scala` |
| Partitioning | `clients/src/main/java/org/apache/kafka/clients/producer/Partitioner.java` |
| Transactions | `core/src/main/scala/kafka/coordinator/transaction/` |
| Controller | `core/src/main/scala/kafka/controller/` |
| Replication | `core/src/main/scala/kafka/server/ReplicaManager.scala` |

---

## Key Configuration Reference

### Storage Configuration
```properties
log.segment.bytes=1073741824
log.roll.ms=604800000
log.retention.ms=604800000
log.retention.bytes=-1
log.index.interval.bytes=4096
log.index.size.max.bytes=10485760
log.cleaner.enable=true
```

### Raft/KRaft Configuration
```properties
controller.quorum.voters=1@host1:9093,2@host2:9093,3@host3:9093
controller.quorum.election.timeout.ms=1000
controller.quorum.fetch.timeout.ms=2000
```

### Replication Configuration
```properties
default.replication.factor=3
min.insync.replicas=2
replica.lag.time.max.ms=10000
```

### Producer Configuration
```properties
enable.idempotence=true
acks=all
max.in.flight.requests.per.connection=5
partitioner.class=org.apache.kafka.clients.producer.internals.DefaultPartitioner
```

---

## Conclusion

Apache Kafka is a sophisticated distributed system that combines multiple proven algorithms and data structures from distributed systems theory. By understanding these components—from the foundational append-only logs and Raft consensus to the optimization techniques like Bloom filters and zero-copy transfers—developers can better configure, operate, and extend Kafka for their specific use cases.

The system's design philosophy emphasizes:
- **Simplicity**: Append-only logs are easy to reason about
- **Performance**: Zero-copy, memory-mapping, and batching
- **Durability**: WAL, ISR, and quorum-based commits
- **Scalability**: Consistent hashing, gossip protocols, and segmented storage

This combination of algorithms makes Kafka suitable for everything from high-throughput event streaming to exactly-once transactional processing.
