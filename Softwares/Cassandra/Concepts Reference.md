# Apache Cassandra: Complete Distributed Systems Algorithms & Concepts Reference

## Document Overview

This document provides a comprehensive analysis of Apache Cassandra's architectural patterns, algorithms, and distributed systems concepts. Cassandra is a distributed NoSQL database designed for massive scale, high availability, and linear write scalability. Its architecture combines Amazon Dynamo's distribution mechanisms with Google Bigtable's storage model, creating a masterless system with no single point of failure.

---

## Table of Contents

1. [Core Architectural Patterns](#core-architectural-patterns)
2. [Data Distribution & Partitioning](#data-distribution--partitioning)
3. [LSM-Tree Storage Engine](#lsm-tree-storage-engine)
4. [Write Path & Durability](#write-path--durability)
5. [Read Path & Optimization](#read-path--optimization)
6. [Replication & Consistency](#replication--consistency)
7. [Cluster Membership & Failure Detection](#cluster-membership--failure-detection)
8. [Lightweight Transactions (Paxos)](#lightweight-transactions-paxos)
9. [Compaction Strategies](#compaction-strategies)
10. [Caching Mechanisms](#caching-mechanisms)
11. [Anti-Entropy & Repair](#anti-entropy--repair)
12. [Complete System Interaction Diagram](#complete-system-interaction-diagram)

---

## Core Architectural Patterns

### 1. Masterless Peer-to-Peer Architecture

**Purpose**: Eliminate single points of failure and enable linear scalability

**Key Characteristics**:
- All nodes are equal (no master/slave distinction)
- Any node can handle any read or write request
- Nodes communicate via gossip protocol
- Clients can connect to any node (becomes coordinator)

**Benefits**:
- No single point of failure
- Linear write scalability
- Simple operations (add nodes without downtime)
- Natural load distribution

**Comparison**:
| Aspect | Master-Slave (MySQL, MongoDB) | Masterless (Cassandra) |
|--------|------------------------------|------------------------|
| Write scaling | Limited by master | Linear with node count |
| Failover | Manual or complex | Automatic |
| Client complexity | Simple (knows master) | Higher (any node) |
| Consistency | Strong by default | Tunable |

### 2. Dynamo + BigTable Hybrid Architecture

**Purpose**: Combine best of both distributed systems

**Influences**:

| Source | Components Adopted |
|--------|-------------------|
| **Amazon Dynamo** | Consistent hashing, gossip protocol, hinted handoff, tunable consistency |
| **Google Bigtable** | SSTable storage, column-family data model, memtable architecture |

**Why This Hybrid Works**:
- Dynamo provides distribution and availability
- Bigtable provides efficient storage and querying
- Combined: Highly available, scalable, and performant

---

## Data Distribution & Partitioning

### 3. Consistent Hashing

**Purpose**: Distribute data evenly across nodes without central coordination

**How It Works**:
```
hash(partition_key) → token → position on ring → node assignment

Example with Murmur3Partitioner:
hash("user123") = 2,516,483,921,400,819,000  (64-bit value)
token range: -2^63 to +2^63 - 1
node assigned based on token ownership
```

**Properties**:
- **Deterministic**: Same key always maps to same token
- **Even distribution**: Hash function spreads keys uniformly
- **Minimal rebalancing**: Adding node only affects neighboring ranges

**Visual Representation**:
```
Ring of 4 nodes (simplified):
        
        Node A
      /        \
     /          \
Node D            Node B
     \          /
      \        /
        Node C

Each node owns a contiguous range of tokens
```

### 4. Token Ring Architecture

**Purpose**: Logical representation of data distribution across cluster

**Structure**:
- Each node assigned one or more token ranges
- Token ranges form a complete ring (0 to max value)
- Partition key hash determines position on ring
- Data stored on node owning that token range

**Primary Load vs. Total Load**:
Cassandra distinguishes between:
- **Primary load**: Data a node is responsible for from its token range
- **Total load**: Including replicas from other token ranges

Load balancing decisions use primary load for accuracy.

### 5. Virtual Nodes (Vnodes)

**Purpose**: Improve distribution granularity and reduce rebalancing overhead

**How Vnodes Work**:
- Each physical node assigned multiple tokens (default: 256)
- Each token range is a vnode
- Vnodes are distributed randomly across physical nodes

**Benefits**:
| Feature | Without Vnodes | With Vnodes |
|---------|---------------|-------------|
| Rebalancing granularity | Entire node's data | Small token ranges |
| Node addition | Rebalance entire node | Borrow vnodes from many nodes |
| Hot spots mitigation | Poor | Excellent |
| Heterogeneous clusters | Difficult | Easy (vnode count per node) |

**Configuration**:
```yaml
# cassandra.yaml
num_tokens: 256  # Default, can be adjusted per node
```

### 6. Partitioners

**Purpose**: Hash function for computing token from partition key

**Available Partitioners**:

| Partitioner | Hash Function | Token Range | Use Case |
|-------------|---------------|-------------|----------|
| **Murmur3Partitioner (default)** | MurmurHash 64-bit | -2⁶³ to +2⁶³-1 | New clusters, best performance |
| RandomPartitioner (legacy) | MD5 128-bit | 0 to 2¹²⁷-1 | Backward compatibility |
| ByteOrderedPartitioner | Raw key bytes | Lexical order | Range scans (rare, problematic) |

**Murmur3Partitioner Advantages**:
- Faster hashing (2-3x MD5 speed)
- Better distribution quality
- Industry standard for distributed systems

**Consistency Level Calculation Example**:
```cql
-- With RF=3, QUORUM = floor(3/2) + 1 = 2
INSERT INTO users (user_id, name) 
VALUES (123, 'John')
USING CONSISTENCY QUORUM;
-- Waits for 2 of 3 replicas to acknowledge
```

### 7. Rack and Datacenter Awareness

**Purpose**: Fault isolation and geographical distribution

**Hierarchy**:
```
Cluster
├── Datacenter (us-east)
│   ├── Rack 1 (rack1)
│   │   ├── Node 1
│   │   └── Node 2
│   └── Rack 2 (rack2)
│       ├── Node 3
│       └── Node 4
└── Datacenter (us-west)
    ├── Rack 1
    └── Rack 2
```

**Replication Rules**:
- Replicas placed in different racks when possible
- Never place all replicas in same rack
- Multiple datacenters enable disaster recovery

---

## LSM-Tree Storage Engine

### 8. Log-Structured Merge-Tree (LSM-Tree)

**Purpose**: Write-optimized storage turning random writes into sequential writes

**Core Insight**:
Random writes kill disk performance (especially spinning disks). LSM-trees batch writes in memory and flush sequentially to disk.

**Architecture Overview**:
```
Write Path:  Write → Commit Log → Memtable
                ↓
Read Path:   Memtable → SSTable (Bloom Filter → Index → Data)
                ↓
Background:  Compaction (merge SSTables)
```

**Why Cassandra Uses LSM-Tree**:
- Write throughput: 10x+ B-trees for write-heavy workloads
- No in-place updates (no read-modify-write)
- Sequential disk I/O (80%+ more efficient)
- Compression friendly (immutable files)

**Comparison**:

| Aspect | B-Tree (MySQL InnoDB) | LSM-Tree (Cassandra) |
|--------|----------------------|---------------------|
| Write amplification | Low | Medium-High |
| Read amplification | Low | Medium (mitigated by Bloom filters) |
| Space amplification | Low (in-place updates) | Medium (multiple versions) |
| Write throughput | Moderate | Very high |
| Suitable for | Mixed workloads | Write-heavy, append-heavy |

### 9. Commit Log

**Purpose**: Durability mechanism for crash recovery

**How It Works**:
1. Every write operation appended to commit log immediately
2. Commit log is sequential file on disk
3. Data held in memory (memtable) can be lost on crash
4. On restart, commit log replayed to rebuild memtables

**Properties**:
- **Write-ahead logging**: Log before memory update
- **Sequential writes**: Fast append-only I/O
- **Crash recovery only**: Clients never read from commit log
- **Configurable sync**: `commitlog_sync` (periodic/batch)

**Commit Log Components**:
```
commitlog/
├── CommitLog-1.log (active)
├── CommitLog-2.log (active)
└── CommitLog-*.log (recycled after replay)
```

### 10. Memtable

**Purpose**: In-memory write buffer for recent writes

**Characteristics**:
- One memtable per table
- Sorted data structure (typically skip list or red-black tree)
- Writes are O(log N) and always succeed
- Reads check memtable before SSTables

**Flush Triggers**:
| Trigger | Default | Description |
|---------|---------|-------------|
| Size threshold | 64 MB | Memtable flushed when full |
| Commit log limit | 32 MB per log | Replay time bound |
| Manual flush | N/A | `nodetool flush` |

**Flush Process**:
```
Memtable (64 MB) → Immutable Memtable → Write to SSTable → New Memtable
         ↓
   (non-blocking, writes continue to new memtable)
```

### 11. SSTable (Sorted String Table)

**Purpose**: Immutable on-disk storage format

**File Components**:

| Component | Purpose | Format |
|-----------|---------|--------|
| **Data.db** | Actual row data | Sorted by partition key |
| **Index.db** | Partition key to position | B-tree-like index |
| **Filter.db** | Bloom filter | Probabilistic membership |
| **Summary.db** | Partition summary | Sampled index entries |
| **Statistics.db** | Metadata | Row count, size estimates |
| **CompressionInfo.db** | Compression map | Block compression metadata |

**SSTable Characteristics**:
- **Immutable**: Never modified after write
- **Sorted**: Data sorted by partition key
- **Compressed**: Blocks compressed (default: LZ4, Snappy, or Deflate)
- **Versioned**: Multiple SSTables can exist for same partition

**Lifecycle**:
```
Memtable flush → SSTable (Level 0) → Compaction → Higher level → Tombstone cleanup
```

### 12. Bloom Filters

**Purpose**: Probabilistic membership testing to avoid unnecessary disk reads

**How They Work**:
- Bit array of size `m` with `k` hash functions
- Insert: Set bits at all `k` hash positions
- Query: If any bit is 0 → element definitely absent
- False positives possible, false negatives impossible

**Cassandra's Implementation**:
- One bloom filter per SSTable
- Stored in `Filter.db` file
- Memory-resident (configurable: disk or memory)
- False positive rate: Configurable (default ~1%)

**Performance Impact**:
```
Read Path with Bloom Filter:
Client Request → Check Memtable (miss) → Check Bloom Filter
                                              ↓
                                    "Probably has" → Open SSTable
                                    "Definitely no" → Skip SSTable
```

**Memory Calculation**:
- 1-2 bytes per row in SSTable (typical)
- 100 GB of data → 100-200 MB bloom filter

---

## Write Path & Durability

### 13. Write Path Flow

**Purpose**: Efficient, durable write processing

**Step-by-Step Process**:
```
1. Client → Coordinator Node (any node)
2. Coordinator calculates token: hash(partition_key)
3. Coordinator identifies replica nodes
4. Coordinator sends write to all replicas in parallel
5. Each replica:
   a. Writes to commit log (durability)
   b. Writes to memtable (in-memory)
6. Coordinator waits for acknowledgments (based on consistency level)
7. Coordinator responds to client
8. [Async] Memtable flushes to SSTable when full
```

**Write Path Visualization**:
```
Write Request
     │
     ▼
Coordinator Node (calculates replicas)
     │
     ├──────► Replica 1 ──► CommitLog ──► Memtable
     │
     ├──────► Replica 2 ──► CommitLog ──► Memtable
     │
     └──────► Replica 3 ──► CommitLog ──► Memtable
              (parallel writes)
                   │
                   ▼ (wait for QUORUM)
              Client Response
```

**No Reads on Write Path**:
Unlike B-tree databases, Cassandra never reads existing data during writes. This eliminates read-before-write overhead and makes writes extremely fast.

### 14. Hinted Handoff

**Purpose**: Maintain availability during node outages

**How It Works**:
1. Coordinator detects replica node is down
2. Coordinator stores mutation as "hint" locally
3. Hint includes: target node, mutation data, timestamp
4. Coordinator returns success (if consistency requirements met)
5. When down node returns:
   - Coordinators replay hints
   - Target node applies mutations
   - Hints discarded after replay

**Hint Properties**:
- **Configurable window**: `max_hint_window_in_ms` (default 3 hours)
- **Storage**: Local filesystem (`hints/` directory)
- **Replay on node up**: Automatic
- **Security**: Best-effort (not guaranteed delivery)

**Example Scenario**:
```
1. Node B fails at 10:00:00
2. Client writes to Coordinator Node A at 10:00:30
3. Node A stores hint for Node B
4. Client receives success (if CL met without B)
5. Node B recovers at 10:05:00
6. Node A replays hints to Node B
7. Node B is now consistent
```

**Consistency Implications**:
- Hinted handoff provides **eventual consistency**
- Applications expecting immediate consistency should use higher CL
- Cannot disable read repair entirely with hints alone

### 15. Write Consistency Levels

**Purpose**: Balance durability, consistency, and availability

**Consistency Levels**:

| Level | Behavior | Use Case |
|-------|----------|----------|
| **ANY** | At least one node (including hint) | Maximum availability, minimal durability |
| **ONE** | One replica acknowledges | Performance-focused, tolerant of inconsistency |
| **TWO** | Two replicas acknowledge | Balance |
| **THREE** | Three replicas acknowledge | Higher consistency |
| **QUORUM** | Majority: `(RF/2) + 1` | Default for strong consistency |
| **LOCAL_QUORUM** | Majority in local DC | Multi-DC deployments |
| **EACH_QUORUM** | Majority in every DC | Strongest, highest latency |
| **ALL** | All replicas acknowledge | Highest durability, lowest availability |

**QUORUM Formula**:
```
Given RF=3: QUORUM = floor(3/2) + 1 = 2
Given RF=5: QUORUM = floor(5/2) + 1 = 3
```

**Write Success Conditions**:
```
For CL=QUORUM with RF=3:
- Writes sent to all 3 replicas
- Success when 2 acknowledge
- Hint counts if CL=ANY, not for QUORUM
```

---

## Read Path & Optimization

### 16. Read Path Flow

**Purpose**: Efficient retrieval from multiple data sources

**Step-by-Step Process**:
```
1. Client → Coordinator Node
2. Coordinator calculates replica nodes
3. Coordinator sends read to replicas (fastest respond)
4. Each replica checks:
   a. Row cache (if enabled)
   b. Memtable
   c. SSTables (using Bloom filters)
5. Coordinator compares versions from multiple replicas
6. If inconsistency: Read repair triggered
7. Coordinator returns newest version to client
```

**Read Path Visualization**:
```
Read Request
     │
     ▼
Coordinator (identifies replicas)
     │
     ├──────► Replica 1
     │          ├─ Row Cache
     │          ├─ Memtable
     │          └─ SSTables (Bloom → Index → Data)
     │
     ├──────► Replica 2 (same process)
     │
     └──────► Replica 3 (same process)
              (parallel reads)
                   │
                   ▼
           Compare Versions
                   │
        ┌──────────┴──────────┐
        │                     │
   Consistent           Inconsistent
        │                     │
   Return Data         Trigger Read Repair
                           (update stale replicas)
```

### 17. Read Consistency Levels

**Purpose**: Control how many replicas must respond

**Levels for Reads**:

| Level | Behavior | When to Use |
|-------|----------|-------------|
| **ONE** | Fastest response, may return stale data | Non-critical reads |
| **QUORUM** | Majority must agree | Default for correctness |
| **LOCAL_QUORUM** | Majority in local DC | Multi-DC, low latency |
| **ALL** | All replicas must respond | Highest consistency, lowest availability |

**Formula for Strong Consistency**:
For linearizable reads: `R + W > RF`
- With RF=3, `QUORUM + QUORUM = 2 + 2 = 4 > 3` ✓
- With `ONE + ONE = 1 + 1 = 2 < 3` ✗ (eventual consistency)

### 18. Read Repair

**Purpose**: Fix inconsistent replicas during reads

**How Read Repair Works**:
1. Coordinator reads from all replicas (in background)
2. Compares digests (hashes) of returned data
3. If digests differ: Performs full data comparison
4. Identifies stale replicas
5. Sends writes to bring stale replicas current
6. Only impacts read path; no separate repair needed

**Read Repair Types**:

| Type | Description | Overhead |
|------|-------------|----------|
| **Blocking** | Waits for repair before returning | Higher latency |
| **Non-blocking** | Returns immediately, repairs async | Lower latency, eventual consistency |
| **Opportunistic** | Repairs based on probability | Configurable rate |

**Configuration**:
```yaml
# cassandra.yaml
read_repair_chance: 0.0  # Probability of repair (0-1)
dc_local_read_repair_chance: 0.0  # In local DC only
```

### 19. Replica Selection Strategy

**Purpose**: Choose fastest replicas for reads

**Strategies**:
| Strategy | Behavior | Use Case |
|----------|----------|----------|
| **Speculative retry** | Send to extra replica if slow | High read availability |
| **Dynamic snitching** | Track latency per replica | Performance optimization |
| **DC-aware** | Prefer local replicas | Multi-DC deployments |

**Eager Retries** (Cassandra 2.0+):
- If replica slow to respond
- Send request to additional replica
- Use fastest response
- Returns earlier than waiting for timeout

---

## Replication & Consistency

### 20. Replication Strategies

**Purpose**: Control how data copies are distributed

**SimpleStrategy** (Single Datacenter):
```cql
CREATE KEYSPACE my_keyspace
WITH REPLICATION = {
    'class': 'SimpleStrategy',
    'replication_factor': 3
};
```
- Replicas placed consecutively on ring
- Not rack/datacenter aware
- Only for single DC or testing

**NetworkTopologyStrategy** (Multi-Datacenter):
```cql
CREATE KEYSPACE my_keyspace
WITH REPLICATION = {
    'class': 'NetworkTopologyStrategy',
    'us-east': 3,
    'us-west': 2,
    'eu-west': 2
};
```
- Per-datacenter replication factor
- Rack-aware placement (different racks)
- Recommended for production

**Replica Placement Rules**:
1. First replica: Node owning token range
2. Additional replicas: Next nodes clockwise
3. Ensure replicas span racks (prevent rack-level failure)
4. Across DCs: Independent placement

### 21. Tunable Consistency

**Purpose**: Balance consistency, availability, partition tolerance (CAP)

**CAP Theorem Position**:
Cassandra is AP (Available, Partition-tolerant) by default, but can be tuned for CP.

**Consistency Spectrum**:

```
Eventual Consistency ←→ Strong Consistency
        ↓                    ↓
    CL=ONE (AP)          CL=QUORUM (CP)
```

**Write + Read Formula**:

| Use Case | Write CL | Read CL | Consistency | Availability |
|----------|----------|---------|-------------|--------------|
| **High performance** | ONE | ONE | Eventual | Highest |
| **Balanced** | QUORUM | QUORUM | Strong (R+W>RF) | High |
| **Strongest** | ALL | ALL | Linearizable | Lowest |

**Example: Banking vs. Analytics**
```cql
-- Banking transaction (strong consistency)
BEGIN TRANSACTION
UPDATE accounts SET balance = balance - 100 
WHERE user_id = '123' 
USING CONSISTENCY QUORUM;
-- Read back immediately
SELECT balance FROM accounts 
WHERE user_id = '123' 
USING CONSISTENCY QUORUM;

-- Analytics (eventual consistency)
INSERT INTO page_views (user_id, page, time) 
VALUES ('123', '/home', now()) 
USING CONSISTENCY ONE;
-- Query may be slightly stale, but fast
SELECT COUNT(*) FROM page_views 
USING CONSISTENCY ONE;
```

---

## Cluster Membership & Failure Detection

### 22. Gossip Protocol

**Purpose**: Decentralized cluster state propagation

**How Gossip Works**:
- Each node exchanges state with 1-3 other nodes every second
- One seed node in each gossip exchange
- State information spreads exponentially (log N time)
- Epidemic model (like human gossip)

**Gossiped Information**:
| Information | Purpose |
|-------------|---------|
| Heartbeat state | Node liveness |
| Load information | Load balancing |
| Schema version | Schema agreement |
| Token ownership | Data distribution |
| Datacenter/rack | Topology awareness |

**Gossip Efficiency**:
```
Time for all nodes to learn: O(log N) rounds
Network overhead: O(N) messages per round
```

**Seed Nodes**:
- Designated nodes for initial cluster discovery
- Not special beyond initial contact
- Best practice: 2-3 seeds per datacenter
- Do not make all nodes seeds

### 23. Phi Accrual Failure Detection

**Purpose**: Adaptive failure detection without timeouts

**Traditional Heartbeats Problem**:
- Fixed timeout either too slow or too aggressive
- Network variance causes false positives/timeouts

**Phi Accrual Solution**:
- Track heartbeat inter-arrival times
- Model distribution (typically normal)
- Calculate suspicion level (phi)
- φ = 1 → 10% chance of failure
- φ = 2 → 1% chance
- φ = 3 → 0.1% chance

**Advantages**:
- Adapts to network conditions automatically
- No manual timeout tuning
- Provides confidence level, not binary decision
- Handles network jitter gracefully

### 24. Gossiping-Only Nodes

**Purpose**: Special nodes that coordinate but don't store data

**Configuration**:
```yaml
# cassandra.yaml
cassandra.join_ring=false  # Don't participate in token ring
cassandra.is_gossip_only=true  # Gossip only, no data
```

**Use Cases**:
- Lightweight coordinators for client connections
- Query routers without storage overhead
- Isolated control plane nodes

**Important**: Gossip-only nodes don't count toward replication factor

### 25. Snitch

**Purpose**: Topology awareness and proximity determination

**Snitch Types**:

| Snitch | Behavior | Use Case |
|--------|----------|----------|
| **SimpleSnitch** | Treats all nodes as local | Single DC |
| **GossipingPropertyFileSnitch** (default) | Reads from file, gossips information | Production standard |
| **Ec2Snitch** | AWS region/zone aware | AWS deployments |
| **Ec2MultiRegionSnitch** | Cross-region AWS | Multi-region AWS |
| **GoogleCloudSnitch** | GCP aware | GCP deployments |
| **AzureSnitch** | Azure aware | Azure deployments |

**Snitch Functions**:
- Maps IPs to datacenter/rack
- Determines replica placement
- Influences read/write routing (prefer local replicas)
- Reports topology to gossip

**Dynamic Snitching**:
- Monitors read latency per replica
- Prefers faster replicas for reads
- Avoids slow or overloaded nodes
- Self-tuning over time

---

## Lightweight Transactions (Paxos)

### 26. Paxos Consensus for Compare-and-Set

**Purpose**: Linearizable consistency for single-partition operations

**What LWTs Provide**:
- Compare-and-set (CAS) operations
- "INSERT IF NOT EXISTS"
- "UPDATE IF condition"
- Linearizable reads within single partition

**Paxos Phases**:

| Phase | Purpose | Messages |
|-------|---------|----------|
| **Prepare/Promise** | Propose value, get promise not to accept earlier proposals | Prepare, Promise |
| **Accept/Accepted** | Propose actual value | Accept, Accepted |
| **Commit** | Learn committed value | Commit |

**LWT Syntax**:
```cql
-- Insert if not exists (atomic)
INSERT INTO users (user_id, email) 
VALUES ('123', 'john@example.com') 
IF NOT EXISTS;

-- Conditional update
UPDATE accounts 
SET balance = balance - 100 
WHERE user_id = '123' 
IF balance >= 100;

-- Compare and set
UPDATE inventory 
SET quantity = 45 
WHERE product_id = 'ABC' 
IF quantity = 50;
```

**Limitations**:
- **Single partition only** (no cross-partition transactions)
- **Performance penalty** (4-10x slower than regular writes)
- **Not for high-throughput operations**
- **Consistency level must be SERIAL**

**Performance Comparison**:
```
Regular write (CL=QUORUM): ~1ms
LWT CAS operation: ~5-10ms
```

**When to Use LWTs**:
- Account balances (avoid overdraft)
- Unique constraint enforcement
- Inventory management (avoid overselling)
- Leader election (distributed locks)

**When NOT to Use LWTs**:
- High-volume counters
- Log/timeseries data
- De-duplication with acceptable duplicates

---

## Compaction Strategies

### 27. Size-Tiered Compaction Strategy (STCS)

**Purpose**: Merge similar-sized SSTables; optimize for write-heavy workloads

**How STCS Works**:
- Trigger: Min number of SSTables of similar size (default 4)
- Merges SSTables together
- Creates new larger SSTable
- Discards old SSTables after merge

**Characteristics**:
| Aspect | Value |
|--------|-------|
| Write amplification | Lower (2-3x) |
| Read amplification | Higher (many SSTables) |
| Space amplification | Higher (temporary during compaction) |
| Suitable for | Write-heavy, infrequently read |

**Configuration**:
```cql
CREATE TABLE my_table (id int PRIMARY KEY, value text)
WITH compaction = {
    'class': 'SizeTieredCompactionStrategy',
    'min_threshold': 4,
    'max_threshold': 32,
    'bucket_high': 1.5,
    'bucket_low': 0.5
};
```

### 28. Leveled Compaction Strategy (LCS)

**Purpose**: Maintain SSTables in size tiers; optimize for reads

**How LCS Works**:
- Level 0: Multiple small SSTables (just flushed)
- Level 1: 10 SSTables of ~160MB each
- Level 2: 100 SSTables of ~160MB each
- Level N: 10^N × SSTables
- Data demoted to next level when level is full

**Characteristics**:
| Aspect | Value |
|--------|-------|
| Write amplification | Higher (10-20x) |
| Read amplification | Very low (1-2 SSTables) |
| Space amplification | Lower (~10%) |
| Suitable for | Read-heavy, mixed workloads |

**Configuration**:
```cql
CREATE TABLE my_table (id int PRIMARY KEY, value text)
WITH compaction = {
    'class': 'LeveledCompactionStrategy',
    'sstable_size_in_mb': 160,
    'fanout_size': 10
};
```

### 29. Time Window Compaction Strategy (TWCS)

**Purpose**: Time-based partitioning; optimize for time-series data

**How TWCS Works**:
- SSTables grouped by time window (hour, day, week)
- Within window: Size-tiered compaction
- Across windows: No compaction
- Entire window dropped when expired

**Characteristics**:
| Aspect | Value |
|--------|-------|
| Write amplification | Low within window |
| Read amplification | Depends on query pattern |
| Delete efficiency | Excellent (drop entire window) |
| Suitable for | Time-series, logs, metrics |

**Configuration**:
```cql
CREATE TABLE metrics (date date, time time, value int, PRIMARY KEY(date, time))
WITH compaction = {
    'class': 'TimeWindowCompactionStrategy',
    'compaction_window_unit': 'DAYS',
    'compaction_window_size': 7
};
```

### 30. Compaction Tuning Parameters

**Common Parameters**:

| Parameter | Purpose | Default |
|-----------|---------|---------|
| `min_threshold` | Min SSTables to trigger compaction (STCS) | 4 |
| `max_threshold` | Max SSTables before forcing (STCS) | 32 |
| `sstable_size_in_mb` | Target SSTable size (LCS) | 160 |
| `tombstone_threshold` | % of tombstones to trigger compaction | 0.2 |
| `unchecked_tombstone_compaction` | Always compact for tombstones | false |

**Compaction Decisions**:
```
Compaction triggered when:
1. Number of SSTables ≥ min_threshold AND sizes are similar
2. Tombstone ratio > tombstone_threshold
3. Forced by nodetool compact
4. Time window expires (TWCS)
```

---

## Caching Mechanisms

### 31. Key Cache

**Purpose**: Cache partition key locations in SSTables

**How Key Cache Works**:
- Stores mapping: partition_key → position in SSTable
- Located in JVM heap
- Enables O(1) lookup of SSTable position
- Avoids binary search in partition index

**Configuration**:
```yaml
# cassandra.yaml
key_cache_size_in_mb: 100          # Default: 100MB
key_cache_save_period: 14400       # Save every 4 hours (seconds)
```

**Effectiveness**:
- 90%+ hit rate for working set
- Reduces disk seeks by 1-2 per read
- Most effective for random reads

### 32. Row Cache

**Purpose**: Cache entire rows in off-heap memory

**How Row Cache Works**:
- Stores complete rows after read
- Located off-heap (reduces GC pressure)
- Subsequent reads served entirely from memory
- No need to read SSTables

**Configuration**:
```yaml
# cassandra.yaml
row_cache_size_in_mb: 0             # Default: 0 (disabled)
row_cache_save_period: 0            # Default: 0 (disabled)
row_cache_class_name: org.apache.cassandra.cache.OHCProvider
```

**When to Enable Row Cache**:
- Frequently accessed small rows
- Read-mostly tables
- Stale data acceptable (cache invalidation on write)

**When NOT to Enable**:
- Memory-constrained environments
- Write-heavy tables (cache invalidated frequently)
- Large rows (blow memory)

### 33. Counter Cache

**Purpose**: Optimize counter read performance

**How Counter Cache Works**:
- Caches frequently accessed counter values
- Reduces lock contention for hot counters
- Off-heap storage

**Use Case**:
- Social media like counts
- Page view counters
- Any increment/decrement operation

---

## Anti-Entropy & Repair

### 34. Merkle Trees for Anti-Entropy

**Purpose**: Efficiently detect data inconsistencies between replicas

**What is a Merkle Tree**:
- Binary hash tree of data partitions
- Leaf nodes: Hash of individual rows
- Internal nodes: Hash of child nodes
- Root: Single hash representing entire data set

**How Anti-Entropy Works**:
1. Two nodes exchange Merkle tree roots
2. If roots match: Data is consistent
3. If mismatch: Binary search down tree
4. Identify exactly which partitions differ
5. Exchange only differing data

**Merkle Tree Visualization**:
```
                    Root
                   /    \
              HashA      HashB
             /    \      /    \
         Hash1  Hash2  Hash3  Hash4
          |      |      |      |
        Row1   Row2   Row3   Row4
```

**Properties**:
- O(log N) comparison time
- Minimal data transfer (only differing parts)
- Compute-intensive (CPU cost of hashing)
- Used in repair operations

### 35. Repair Types

**Purpose**: Manually trigger anti-entropy to fix inconsistencies

**Sequential Repair**:
```
nodetool repair -seq my_keyspace
```
- Repairs one table at a time
- Lower resource usage
- Slower overall

**Parallel Repair**:
```
nodetool repair -pr my_keyspace
```
- Repairs multiple tables simultaneously
- Higher resource usage
- Faster completion

**Incremental Repair**:
```
nodetool repair -inc my_keyspace
```
- Repairs only unrepaired data since last repair
- More efficient for large datasets
- Recommended for production

**Repair Frequency Best Practices**:

| Data Type | Repair Frequency |
|-----------|------------------|
| Critical (finance) | Daily |
| Operational (user data) | Weekly |
| Time-series (logs) | Monthly or never |
| Temporary (sessions) | Never (let TTL handle) |

---

## Tombstones & Deletion

### 36. Tombstones

**Purpose**: Deletion markers for immutable SSTables

**Problem**:
SSTables are immutable → cannot delete data in place.

**Solution - Tombstones**:
- Delete operation writes a tombstone (deletion marker)
- Tombstone includes: key, timestamp, optional columns
- Reads skip data with tombstone timestamp
- Compaction physically removes tombstoned data

**Tombstone Lifecycle**:
```
1. Write tombstone (inserts marker)
2. Reads see tombstone, ignore deleted data
3. Compaction: Tombstone + older data → data removed
4. Tombstone alone → removed after gc_grace_seconds
```

**Tombstone Example**:
```cql
DELETE FROM users WHERE user_id = '123';
-- Actually writes tombstone for user 123
```

### 37. GC Grace Seconds

**Purpose**: Prevent resurrection of deleted data

**Problem**:
- Node down during deletion
- Tombstone not propagated
- Node returns with old data
- Old data appears as "new" (data resurrection)

**Solution - GC Grace Seconds**:
```
gc_grace_seconds: 864000  # Default: 10 days
```
- Tombstone kept for this duration
- Enough time for all nodes to receive tombstone
- After grace: Tombstone removed in compaction
- After grace: Deleted data permanently gone

**Guidelines**:
- Never set to 0 (data resurrection risk)
- Increase for large clusters (more time to propagate)
- Decrease if frequent tombstones cause read performance issues

---

## Complete System Interaction Diagram

```
┌─────────────────────────────────────────────────────────────────────────────┐
│                          CLIENT LAYER                                        │
│  ┌──────────────┐  ┌──────────────┐  ┌──────────────┐                       │
│  │  CQL Driver  │  │  Thrift API  │  │  Smart Driver│                       │
│  │  (Datastax)  │  │  (Legacy)    │  │  (Token aware)│                      │
│  └──────────────┘  └──────────────┘  └──────────────┘                       │
│         │                 │                 │                               │
│         └─────────────────┴─────────────────┘                               │
│                           │                                                  │
└───────────────────────────┼──────────────────────────────────────────────────┘
                            │ (connect to any node)
                            ▼
┌─────────────────────────────────────────────────────────────────────────────┐
│                    COORDINATOR NODE (Any node)                               │
│  ┌─────────────────────────────────────────────────────────────────────┐    │
│  │ Request Router                                                        │    │
│  │ • Parse CQL                                                           │    │
│  │ • Calculate token: hash(partition_key)                               │    │
│  │ • Identify replicas (TokenMetadata)                                   │    │
│  │ • Route to replicas                                                   │    │
│  └─────────────────────────────────────────────────────────────────────┘    │
│                                                                              │
│  Consistency Manager:                                                        │
│  • Track acknowledgments                                                    │
│  • Enforce consistency level                                                │
│  • Manage read repairs                                                      │
│  • Handle speculative retries                                               │
└───────────────────────────────────┬─────────────────────────────────────────┘
                                    │
                                    ▼
┌─────────────────────────────────────────────────────────────────────────────┐
│                         REPLICA NODE                                         │
│                                                                              │
│  ┌─────────────────────────────────────────────────────────────────────┐    │
│  │ WRITE PATH                                                             │    │
│  │                                                                        │    │
│  │   Write ──► CommitLog (sequential) ──► Memtable ──► [Async] Flush    │    │
│  │   Request     (durability)           (sorted)         to SSTable      │    │
│  │                                                                        │    │
│  └─────────────────────────────────────────────────────────────────────┘    │
│                                                                              │
│  ┌─────────────────────────────────────────────────────────────────────┐    │
│  │ READ PATH                                                              │    │
│  │                                                                        │    │
│  │   Read Request ──┬─► Row Cache (if enabled)                          │    │
│  │                   ├─► Memtable (in-memory)                           │    │
│  │                   └─► SSTable (disk)                                 │    │
│  │                         ├─► Bloom Filter (skip if definitely absent)  │    │
│  │                         ├─► Key Cache (position in SSTable)          │    │
│  │                         ├─► Partition Summary (sampled index)        │    │
│  │                         ├─► Partition Index (full index)             │    │
│  │                         └─► Data.db (actual data)                    │    │
│  │                                                                        │    │
│  └─────────────────────────────────────────────────────────────────────┘    │
│                                                                              │
│  ┌─────────────────────────────────────────────────────────────────────┐    │
│  │ STORAGE ENGINE                                                         │    │
│  │                                                                        │    │
│  │   ┌─────────┐    ┌─────────┐    ┌─────────┐    ┌─────────┐          │    │
│  │   │Memtable │───►│SSTable  │───►│SSTable  │───►│SSTable  │          │    │
│  │   │(64 MB)  │    │(L0)     │    │(L1)     │    │(L2)     │          │    │
│  │   └─────────┘    └─────────┘    └─────────┘    └─────────┘          │    │
│  │                         │              │              │                │    │
│  │                         └──────────────┼──────────────┘                │    │
│  │                                        │                               │    │
│  │                              Compaction (merged)                       │    │
│  │                                                                        │    │
│  └─────────────────────────────────────────────────────────────────────┘    │
└─────────────────────────────────────────────────────────────────────────────┘
                                    │
                                    ▼
┌─────────────────────────────────────────────────────────────────────────────┐
│                    CLUSTER MEMBERSHIP & GOSSIP                               │
│                                                                              │
│  ┌─────────────────────────────────────────────────────────────────────┐    │
│  │                        TOKEN RING                                      │    │
│  │                                                                        │    │
│  │                         ┌───────┐                                      │    │
│  │                   ┌─────┤ Node A├─────┐                                │    │
│  │                   │     │range0-25│    │                               │    │
│  │                   │     └───────┘    │                                │    │
│  │                ┌──┴──┐            ┌──┴──┐                             │    │
│  │                │Node D│            │Node B│                             │    │
│  │                │range75-99│        │range25-50│                        │    │
│  │                └──┬──┘            └──┬──┘                             │    │
│  │                   │     ┌───────┐    │                                │    │
│  │                   └─────┤ Node C├─────┘                                │    │
│  │                         │range50-75│                                   │    │
│  │                         └───────┘                                      │    │
│  │                                                                        │    │
│  │   Vnode distribution: Each physical node has 256 vnodes                │    │
│  │                                                                        │    │
│  └─────────────────────────────────────────────────────────────────────┘    │
│                                                                              │
│  ┌─────────────────────────────────────────────────────────────────────┐    │
│  │                    GOSSIP PROTOCOL                                    │    │
│  │                                                                        │    │
│  │   Node A ──Gossip──► Node B (every second, 1-3 nodes)                │    │
│  │     │                                                                  │    │
│  │     ├── HeartbeatState (version, timestamp)                           │    │
│  │     ├── ApplicationState (load, schema, tokens)                       │    │
│  │     └── EndpointState (status, rack, DC)                              │    │
│  │                                                                        │    │
│  │   Phi Accrual Failure Detection: φ = 1 (10% failure suspicion)        │    │
│  │                                                                        │    │
│  └─────────────────────────────────────────────────────────────────────┘    │
│                                                                              │
│  ┌─────────────────────────────────────────────────────────────────────┐    │
│  │                  REPLICATION & CONSISTENCY                            │    │
│  │                                                                        │    │
│  │   RF=3 (NetworkTopologyStrategy)                                      │    │
│  │   DC1=3, DC2=2                                                        │    │
│  │                                                                        │    │
│  │   Consistency Levels:                                                 │    │
│  │   • QUORUM = floor(3/2)+1 = 2 (for DC1)                              │    │
│  │   • LOCAL_QUORUM = 2 (local only)                                    │    │
│  │   • EACH_QUORUM = 2 (both DCs)                                       │    │
│  │                                                                        │    │
│  │   R+W > RF for strong consistency: 2+2=4>3 ✓                         │    │
│  │                                                                        │    │
│  └─────────────────────────────────────────────────────────────────────┘    │
└─────────────────────────────────────────────────────────────────────────────┘
```

---

## Algorithm Summary Table

| # | Algorithm/Concept | Primary Purpose | Cassandra Component |
|---|------------------|-----------------|---------------------|
| 1 | Peer-to-Peer Architecture | No single point of failure | Entire system |
| 2 | Consistent Hashing | Data distribution | Partitioner |
| 3 | Token Ring | Logical data placement | TokenMetadata |
| 4 | Virtual Nodes (Vnodes) | Fine-grained distribution | num_tokens |
| 5 | Murmur3 Hash | Fast, even distribution | Murmur3Partitioner |
| 6 | LSM-Tree | Write-optimized storage | Storage engine |
| 7 | Commit Log | Write durability | CommitLog |
| 8 | Memtable | In-memory write buffer | Memtable |
| 9 | SSTable | Immutable disk format | SSTable |
| 10 | Bloom Filter | Probabilistic membership | SSTable Filter.db |
| 11 | Hinted Handoff | Availability during outages | HintedHandoff |
| 12 | Read Repair | Fix inconsistencies on read | ReadRepair |
| 13 | Speculative Retry | Reduce tail latency | ReadCoordinator |
| 14 | SimpleStrategy | Basic replication | ReplicationStrategy |
| 15 | NetworkTopologyStrategy | DC-aware replication | ReplicationStrategy |
| 16 | Tunable Consistency | Consistency vs. availability trade-off | ConsistencyLevel |
| 17 | Gossip Protocol | Cluster state propagation | Gossiper |
| 18 | Phi Accrual Detection | Adaptive failure detection | FailureDetector |
| 19 | Gossiping-Only Nodes | Coordinators without data | join_ring=false |
| 20 | Dynamic Snitching | Adaptive read routing | DynamicEndpointSnitch |
| 21 | Paxos Consensus | Lightweight transactions | LWT (CAS) |
| 22 | Size-Tiered Compaction | Write-optimized | STCS |
| 23 | Leveled Compaction | Read-optimized | LCS |
| 24 | Time Window Compaction | Time-series optimized | TWCS |
| 25 | Key Cache | Partition-to-position mapping | KeyCache |
| 26 | Row Cache | Complete row caching | RowCache |
| 27 | Counter Cache | Hot counter optimization | CounterCache |
| 28 | Merkle Trees | Anti-entropy comparison | Repair |
| 29 | Tombstones | Deletion markers | Deletion |
| 30 | GC Grace Seconds | Prevent data resurrection | gc_grace_seconds |

---

## Source Code Reference

| Component | Source Path (Cassandra GitHub) |
|-----------|-------------------------------|
| Partitioner | `src/java/org/apache/cassandra/dht/` |
| Storage Engine | `src/java/org/apache/cassandra/db/` |
| Memtable | `src/java/org/apache/cassandra/db/memtable/` |
| SSTable | `src/java/org/apache/cassandra/io/sstable/` |
| Compaction | `src/java/org/apache/cassandra/db/compaction/` |
| Gossip | `src/java/org/apache/cassandra/gms/` |
| Hinted Handoff | `src/java/org/apache/cassandra/hints/` |
| Read Repair | `src/java/org/apache/cassandra/service/reads/` |
| Paxos | `src/java/org/apache/cassandra/service/paxos/` |
| CassandraYaml | `src/java/org/apache/cassandra/config/` |

---

## Configuration Reference

### cassandra.yaml (Key Settings)

```yaml
# Distribution
num_tokens: 256
partitioner: org.apache.cassandra.dht.Murmur3Partitioner

# Storage
commitlog_sync: periodic
commitlog_sync_period_in_ms: 10000
commitlog_segment_size_in_mb: 32

# Memory
memtable_heap_space_in_mb: 2048
memtable_offheap_space_in_mb: 2048

# Caching
key_cache_size_in_mb: 100
row_cache_size_in_mb: 0
counter_cache_size_in_mb: 50

# Compaction
concurrent_compactors: 2
compaction_throughput_mb_per_sec: 64

# Failure Detection
phi_convict_threshold: 8  # φ threshold for marking down
fd_max_interval_ms: 30000

# Hinted Handoff
max_hint_window_in_ms: 10800000  # 3 hours
hinted_handoff_enabled: true

# Repair
incremental_repair: false
repair_session_max_tree_depth: 18
```

### Table-Level Settings

```cql
CREATE TABLE my_table (
    id int PRIMARY KEY,
    value text
) WITH 
    bloom_filter_fp_chance = 0.01,
    caching = {'keys': 'ALL', 'rows_per_partition': '100'},
    compaction = {'class': 'SizeTieredCompactionStrategy', 
                  'min_threshold': 4},
    compression = {'class': 'LZ4Compressor'},
    gc_grace_seconds = 864000,
    default_time_to_live = 0,
    memtable_flush_period_in_ms = 0;
```

---

## Performance & Complexity Reference

| Operation | Complexity | Typical Latency |
|-----------|------------|-----------------|
| Write (CL=ONE) | O(1) + sequential I/O | <1 ms |
| Write (CL=QUORUM) | O(RF) network | 1-3 ms |
| Read (CL=ONE) | O(log N) + Bloom filter | 1-2 ms |
| Read (CL=QUORUM) | O(RF) × O(log N) | 3-10 ms |
| LWT (Paxos) | 4-8 round trips | 5-15 ms |
| Range Scan | O(partition count) + O(N) | Variable |

---

## Conclusion

Cassandra's design philosophy emphasizes:

- **Availability over consistency** (AP by default, tunable to CP)
- **Write-optimized storage** (LSM-trees for sequential I/O)
- **Masterless architecture** (no single point of failure)
- **Operational simplicity** (add nodes without rebalancing)
- **Cross-datacenter readiness** (built-in replication topologies)

Key innovations include:
- **Vnodes**: Fine-grained distribution enabling heterogeneous clusters
- **Phi Accrual**: Adaptive failure detection without magic numbers
- **Hinted Handoff + Read Repair**: Multiple mechanisms for consistency
- **Tunable Consistency**: Per-operation trade-offs
- **Merkle Trees**: Efficient anti-entropy comparison
- **Multiple Compaction Strategies**: Workload-specific optimization

This combination of algorithms makes Cassandra ideal for:
- Write-heavy applications (time-series, logs, metrics)
- Geographic distribution (multi-datacenter active-active)
- High availability requirements (99.99%+ uptime)
- Linear scalability (add nodes without downtime)
- Large data volumes (petabytes across thousands of nodes)
