# Redis: Complete Distributed Systems Algorithms & Concepts Reference

## Document Overview

This document provides a comprehensive analysis of Redis's architectural patterns, algorithms, and distributed systems concepts. Redis is an in-memory data structure store that functions as a database, cache, and message broker. Its performance (100k+ QPS) derives from elegant algorithms and data structures optimized for memory efficiency and single-threaded execution.

---

## Table of Contents

1. [Core Data Structures](#core-data-structures)
2. [Memory Optimization Structures](#memory-optimization-structures)
3. [Cache Eviction & Expiration](#cache-eviction--expiration)
4. [Persistence Algorithms](#persistence-algorithms)
5. [Replication & High Availability](#replication--high-availability)
6. [Redis Cluster (Distributed Sharding)](#redis-cluster-distributed-sharding)
7. [Sentinel (Failover Coordination)](#sentinel-failover-coordination)
8. [Messaging & Stream Processing](#messaging--stream-processing)
9. [Probabilistic Data Structures](#probabilistic-data-structures-redis-stack)
10. [Network & Event Model](#network--event-model)
11. [Complete System Interaction Diagram](#complete-system-interaction-diagram)

---

## Core Data Structures

### 1. Hash Tables (Dictionary)

**Purpose**: O(1) key-value lookups for database, keyspace, and hash objects

**Implementation**:
Redis uses a hash table with the following structure :
- **Array of buckets**: Each bucket contains a pointer to a linked list of entries
- **Entry structure**: Contains key, value, and pointer to next entry
- **Collision resolution**: Chaining (linked lists per bucket)

**Rehashing Mechanism**:
When the load factor exceeds a threshold (typically 1 or 5 depending on state), Redis triggers rehashing :
- Creates a new hash table with double the current size
- Performs **incremental rehashing** to avoid blocking

**Time Complexity**:
- Insert/Delete/Lookup: O(1) average, O(N) worst-case (rare)

---

### 2. Skip Lists (Sorted Sets - ZSET)

**Purpose**: Ordered indexing for sorted sets with efficient range queries

**Structure** :
A skip list is a linked list enhanced with **multiple levels of indexing**:

```
Level 4:  1 ----------------------------> 9
Level 3:  1 -----------> 5 -----------> 9
Level 2:  1 --> 3 --> 5 --> 7 --> 9
Level 1:  1->2->3->4->5->6->7->8->9
```

**Key Properties** :
- **Time complexity**: O(log N) for search, insert, delete
- **Space complexity**: O(N) average (tunable via node height probability)
- **Dynamic balance**: Uses randomization (not strict balancing)

**Why Skip Lists Over Red-Black Trees** :
- **Range queries**: Skip lists naturally support efficient range scans (O(log N + M)) without parent pointer overhead
- **Implementation simplicity**: Skip lists are easier to implement and maintain
- **Memory flexibility**: Can tune space vs. performance by adjusting node height probability (typically 1/4)

**Index Maintenance** :
When inserting a new node, Redis generates a random "height" (k) for the node (1-32 levels) using a power-law distribution. This node is inserted into all k index levels, maintaining "balance" without complex rotations.

**Used For**: ZSET operations like ZRANGE, ZRANK, ZSCORE

---

### 3. Linked Lists

**Purpose**: Queue and list operations (LPUSH, RPUSH, LPOP, RPOP)

**Implementation**:
Redis lists before 3.2 used a doubly-linked list structure:
- Each node contains prev/next pointers and a void* value
- O(1) push/pop at both ends
- O(N) index access

**Current Approach**: Replaced by QuickList (see below)

**Used For**: List data type operations

---

### 4. QuickList

**Purpose**: Hybrid structure combining linked list + ziplist for memory efficiency

**Introduced**: Redis 3.2 

**Structure** :
```
QuickList (linked list of QuickListNodes)
    │
    ├── QuickListNode 1 → ziplist (8KB max)
    ├── QuickListNode 2 → ziplist (8KB max)
    └── QuickListNode N → ziplist (8KB max)
```

**Design Rationale** :
- **Problem**: Simple linked lists waste memory on node overhead (prev/next pointers)
- **Problem**: ziplist causes "cascading update" issues on modification
- **Solution**: Combine both - multiple small ziplists linked together

**Memory Improvements** :
- For 1M lists with 5 items (10 chars each): Memory reduced from 214MB to 138MB (**35% reduction**)
- QuickList node overhead: ~80 bytes per node saved

**Conversion Rules** :
| Condition | Action |
|-----------|--------|
| ziplist reaches `list-max-listpack-size` limit | Convert to QuickList |
| QuickList has 1 node size < half the limit | Convert back to listpack |

**Performance** :
- LRANGE: ~3-13% improvement over pure linked lists
- Memory savings: 35% for typical workloads

---

### 5. ListPack

**Purpose**: Compact memory encoding replacing ziplist (solves cascading update)

**Introduced**: Redis 5.0 

**Problem with ziplist** :
Each ziplist entry stores `prevlen` (length of previous entry). When an entry changes size, all subsequent entries must update their `prevlen` field - causing **cascading updates** (O(N^2) worst-case).

**ListPack Solution** :
```
ListPack entry structure:
┌──────────────────────────────────┐
│ encoding (1-5 bytes)             │  ← Type of data
│ data (variable)                   │  ← Actual value
│ len (1-5 bytes)                   │  ← Length of encoding+data
└──────────────────────────────────┘
```

**Key Innovation**:
- **No prevlen field** - each entry only stores its OWN length 
- New entries don't affect subsequent entries
- **Cascading update eliminated completely**

**Memory Layout**:
```
[total_bytes] [element_count] [entry1] [entry2] ... [entryN] [end_marker]
```

**When Used**: Hash, ZSet, and List when element count is small (configurable thresholds)

---

## Memory Optimization Structures

### 6. ziplist (Legacy - superseded by listpack)

**Purpose**: Compact sequential storage for small lists/hashes/zsets

**Structure** :
```
[zlbytes][zltail][zllen][entry1][entry2]...[entryN][zlend]
```

**Each Entry**:
```
[prevlen (1-5 bytes)][encoding][data]
```

**Limitations** :
- **Cascading update**: Changing entry size forces subsequent entries to update prevlen
- **Slow middle access**: Must traverse sequentially
- **Replaced by listpack** in Redis 5.0+

**Still used**: For backward compatibility and some small-object optimizations

---

## Cache Eviction & Expiration

Redis provides eight eviction policies when memory limit is reached :

### 7. LRU (Least Recently Used)

**Purpose**: Keep most recently accessed keys, evict least recently used

**Policies** :
| Policy | Scope | Description |
|--------|-------|-------------|
| `allkeys-lru` | All keys | Evict LRU regardless of TTL (Default for caching) |
| `volatile-lru` | Keys with TTL | Only evict from keys with expiration set |

**How It Works**:
- Redis does NOT maintain exact LRU (would cost memory)
- **Approximate LRU**: Samples `maxmemory-samples` (default 5) keys, evicts oldest among them
- Each key stores an **LRU clock** (last access timestamp, 24-bit resolution)

---

### 8. LFU (Least Frequently Used)

**Purpose**: Keep frequently accessed keys, evict least frequently used

**Introduced**: Redis 4.0

**Policies** :
| Policy | Scope | Description |
|--------|-------|-------------|
| `allkeys-lfu` | All keys | Evict least frequently used |
| `volatile-lfu` | Keys with TTL | Only evict from keys with expiration |

**Algorithm**:
- Each key maintains a **logarithmic counter** (0-255)
- Counter increments on access (with probability based on current value)
- Counter decays over time (half-life decay)

**Why LFU over LRU**:
- LRU can be fooled by one-time scan of many keys
- LFU captures **access frequency**, not just recency

---

### 9. Random Eviction

**Purpose**: Simple eviction without tracking

**Policies** :
| Policy | Scope | Description |
|--------|-------|-------------|
| `allkeys-random` | All keys | Randomly evict any key |
| `volatile-random` | Keys with TTL | Randomly evict from expiring keys |

**Use Case**: When access pattern is uniform and tracking overhead is unwanted

---

### 10. volatile-ttl

**Purpose**: Evict keys with shortest remaining TTL first

**Policy**: `volatile-ttl` 

**How It Works**:
- Only considers keys with expiration set
- Evicts key with smallest `ttl` (time-to-live remaining)
- Useful for time-sensitive data

---

### 11. noeviction

**Purpose**: Reject writes when memory limit reached

**Policy**: `noeviction` 

**Behavior**:
- Writes return error when maxmemory exceeded
- Reads and deletes continue working
- Default for some managed services

**Active-Active Considerations** :
- In Active-Active databases, eviction starts at **80% memory usage** to allow propagation across regions
- Eviction rate increases if usage continues rising

---

### 12. TTL Expiration

**Purpose**: Time-based key lifecycle management

**Two Expiration Mechanisms** [citation:1, 6]:

| Mechanism | Type | Description |
|-----------|------|-------------|
| **Active Expiration** | Background | Redis tests 20 random keys with TTL every 100ms; deletes expired ones |
| **Passive Expiration** | On-access | When key is accessed, Redis checks TTL and deletes if expired |

**Expiration Commands**:
```bash
EXPIRE key 60        # Expire in 60 seconds
SETEX key 60 value   # Set with expiration
PEXPIRE key 60000    # Expire in milliseconds
```

**Memory Impact**:
- Expired keys count toward memory usage until deleted
- Active expiration ensures eventual cleanup without access requirement

---

## Persistence Algorithms

### 13. Append Only File (AOF)

**Purpose**: Command-log persistence recording every write operation

**How It Works**:
- Every write command (SET, HSET, etc.) appended to AOF file
- On restart, Redis replays all commands to rebuild dataset
- Three fsync policies :

| Policy | Description | Trade-off |
|--------|-------------|-----------|
| `appendfsync always` | fsync after every write | Safest, slowest |
| `appendfsync everysec` | fsync once per second | **Default** - good balance |
| `appendfsync no` | OS handles fsync | Fastest, potential data loss |

**AOF File Format**:
```
*3\r\n$3\r\nSET\r\n$3\r\nkey\r\n$5\r\nvalue\r\n
*2\r\n$3\r\nDEL\r\n$3\r\nkey\r\n
```

---

### 14. AOF Rewrite

**Purpose**: Log compaction - remove redundant commands

**Problem**: AOF grows indefinitely with repeated updates to same keys

**Rewrite Process** :
1. Fork a child process (using copy-on-write)
2. Child reads current database state
3. Child writes minimal command set to new AOF file
4. Parent continues serving requests (buffering new writes)
5. After child finishes, parent appends buffered writes to new AOF
6. Atomic rename replaces old AOF

**Example Compression**:
```
Original AOF: SET key1 1, SET key1 2, SET key1 3
Rewritten:    SET key1 3  (only final value)
```

**Replication Offset Concern** :
When AOF rewrite occurs on a replica, the replication offset must be preserved to allow partial resynchronization (PSYNC). If not saved, replica may need FULLSYNC instead of efficient PSYNC.

---

### 15. RDB Snapshots

**Purpose**: Point-in-time binary dump of entire dataset

**How It Works**:
- Child process created via fork()
- Child writes memory contents to RDB file
- Uses **copy-on-write** (COW) to avoid copying all memory

**Trigger Conditions**:
```conf
save 900 1      # Save after 900 sec if at least 1 key changed
save 300 10     # Save after 300 sec if at least 10 keys changed  
save 60 10000   # Save after 60 sec if at least 10000 keys changed
```

**RDB vs. AOF**:

| Aspect | RDB | AOF |
|--------|-----|-----|
| Recovery speed | Fast (binary load) | Slow (command replay) |
| Data loss window | Last snapshot | Up to 1 sec (everysec) |
| File size | Compressed | Larger |
| Debugging | Binary, not readable | Human-readable commands |

**Best Practice**: Use both - RDB for backups, AOF for durability

---

### 16. Copy-on-Write (CoW)

**Purpose**: Efficient snapshot creation without blocking writes

**How It Works** :
When parent process calls `fork()`:
- Child inherits parent's memory pages (no actual copy)
- Pages are marked **copy-on-write**
- When parent modifies a page, kernel copies that page before write
- Child continues seeing original page

**Memory Impact**:
- Initially: ~0 extra memory (just page table copies)
- During writes: Only modified pages are copied
- Typically 2-4x memory increase during heavy writes

**Used For**:
- RDB snapshots
- AOF rewrite (child process)
- Background saving without service interruption

---

## Replication & High Availability

### 17. Master-Replica Replication

**Purpose**: High availability and read scaling

**Basic Flow** :
1. Replica connects to master and sends `PSYNC`
2. Master starts **background save** (RDB) while continuing to serve clients
3. Master sends RDB file to replica
4. Master buffers all write commands during transmission
5. Replica loads RDB, then replays buffered commands

---

### 18. Partial Resynchronization (PSYNC)

**Purpose**: Efficient replica recovery after disconnection

**Introduced**: Redis 2.8 (replaces SYNC for reconnections)

**Components** :
| Component | Purpose |
|-----------|---------|
| **Replication ID** (repl-id) | Identifies master's replication history |
| **Replication Offset** | Byte position in replication stream |
| **Replication Backlog** | Ring buffer of recent commands (configurable size) |

**PSYNC Flow** :
```
Replica: "PSYNC <repl-id> <offset>"
Master: 
  - If repl-id matches and offset exists in backlog → CONTINUE
  - Else → FULLSYNC (send RDB)
```

**When FULLSYNC Happens** :
- First-time connection
- Replication ID changed (master reboot/failover)
- Offset out of backlog range
- Corruption or configuration change

---

### 19. Replication Backlog

**Purpose**: Ring buffer storing recent commands for PSYNC

**Configuration**:
```conf
repl-backlog-size = 1MB  # Default, increase for busy clusters
repl-backlog-ttl = 3600  # Seconds to keep backlog after no replicas
```

**How It Works**:
- Circular buffer in master memory
- Commands appended with increasing offset
- Oldest commands overwritten when full

**Sizing Rule**: Backlog should hold >= replica reconnect time worth of writes

---

### 20. Replication Offsets

**Purpose**: Synchronization tracking between master and replicas

**Properties**:
- Monotonically increasing integer per replication stream
- Each command adds its byte length to offset
- Master and replica maintain their own offsets
- PSYNC uses offsets to request missing commands

**Replication ID** :
- Unique identifier for master's replication history
- Changes on: Restart, promotion to master, `REPLICAOF NO ONE`
- Used to detect if replica is from same history

**Safety Consideration** :
When AOF rewrite occurs, the `repl-id` and `repl-offset` should be persisted in the RDB portion of the AOF. Without this, a replica that restarts may request a FULLSYNC instead of a more efficient PSYNC.

---

## Redis Cluster (Distributed Sharding)

### 21. Hash Slots (16384 Slots)

**Purpose**: Data sharding across cluster nodes

**Total Slots**: 16,384 (0-16383) 

**Why 16384?** :
- CRC16 generates 16-bit hash (0-65535)
- Need even distribution, 16384 is power of 2 (2^14)
- Cluster heartbeat messages include slot bitmap (2KB for 16384 slots)
- Larger bitmap would increase network overhead

**Slot Assignment**:
- Each master node responsible for one or more contiguous slot ranges
- Slots are rebalanced during cluster expansion/contraction

---

### 22. CRC16 Hashing

**Purpose**: Compute which hash slot a key belongs to

**Algorithm** :
```
slot = CRC16(key) % 16384
```

**Hash Tags**:
Forces multiple keys into same slot:
```
{user:1000}.profile → Only "user:1000" is hashed
{user:1000}.settings → Same slot as profile
```

**Use Case**: Multi-key operations (MSET, transactions) require all keys in same slot

---

### 23. Gossip Protocol

**Purpose**: Cluster membership and state propagation

**How It Works** :
- Each node periodically exchanges messages with random subset of peers
- Shared information:
  - Known nodes and their slots
  - Node health (PFAIL/FAIL states)
  - Configuration epochs

**Message Types** :
| Type | Purpose |
|------|---------|
| `MEET` | Add new node to cluster |
| `PING` | Check node health, exchange gossip |
| `PONG` | Response to PING/MEET |
| `FAIL` | Broadcast node failure confirmation |

**Frequency**: `cluster-node-timeout` (default 15 seconds)

---

### 24. Heartbeat Failure Detection

**Purpose**: Detect node failures without central coordinator

**Two-Stage Detection** :

| State | Trigger | Duration |
|-------|---------|----------|
| **PFAIL** (Probable Fail) | Node misses heartbeat for `cluster-node-timeout` | Local suspicion only |
| **FAIL** (Confirmed Fail) | Majority of masters agree on PFAIL | Triggers failover |

**Why Two Stages?**:
- Single node may have network partition
- Majority confirmation prevents false failover
- Requires at least 3 master nodes for fault tolerance

---

### 25. Leader Election for Failover

**Purpose**: Elect which replica becomes new master

**Election Process** :
1. Replica detects master FAIL state
2. Replica waits random delay (to stagger elections)
3. Replica sends `FAILOVER_AUTH_REQUEST` to all masters
4. Each master votes for **one** replica per epoch
5. Replica needs **majority** of masters votes
6. If elected, replica becomes master and takes over slots

**Vote Requirements**: `> N/2 + 1` (N = number of master nodes) 

**Prevention Mechanisms**:
- Each configuration epoch allows only one election
- Monotonic epoch numbers prevent stale leadership

---

## Sentinel (Failover Coordination)

### 26. Quorum-Based Election

**Purpose**: Sentinel leader election for failover coordination

**Sentinel Quorum** :
- Minimum sentinels required: 3 (odd number prevents ties)
- Quorum = `N/2 + 1` (e.g., 2 of 3, 3 of 5)

**Two Detection Stages** :

| Stage | Description | Trigger |
|-------|-------------|---------|
| **SDOWN** (Subjective Down) | Single sentinel believes master down | No response for `down-after-milliseconds` (default 30s) |
| **ODOWN** (Objective Down) | Quorum of sentinels agree master down | `quorum` confirmations |

---

### 27. Majority Voting (Raft-based Election)

**Purpose**: Elect leader sentinel to execute failover

**Algorithm**: Raft consensus variant 

**Election Requirements**:
- Sentinel must receive votes from **majority** (`> N/2 + 1`)
- Each sentinel votes for highest configuration epoch

**Election Flow** :
```
1. Sentinel detects ODOWN
2. Sentinel increments epoch (config-epoch)
3. Sentinel broadcasts SLAVEOT (request vote)
4. Other sentinels reply (grant/deny)
5. Winner becomes leader
6. Leader executes failover
```

**Prevents Split-Brain**: Only one leader per epoch

---

### 28. Epoch-Based Failover

**Purpose**: Prevent conflicting failover attempts

**Configuration Epoch** :
- Monotonically increasing number (like Raft term)
- Each failover increments epoch
- New master's epoch > all previous epochs

**Why Epoch Matters**:
- Replicas reject commands from older epoch masters
- Client redirects based on highest epoch
- Resolves "split-brain" master scenarios

**Example**:
```
Epoch 1: Master A fails
Epoch 2: Master B promoted
If A reappears, replicas ignore it (epoch 2 > 1)
```

---

## Messaging & Stream Processing

### 29. Publish-Subscribe (Pub/Sub)

**Purpose**: Messaging pattern for decoupled communication

**How It Works**:
- Publishers send messages to channels
- Subscribers receive all messages to subscribed channels
- **No persistence** - messages dropped if no subscribers

**Commands**:
```bash
SUBSCRIBE news         # Subscribe to channel
PUBLISH news "hello"   # Send to all subscribers
PSUBSCRIBE news.*      # Pattern subscription
```

**Limitations**: Messages lost if subscriber disconnects

---

### 30. Redis Streams

**Purpose**: Append-only event log with consumer groups

**Introduced**: Redis 5.0 

**Structure** :
```
Stream (key) → List of messages
Each message:
  - ID: "<millisecondsTime>-<sequenceNumber>" (e.g., "1609459200000-0")
  - Fields: Multiple field-value pairs
```

**Features** :
- **Persistence**: Streams stored on disk (like Kafka)
- **Consumer groups**: Multiple groups with independent offsets
- **Blocking reads**: Wait for new messages
- **Range queries**: Get messages by ID range

---

### 31. Consumer Groups

**Purpose**: Coordinate multiple consumers processing same stream

**Structure** :
```
Stream
  └── Consumer Group
        ├── last_delivered_id (cursor position)
        ├── Consumer 1 → PEL (Pending Entry List)
        ├── Consumer 2 → PEL
        └── Consumer N → PEL
```

**Create Group** :
```bash
XGROUP CREATE mystream mygroup 0  # Start from beginning
XGROUP CREATE mystream mygroup $  # Start from end
```

**Key Property**: Different consumer groups have independent cursors - each group consumes all messages 

---

### 32. Message Acknowledgment Tracking

**Purpose**: Ensure at-least-once processing

**How It Works** :
1. Consumer reads message via `XREADGROUP`
2. Message ID added to consumer's **PEL** (Pending Entry List)
3. Consumer processes message
4. Consumer sends `XACK` message_id
5. Message removed from PEL

**Failure Recovery** :
- If consumer crashes before ACK, message remains in PEL
- Other consumers can claim unacked messages
- Prevents data loss

---

### 33. Pending Entry List (PEL)

**Purpose**: Track unacknowledged messages per consumer

**Properties** :
- Contains message IDs read but not yet acknowledged
- Monitored via `XPENDING` command
- Grows with unacked messages - **must ACK to prevent memory bloat**

**PEL Contents**:
```
XPENDING mystream mygroup
1) (integer) 3        # 3 pending messages
2) 1609459200000-0    # Oldest pending ID
3) 1609459200002-0    # Newest pending ID
4) 1) "consumer-1"    # Consumer with pending
   2) "2"             # Count for that consumer
```

**Recovery Command** :
```bash
# Claim unacked messages from crashed consumer
XCLAIM mystream mygroup consumer-2 60000 1609459200000-0
```

---

## Probabilistic Data Structures (Redis Stack)

### 34. HyperLogLog (HLL)

**Purpose**: Approximate unique counting

**Accuracy**: ~0.81% error rate, configurable

**How It Works**:
- Probabilistic algorithm estimating cardinality of multiset
- Uses observation of longest run of leading zeros in hash values
- Memory: ~12KB per key (regardless of cardinality)

**Commands**:
```bash
PFADD visitors "user123" "user456"  # Add elements
PFCOUNT visitors                     # Approximate count
PFMERGE all_visitors site1 site2     # Merge HLLs
```

**Used For**: Unique visitor counting, distinct event tracking

---

### 35. Bloom Filter

**Purpose**: Probabilistic membership testing

**Properties** :
- **Can say**: "definitely not present" (100% certainty)
- **Can say**: "maybe present" (configurable false positive rate)
- **Cannot**: Say definitely present

**Implementation**:
- Bit array of size `m`
- `k` independent hash functions
- Add: Set bits at all `k` hash positions
- Check: If any bit is 0, element not present

**Commands** (Redis Stack) :
```bash
BF.RESERVE myfilter 0.01 100000  # 1% false positive, 100k capacity
BF.ADD myfilter "user123"        # Add element
BF.EXISTS myfilter "user123"     # Check existence
BF.MADD myfilter "a" "b" "c"     # Batch add
```

**Used For**: Cache filtering, duplicate detection, spam prevention

---

### 36. Cuckoo Filter

**Purpose**: Deletable probabilistic membership filter

**Advantages Over Bloom** :
- **Supports deletion** (Bloom cannot)
- Better lookup performance
- Lower space for same false positive rate
- Supports counting occurrences

**How It Works**:
- Uses cuckoo hashing (two candidate buckets)
- Each bucket stores multiple fingerprints
- Insert: Place fingerprint in one of two positions
- Delete: Remove fingerprint from bucket
- If both buckets full, "kick" existing fingerprint

**Commands** (Redis Stack) :
```bash
CF.RESERVE myfilter 100000  # Capacity
CF.ADD myfilter "item"       # Add
CF.DEL myfilter "item"       # Delete (Bloom can't do this!)
CF.EXISTS myfilter "item"    # Check
```

**Used For**: Databases with frequent deletions, cache hierarchies

---

### 37. Count-Min Sketch

**Purpose**: Approximate frequency counting

**How It Works** :
- 2D array: `width` buckets × `depth` hash functions
- Each hash function maps to one bucket per row
- Insert: Increment each mapped bucket
- Query: Return minimum value across all rows

**Accuracy Guarantee**:
- Always overestimates (never underestimates)
- Error: `ε * N` with probability `1 - δ`
- Configured via `width = 2/ε`, `depth = ln(1/δ)`

**Commands** (Redis Stack) :
```bash
CMS.INITBYPROB myfilter 0.001 0.01  # error=0.1%, prob=99%
CMS.INCRBY myfilter "item" 5        # Increment by 5
CMS.QUERY myfilter "item"           # Get count estimate
```

**Used For**: Frequency estimation, heavy hitters, top-k elements

---

### 38. Top-K Algorithm

**Purpose**: Heavy-hitter detection - find k most frequent items

**How It Works** :
- Maintains list of `k` candidates with counts
- Uses Count-Min Sketch to estimate frequencies
- Replaces candidates when lower frequency detected

**Properties**:
- Memory efficient (not storing all items)
- Guarantees top-k elements within error bounds
- Supports removals

**Commands** (Redis Stack) :
```bash
TOPK.RESERVE mytop 100 2000 5 0.9  # k=100, width=2000, depth=5, decay=0.9
TOPK.ADD mytop "item1" "item2"     # Add items
TOPK.QUERY mytop "item1"           # Check if in top-k
TOPK.LIST mytop                    # Get current top-k
```

**Used For**: Trending topics, popular products, hot keys detection

---

### 39. t-Digest

**Purpose**: Percentile and quantile estimation

**Introduced**: Redis Stack (latest) 

**Problem Solved**:
Traditional percentiles (like p99 latency) require storing all data or using fixed histograms. t-digest provides accurate quantiles with bounded error using compact representation.

**How It Works** :
- Creates variable-sized clusters based on density
- Many centroids near extremes (tails)
- Fewer centroids near median
- Merges centroids when needed

**Accuracy**:
- Best at extremes (p99, p1)
- Configurable compression factor
- Memory: ~10KB to ~1MB typical

**Commands** (Redis Stack) :
```bash
TDIGEST.CREATE mydigest 100  # Compression=100
TDIGEST.ADD mydigest 1.5 2.5 3.0  # Add values
TDIGEST.QUANTILE mydigest 0.5 0.9 0.99  # Get percentiles
TDIGEST.CDF mydigest 2.5  # Fraction of values <= 2.5
```

**Questions t-digest Answers** :
- Which fraction of values are < given value?
- What is the p-percentile value?
- What is the mean between p1 and p2 percentiles?
- What is the nth smallest value?

**Used For**: Latency monitoring, performance metrics, sensor data

---

## Network & Event Model

### 40. Reactor Pattern

**Purpose**: Event-driven architecture for single-threaded performance

**Components** :
- **Event Loop**: Single thread handling all I/O
- **Event Demultiplexer**: epoll/kqueue (waits for I/O events)
- **Event Handlers**: Command processing, replication, etc.

**Flow** :
```
                ┌─────────────────┐
                │   Event Loop    │
                └────────┬────────┘
                         │
         ┌───────────────▼───────────────┐
         │    epoll_wait (I/O events)    │
         └───────────────┬───────────────┘
                         │
         ┌───────────────▼───────────────┐
         │   Dispatch by event type      │
         └───────────────┬───────────────┘
                         │
      ┌──────────────────┼──────────────────┐
      ▼                  ▼                  ▼
  Accept            Read/Write           Timer
  Handler            Handler            Handler
```

---

### 41. Single-Threaded Event Loop Architecture

**Purpose**: Avoid concurrency overhead (locking, context switching)

**Redis 6.0 and earlier**: Pure single-threaded 

**Workflow** :
```c
while (!should_exit) {
    // 1. Wait for file descriptors to be ready
    aeApiPoll(time_event, &ready_fds);
    
    // 2. Process ready events
    for (fd : ready_fds) {
        if (fd_type == ACCEPT) {
            acceptClientHandler();    // New connection
        } else if (fd_type == READ) {
            readQueryFromClient();     // Parse command
            processCommand();          // Execute
            sendReplyToClient();       // Send response
        }
    }
    
    // 3. Process time events (expiration, cron)
    processTimeEvents();
}
```

**Performance Impact**: QPS 100k+ despite single thread 

**Trade-offs**:
- ✅ No locks, no context switches
- ✅ Predictable performance
- ❌ Slow commands block everything
- ❌ Cannot use all CPU cores

**Redis 6.0+**: Multi-threaded I/O (still single-threaded execution) - threads only handle network I/O, command execution remains single-threaded

---

### 42. Non-Blocking I/O

**Purpose**: Prevent slow clients from blocking event loop

**Implementation** :
Redis sets all client sockets to non-blocking mode:
```c
int flags = fcntl(fd, F_GETFL, 0);
fcntl(fd, F_SETFL, flags | O_NONBLOCK);
```

**Non-Blocking read() Returns** :
| Return Value | Meaning |
|--------------|---------|
| >0 | Bytes read |
| 0 | Connection closed |
| -1 + EAGAIN | No data ready (try again later) |

**Benefits**:
- Single slow client cannot block entire server
- Many clients can be in various states of I/O readiness
- Event loop processes only ready connections

---

### 43. I/O Multiplexing (epoll/kqueue/select)

**Purpose**: Monitor multiple file descriptors with single thread

**Cross-Platform Abstraction** :
| OS | Mechanism | Implementation |
|----|-----------|----------------|
| Linux | epoll | `ae_epoll.c` |
| macOS/BSD | kqueue | `ae_kqueue.c` |
| Solaris | event ports | `ae_evport.c` |
| All | select (fallback) | `ae_select.c` |

**epoll Architecture** :

Three system calls:
```c
// 1. Create epoll instance
int epfd = epoll_create1(0);

// 2. Add/remove monitored file descriptors
struct epoll_event event = {
    .events = EPOLLIN | EPOLLET,  // Edge-triggered
    .data.fd = client_fd
};
epoll_ctl(epfd, EPOLL_CTL_ADD, client_fd, &event);

// 3. Wait for events
struct epoll_event events[10];
int n = epoll_wait(epfd, events, 10, -1);  // Block until events
```

**epoll Advantages** :
| Feature | select/poll | epoll |
|---------|-------------|-------|
| FD monitoring | O(N) scan | O(log N) red-black tree |
| FD limit | 1024 (select) | 系统限制 (~百万) |
| Event retrieval | Copy all FDs each time | Ready list only |
| Trigger modes | Level-trigger only | Level + Edge trigger |

**Edge-Triggered (ET) vs. Level-Triggered (LT)** :

| Mode | Behavior | Redis Choice |
|------|----------|--------------|
| LT | FD ready every epoll_wait until drained | Safer, simpler |
| ET | FD ready only once, must drain completely | **Higher performance** |

Redis uses ET mode to minimize epoll_wait calls :
```c
// ET mode - must read ALL data
ssize_t total = 0;
while ((n = read(fd, buf + total, sizeof(buf) - total)) > 0) {
    total += n;
}
// If read returns EAGAIN, we're done
```

---

### 44. Memory-Mapped Optimizations

**Purpose**: Efficient file I/O for persistence

**How It Works**:
- File mapped into process virtual address space
- OS handles paging data in/out of RAM
- No system call overhead for reads

**Redis Usage**:
- Loading RDB files (memory-mapped for faster load)
- Not used for normal operations (since data is in memory)

---

### 45. Batch Processing & Pipelining

**Purpose**: Reduce network round trips

**Command Pipelining**:
Client sends multiple commands without waiting for responses:
```
Without Pipeline:      CMD1 --→←-- RESP1 --→ CMD2 --→←-- RESP2
With Pipeline:         CMD1 CMD2 CMD3 →← RESP1 RESP2 RESP3
```

**Performance Gain**:
- 1 command: ~1 RTT + processing
- N commands pipelined: ~1 RTT + N × processing
- 10x-100x throughput improvement for many small commands

**Implementation**:
Client libraries send commands in bulk; Redis processes sequentially and returns responses in order.

---

## Complete System Interaction Diagram

```
┌─────────────────────────────────────────────────────────────────────────────┐
│                          CLIENT LAYER                                        │
│  ┌──────────────┐  ┌──────────────┐  ┌──────────────┐                       │
│  │  Pipelining  │  │ Pub/Sub      │  │ Smart Client │                       │
│  │  Commands    │  │ Subscriber   │  │ (Cluster)    │                       │
│  └──────────────┘  └──────────────┘  └──────────────┘                       │
└───────────────────────────────────┬─────────────────────────────────────────┘
                                    │
                                    ▼
┌─────────────────────────────────────────────────────────────────────────────┐
│                         NETWORK LAYER                                        │
│  ┌────────────────────────────────────────────────────────────────────┐     │
│  │                    REACTOR + epoll (Linux)                          │     │
│  │  ┌─────────────────────────────────────────────────────────────┐  │     │
│  │  │  Single-Threaded Event Loop                                  │  │     │
│  │  │                                                               │  │     │
│  │  │  ┌─────────┐    ┌─────────┐    ┌─────────┐    ┌─────────┐  │  │     │
│  │  │  │ Accept  │    │ Read    │    │ Process │    │ Write   │  │  │     │
│  │  │  │ Handler │───▶│ Handler │───▶│ Command │───▶│ Handler │  │  │     │
│  │  │  └─────────┘    └─────────┘    └─────────┘    └─────────┘  │  │     │
│  │  │       ▲              ▲              ▲              ▲        │  │     │
│  │  │       └──────────────┴──────────────┴──────────────┘        │  │     │
│  │  │                    Non-Blocking I/O                          │  │     │
│  │  └─────────────────────────────────────────────────────────────┘  │     │
│  └────────────────────────────────────────────────────────────────────┘     │
└───────────────────────────────────┬─────────────────────────────────────────┘
                                    │
                                    ▼
┌─────────────────────────────────────────────────────────────────────────────┐
│                        CORE DATA STRUCTURES                                  │
│                                                                              │
│  ┌─────────────────────────────────────────────────────────────────────┐    │
│  │                         DICTIONARY (Hash Table)                      │    │
│  │  ┌─────────┐  ┌─────────┐  ┌─────────┐  ┌─────────┐                │    │
│  │  │ Bucket0 │  │ Bucket1 │  │ Bucket2 │  │ Bucket3 │  ...           │    │
│  │  └────┬────┘  └────┬────┘  └────┬────┘  └────┬────┘                │    │
│  │       │            │            │            │                      │    │
│  │       ▼            ▼            ▼            ▼                      │    │
│  │    Entry        Entry        Entry        Entry                     │    │
│  │    key→value    key→value    key→value    key→value                 │    │
│  │      │            │            │            │                      │    │
│  │      ▼            ▼            ▼            ▼                      │    │
│  │    Next         Next         Next         Next                     │    │
│  └─────────────────────────────────────────────────────────────────────┘    │
│                                                                              │
│  ┌──────────────────────┐  ┌──────────────────────┐  ┌───────────────────┐  │
│  │   SKIP LIST (ZSET)   │  │   QUICKLIST (List)   │  │   LISTPACK        │  │
│  │                      │  │                      │  │   (Small Hash)    │  │
│  │  Level 4: 1--->9     │  │  Node 1→Node 2→Node N│  │  [entry1][entry2] │  │
│  │  Level 3: 1->5->9    │  │    │       │       │ │  │  Compact memory   │  │
│  │  Level 2: 1-3-5-7-9  │  │    ▼       ▼       ▼ │  │  No pointers      │  │
│  │  Level 1: 1-2-...-9  │  │ ziplist ziplist ziplist│                 │  │
│  └──────────────────────┘  └──────────────────────┘  └───────────────────┘  │
└───────────────────────────────────┬─────────────────────────────────────────┘
                                    │
                                    ▼
┌─────────────────────────────────────────────────────────────────────────────┐
│                       PERSISTENCE LAYER                                      │
│                                                                              │
│  ┌─────────────────────────────┐  ┌─────────────────────────────┐           │
│  │          RDB                 │  │          AOF                 │           │
│  │  ┌─────────────────────┐    │  │  ┌─────────────────────┐     │           │
│  │  │ fork() → child      │    │  │  │ Append every write  │     │           │
│  │  │ Copy-on-Write       │    │  │  │ to file             │     │           │
│  │  │ Binary dump         │    │  │  │ fsync policies:     │     │           │
│  │  │ Point-in-time       │    │  │  │ - always            │     │           │
│  │  └─────────────────────┘    │  │  │ - everysec (default)│     │           │
│  │                             │  │  │ - no                │     │           │
│  │                             │  │  └─────────────────────┘     │           │
│  │                             │  │                               │           │
│  │                             │  │  ┌─────────────────────┐     │           │
│  │                             │  │  │ AOF REWRITE         │     │           │
│  │                             │  │  │ fork() child        │     │           │
│  │                             │  │  │ Minimal commands    │     │           │
│  │                             │  │  │ Atomic rename       │     │           │
│  │                             │  │  └─────────────────────┘     │           │
│  └─────────────────────────────┘  └─────────────────────────────┘           │
└───────────────────────────────────┬─────────────────────────────────────────┘
                                    │
                                    ▼
┌─────────────────────────────────────────────────────────────────────────────┐
│                    REPLICATION & HIGH AVAILABILITY                           │
│                                                                              │
│  ┌─────────────────────────────────────────────────────────────────────┐    │
│  │                    MASTER (handles writes)                           │    │
│  │  ┌─────────────────────────────────────────────────────────────┐    │    │
│  │  │ Replication Backlog (ring buffer)                           │    │    │
│  │  │ offset: 1000 [cmd1][cmd2][cmd3]...[cmd1000]                │    │    │
│  │  └─────────────────────────────────────────────────────────────┘    │    │
│  │                              │                                       │    │
│  │              ┌───────────────┼───────────────┐                      │    │
│  │              ▼               ▼               ▼                      │    │
│  │  ┌──────────────┐  ┌──────────────┐  ┌──────────────┐               │    │
│  │  │  REPLICA 1   │  │  REPLICA 2   │  │  REPLICA 3   │               │    │
│  │  │  PSYNC       │  │  PSYNC       │  │  PSYNC       │               │    │
│  │  │  offset:950  │  │  offset:980  │  │  offset:1000 │               │    │
│  │  └──────────────┘  └──────────────┘  └──────────────┘               │    │
│  └─────────────────────────────────────────────────────────────────────┘    │
│                                                                              │
│  ┌─────────────────────────────────────────────────────────────────────┐    │
│  │                     SENTINEL QUORUM (3+ instances)                   │    │
│  │  ┌──────────────┐  ┌──────────────┐  ┌──────────────┐               │    │
│  │  │  Sentinel A  │  │  Sentinel B  │  │  Sentinel C  │               │    │
│  │  │  SDOWN→ODOWN │  │  SDOWN→ODOWN │  │  SDOWN→ODOWN │               │    │
│  │  │  Raft Leader │  │  Follower    │  │  Follower    │               │    │
│  │  │  Election    │  │              │  │              │               │    │
│  │  └──────────────┘  └──────────────┘  └──────────────┘               │    │
│  │         │                 │                 │                        │    │
│  │         └─────────────────┴─────────────────┘                        │    │
│  │                    Majority vote required                            │    │
│  └─────────────────────────────────────────────────────────────────────┘    │
└─────────────────────────────────────────────────────────────────────────────┘
                                    │
                                    ▼
┌─────────────────────────────────────────────────────────────────────────────┐
│                        REDIS CLUSTER (Optional)                              │
│                                                                              │
│  ┌─────────────────────────────────────────────────────────────────────┐    │
│  │                    16,384 HASH SLOTS                                 │    │
│  │  ┌─────────┐ ┌─────────┐ ┌─────────┐ ┌─────────┐ ┌─────────┐       │    │
│  │  │Master A │ │Master B │ │Master C │ │Master D │ │Master E │       │    │
│  │  │slots    │ │slots    │ │slots    │ │slots    │ │slots    │       │    │
│  │  │0-3276   │ │3277-6553│ │6554-9830│ │9831-13107│13108-16383│      │    │
│  │  └────┬────┘ └────┬────┘ └────┬────┘ └────┬────┘ └────┬────┘       │    │
│  │       │           │           │           │           │             │    │
│  │       ▼           ▼           ▼           ▼           ▼             │    │
│  │   Replica A1  Replica B1   Replica C1  Replica D1  Replica E1        │    │
│  │                                                                       │    │
│  │  Gossip Protocol: PING/PONG/FAIL messages for cluster state          │    │
│  │  CRC16(key) % 16384 → slot → master node                             │    │
│  └─────────────────────────────────────────────────────────────────────┘    │
└─────────────────────────────────────────────────────────────────────────────┘
```

---

## Algorithm Summary Table

| # | Algorithm/Concept | Primary Purpose | Redis Component |
|---|------------------|-----------------|-----------------|
| 1 | Hash Tables | O(1) key-value storage | Database, dict |
| 2 | Incremental Rehashing | Non-blocking resize | dict.c |
| 3 | Skip Lists | Ordered Sorted Sets | ZSET |
| 4 | Linked Lists | Queue operations | List (legacy) |
| 5 | QuickList | Hybrid list storage | List type (3.2+) |
| 6 | ListPack | Compact encoding | Small hash/zset/list |
| 7 | LRU Eviction | Cache management | maxmemory-policy |
| 8 | LFU Eviction | Frequency-based eviction | allkeys-lfu |
| 9 | Random Eviction | Simple eviction | allkeys-random |
| 10 | TTL Expiration | Time-based deletion | EXPIRE, active/passive |
| 11 | AOF Persistence | Command logging | appendonly |
| 12 | AOF Rewrite | Log compaction | bgrewriteaof |
| 13 | RDB Snapshots | Point-in-time backups | SAVE, BGSAVE |
| 14 | Copy-on-Write | Efficient forking | fork() for persistence |
| 15 | Master-Replica Replication | HA, read scaling | REPLICAOF |
| 16 | PSYNC | Partial resync | Replication backlog |
| 17 | Replication Backlog | Offset tracking | repl-backlog-size |
| 18 | Replication Offsets | Sync tracking | master_repl_offset |
| 19 | Hash Slots (16384) | Distributed sharding | Cluster |
| 20 | CRC16 Hashing | Slot calculation | Key→slot mapping |
| 21 | Gossip Protocol | Membership propagation | Cluster bus |
| 22 | Heartbeat Detection | Failure detection | PING/PONG |
| 23 | PFAIL/FAIL States | Distributed failure | Cluster detection |
| 24 | Leader Election | Failover coordination | Cluster failover |
| 25 | Quorum Election | Sentinel leader | SDOWN/ODOWN |
| 26 | Raft (Sentinel) | Leader election | Sentinel failover |
| 27 | Epoch-Based Failover | Conflict prevention | config-epoch |
| 28 | Pub/Sub | Messaging | SUBSCRIBE/PUBLISH |
| 29 | Redis Streams | Append-only event log | XADD, XREAD |
| 30 | Consumer Groups | Stream coordination | XREADGROUP |
| 31 | Message ACK | At-least-once delivery | XACK |
| 32 | PEL (Pending Entry List) | Unacked tracking | XPENDING |
| 33 | HyperLogLog | Approximate counting | PFADD, PFCOUNT |
| 34 | Bloom Filter | Membership test | BF.EXISTS |
| 35 | Cuckoo Filter | Deletable membership | CF.DEL |
| 36 | Count-Min Sketch | Frequency estimation | CMS.QUERY |
| 37 | Top-K Algorithm | Heavy-hitter detection | TOPK.LIST |
| 38 | t-Digest | Percentile estimation | TDIGEST.QUANTILE |
| 39 | Reactor Pattern | Event-driven architecture | ae.c event loop |
| 40 | Single-Threaded Event Loop | Concurrency avoidance | main thread |
| 41 | Non-Blocking I/O | Prevent blocking | O_NONBLOCK sockets |
| 42 | I/O Multiplexing (epoll) | Monitor many FDs | ae_epoll.c |
| 43 | Edge-Triggered (ET) | Reduce wakeups | epoll EPOLLET |
| 44 | Memory-Mapped I/O | Efficient file loading | RDB load |
| 45 | Command Pipelining | Reduce RTT | Client pipeline |

---

## Source Code Reference Locations (Open Source Redis)

| Component | Source Path |
|-----------|-------------|
| Dictionary | `src/dict.c`, `src/dict.h` |
| Skip List | `src/t_zset.c`, `src/server.h` (zskiplist) |
| QuickList | `src/quicklist.c`, `src/quicklist.h` |
| ListPack | `src/listpack.c`, `src/listpack.h` |
| ziplist (legacy) | `src/ziplist.c` |
| Eviction | `src/evict.c` |
| Expiration | `src/expire.c` |
| AOF | `src/aof.c` |
| RDB | `src/rdb.c` |
| Replication | `src/replication.c` |
| Sentinel | `src/sentinel.c` |
| Cluster | `src/cluster.c` |
| Streams | `src/t_stream.c` |
| Networking (ae) | `src/ae.c`, `src/ae_epoll.c` |
| HyperLogLog | `src/hyperloglog.c` |

---

## Configuration Reference

### Memory & Eviction
```conf
maxmemory 2gb
maxmemory-policy allkeys-lru  # or allkeys-lfu, volatile-lru, etc.
maxmemory-samples 5           # LRU/LFU sample size
```

### Persistence
```conf
save 900 1
save 300 10
save 60 10000

appendonly yes
appendfsync everysec
auto-aof-rewrite-percentage 100
auto-aof-rewrite-min-size 64mb
```

### Replication
```conf
replicaof master-host master-port
repl-backlog-size 1mb
repl-diskless-sync yes
```

### Cluster
```conf
cluster-enabled yes
cluster-config-file nodes.conf
cluster-node-timeout 15000
```

### Sentinel
```conf
sentinel monitor mymaster 127.0.0.1 6379 2  # quorum=2
sentinel down-after-milliseconds mymaster 30000
sentinel failover-timeout mymaster 180000
```

### Eviction Policies Summary
```bash
# Check current policy
CONFIG GET maxmemory-policy

# Set policy
CONFIG SET maxmemory-policy allkeys-lru

# View memory stats
INFO memory
INFO stats
```

---

## Performance & Complexity Reference

| Data Structure | Add | Delete | Search | Range Query | Memory Overhead |
|----------------|-----|--------|--------|-------------|-----------------|
| Hash Table | O(1) | O(1) | O(1) | O(N) | Low-medium |
| Skip List | O(log N) | O(log N) | O(log N) | O(log N + M) | Medium |
| QuickList | O(1)* | O(1)* | O(N)** | O(N) | Low |
| ListPack | O(N) | O(N) | O(N) | O(N) | Very low |
| Streams | O(1) | O(N) | O(log N) | O(log N + M) | Low |

*Push/pop at ends  **Index access

---

## Conclusion

Redis's design philosophy emphasizes:

- **Simplicity over features** (single-threaded, deterministic algorithms)
- **Memory efficiency over CPU** (listpack, quicklist)
- **Performance over strong consistency** (async replication, approximate structures)
- **Operational simplicity** (no schema, no indexing)

Key innovations include:
- **Incremental rehashing**: Non-blocking dictionary resizing
- **ListPack elimination of cascading updates**: Solving ziplist's fundamental flaw
- **QuickList**: Hybrid structure for optimal list performance
- **PSYNC**: Efficient replication recovery with backlog and offsets
- **Redis Cluster**: Decentralized sharding with 16384 hash slots
- **Streams with consumer groups**: Kafka-like functionality in memory
- **Probabilistic structures**: HyperLogLog, Bloom, Cuckoo, CMS, TopK, t-digest for sub-linear memory
- **epoll with edge-triggering**: Maximizing single-thread throughput

This combination of algorithms makes Redis versatile across use cases: caching (LRU/LFU), message queuing (Streams), real-time analytics (probabilistic structures), session storage (hash tables), and leaderboards (skip lists).

