# Amazon DynamoDB: Complete Distributed Systems Algorithms & Concepts Reference

## Document Overview

This document provides a comprehensive analysis of Amazon DynamoDB's architectural patterns, algorithms, and distributed systems concepts. DynamoDB is a fully managed NoSQL database service built on principles from the original Dynamo paper but with significant evolutionary changes toward stronger consistency and operational simplicity. It powers mission-critical applications at Amazon and millions of AWS customers globally.

---

## Table of Contents

1. [Core Architectural Patterns](#core-architectural-patterns)
2. [Data Partitioning & Distribution](#data-partitioning--distribution)
3. [Storage Engine & Indexes](#storage-engine--indexes)
4. [Replication & Consistency](#replication--consistency)
5. [Throughput Management](#throughput-management)
6. [Transaction Processing](#transaction-processing)
7. [Global Tables (Multi-Region)](#global-tables-multi-region)
8. [Failure Handling & Recovery](#failure-handling--recovery)
9. [Complete System Interaction Diagram](#complete-system-interaction-diagram)

---

## Core Architectural Patterns

### 1. Dynamo vs. DynamoDB: The Evolution

**Purpose**: Understand how DynamoDB evolved from the original Dynamo paper

**Original Dynamo (2007)** :
| Feature | Dynamo Implementation |
|---------|----------------------|
| Replication | Leaderless (peer-to-peer) |
| Consistency | Eventual only |
| Conflict resolution | Client-side (vector clocks) |
| Membership | Gossip-based |
| Operations | Self-managed |

**DynamoDB (Current)** :
| Feature | DynamoDB Implementation |
|---------|------------------------|
| Replication | **Single-leader** (Multi-Paxos per partition) |
| Consistency | Strong (optionally eventual) |
| Conflict resolution | Server-side (no client resolution) |
| Membership | Centralized (AWS control plane) |
| Operations | Fully managed |

**Why the Evolution?**
DynamoDB prioritized operational simplicity and predictable performance over the decentralized ideals of original Dynamo. By using single-leader replication with Paxos, DynamoDB provides:
- Strong consistency as an option
- No client-side conflict resolution complexity
- Simplified application development
- Managed operations at scale

### 2. Multi-Paxos Based Replication

**Purpose**: Strongly consistent replication with leader election per partition

**How It Works** :
Each partition (called a "replication group") has:
- **Leader replica**: Handles all write requests
- **Follower replicas**: Replicate from leader
- **Write quorum**: Requires ≥2/3 of healthy replicas to commit

**Quorum Requirements**:
```
N = total replicas per partition (typically 3 across AZs)
Write quorum = ceil(2N/3)  # e.g., 2 of 3
Read quorum = ceil(2N/3)   # for strongly consistent reads
```

**Comparison to Original Dynamo**:

| Aspect | Original Dynamo | DynamoDB |
|--------|-----------------|----------|
| Replication type | Leaderless | Single-leader (per partition) |
| Write quorum | Configurable (R + W > N) | Fixed (2/3 majority) |
| Consistency options | Tunable | Strong or eventual |
| Leader election | None (any replica writes) | Multi-Paxos |

### 3. Peer-to-Peer Coordination vs. Centralized Control Plane

**Purpose**: Contrast decentralized and centralized coordination approaches

**Original Dynamo (Decentralized)** :
- Gossip protocol for membership discovery
- No central coordinator
- Every node participates in failure detection
- Manual cluster management

**DynamoDB (Centralized)** :
- **AWS Control Plane** manages all tables
- Partition assignment tracked centrally
- Membership changes propagate through control plane
- Fully automated operations

**Advantages of Centralized Control**:
- Simplified operations (no customer cluster management)
- Faster decision making (no gossip convergence)
- Predictable performance (no gossip overhead)

---

## Data Partitioning & Distribution

### 4. Consistent Hashing with Virtual Partitions

**Purpose**: Distribute data evenly across storage nodes

**How It Works** :
- Each item's partition key is hashed using a cryptographic hash function
- Output determines assignment to a logical partition
- Partitions are mapped to physical storage servers

**Virtual Partitions** :
- Each table starts with a default number of partitions
- Partitions split automatically as data grows
- Physical servers host many partitions from different tables

**Hash Function Properties**:
```
partition = hash(partition_key) mod total_partitions

Properties:
- Deterministic: Same key → same partition
- Uniform: Hash spreads keys evenly across range
- Cryptographic: MD5/SHA-256 quality distribution
```

### 5. Partition Splitting & Auto-Scaling

**Purpose**: Scale seamlessly as data grows

**Split Triggers**:
| Metric | Threshold | Action |
|--------|-----------|--------|
| Partition size | 10 GB | Split into two partitions |
| Throughput | 3000 RCU or 1000 WCU | Split hot partition |

**Split Process** :
1. DynamoDB detects partition is "full" or "hot"
2. Control plane initiates split operation
3. Partition's hash range divided into two equal subranges
4. Data redistributed to two new partitions
5. Client routing updated atomically

**Adaptive Capacity** :
- Automatically increases throughput for hot partitions
- Isolates frequently accessed items to their own partition
- Enables single partition to handle up to 3000 RCU/1000 WCU
- Prevents throttling despite imbalanced access patterns

### 6. Hot Key and Hot Partition Mitigation

**Purpose**: Handle skewed access patterns at scale

**The Problem** :
Consistent hashing balances the key space, but not the request space. A single extremely popular key still pins all traffic to one partition, capping throughput at one server's capacity.

**DynamoDB's Approach** :
| Mechanism | Description |
|-----------|-------------|
| **Adaptive Capacity** | Automatically boosts hot partition throughput to 3000 RCU/1000 WCU |
| **Item Isolation** | Hot item moved to dedicated partition |
| **Burst Capacity** | 5 minutes of unused capacity available for spikes |

**Application-Level Mitigations** :
For predictably hot keys, designers can:
- **Add bucket/salt**: `user_id:123#bucket=0..K` to scatter across partitions
- **Read coalescing**: Single-flight concurrent reads to hot keys
- **Caching**: Edge cache for extremely hot items

**Limitations**:
- Adaptive capacity cannot fix monotonically increasing sort keys (e.g., `created_at` alone)
- 3000 RCU/1000 WCU is still an upper bound per partition

### 7. Rack and AZ Awareness

**Purpose**: High availability through fault isolation

**Default Replica Placement** :
```
Replication Group (3 replicas):
├── AZ1: Leader replica (handles writes)
├── AZ2: Follower replica (replicates from leader)
└── AZ3: Follower replica (replicates from leader)

Any AZ can be lost without data loss
```

**Write Quorum Across AZs**:
- Write requires acknowledgment from ≥2 of 3 AZs
- Single AZ failure doesn't block writes
- Remaining healthy replicas can still form quorum

---

## Storage Engine & Indexes

### 8. Primary Key Index (Partition + Sort Key)

**Purpose**: Uniquely identify items and enable efficient queries

**Components** :

| Component | Purpose | Cardinality |
|-----------|---------|-------------|
| **Partition Key** | Determines which partition stores item | Unlimited distinct values |
| **Sort Key** | Defines order within partition | Unlimited per partition |

**Data Organization** :
```
Partition (hash key = "user123")
├── Sort Key = "profile"
│   └── {name: "John", email: "..."}
├── Sort Key = "order#2024-01-01"
│   └── {order_id: "123", total: 100}
└── Sort Key = "order#2024-01-02"
    └── {order_id: "456", total: 200}

Items are stored in Sort Key order within each partition
```

**Query Patterns Supported**:
| Operation | Example |
|-----------|---------|
| Exact partition | `GetItem(partition_key='user123')` |
| Partition + sort equality | `Query where partition='user123' AND sort='profile'` |
| Partition + sort prefix | `Query where partition='user123' AND sort BEGINS_WITH 'order#'` |
| Partition + sort range | `Query where partition='user123' AND sort BETWEEN 'order#2024-01-01' AND 'order#2024-01-31'` |

### 9. Local Secondary Index (LSI)

**Purpose**: Alternate query access within same partition

**Characteristics** :
- Shares partition key with base table
- Alternate sort key (different attribute)
- Stored in same partition as base table data
- Created at table creation (cannot be added later)

**Use Case**: Time-series data where you need multiple sort orders:
```json
Base table:    PK=stock, SK=timestamp
LSI:           PK=stock, SK=price   // Query stocks by price range
```

**Limitations**:
- 5 LSIs per table maximum
- Must specify projected attributes (cannot query all)

### 10. Global Secondary Index (GSI)

**Purpose**: Query by attributes other than base partition key

**Characteristics** :
- **Independent partition key** from base table
- Optionally has its own sort key
- Stored in separate partitions from base table
- Can be created/deleted after table creation

**Consistency Trade-off**:
| Index Type | Consistency | Read Cost |
|------------|-------------|-----------|
| GSI | **Eventually consistent only** | Additional RCU |
| LSI | Strong (same partition) | Included in base read |

**Use Case**: Searching users by email:
```
Base table:  PK=user_id, SK=username
GSI:         PK=email, SK=username

Query: "Get user by email address"
```

### 11. Denormalization as a Design Pattern

**Purpose**: Optimize for DynamoDB's query patterns

**Relational vs. DynamoDB Modeling** :

| Relational Approach | DynamoDB Approach |
|--------------------|-------------------|
| Normalized tables | Denormalized single table |
| JOINs at query time | Pre-joined at write time |
| Foreign keys | Embedded documents |
| Multiple round trips | Single query |

**Example: User with Addresses** :
```json
{
  "user_id": "123",
  "email": "john@example.com",
  "addresses": [                    // Embedded, not separate table
    {"city": "Seattle", "type": "home"},
    {"city": "NYC", "type": "work"}
  ],
  "authentication_tokens": [        // Embedded array
    {"token": "abc123", "last_used": "2024-01-01"}
  ]
}
```

**Benefits**:
- Single read retrieves all related data
- No JOIN performance penalty
- Atomic updates to entire document

**Costs**:
- Data duplication across items
- Larger item size (max 400KB)

---

## Replication & Consistency

### 12. Single-Leader Replication per Partition

**Purpose**: Strong consistency with simpler conflict resolution

**Architecture** :
```
Write Request → Partition Leader → Write-Ahead Log → Replicas
                                                           ↓
                                              Quorum acknowledgment
                                                      ↓
                                                  Client Response
```

**Leader Election (Multi-Paxos)** :
- Each partition independently elects leader
- Leader elected from healthy replicas in replication group
- Election requires ≥2/3 of replicas to agree
- Leader lease prevents split-brain

**Why Single-Leader?** :
- **Strong consistency** as an option (no conflict resolution)
- **Simpler client logic** (no vector clocks)
- **Predictable latency** (no client-side reconciliation)
- **Global Tables** enable multi-region active-active

### 13. Consistency Models

**Purpose**: Tunable consistency for different use cases

**Read Consistency Options** :

| Type | Behavior | Use Case |
|------|----------|----------|
| **Eventually Consistent** | Returns data from any replica; may be stale (default) | High-throughput, tolerant of stale data |
| **Strongly Consistent** | Reads from leader after quorum confirmation | Critical reads requiring latest data |

**Implementation Details**:
```
Eventually Consistent Read:
Client → Any replica (fastest) → Returns data

Strongly Consistent Read:
Client → Leader → Wait for write quorum confirmation → Returns data
```

**Performance Impact**:
| Consistency | Latency | Throughput |
|-------------|---------|------------|
| Eventual | Lower (any replica) | Higher |
| Strong | Higher (leader + quorum) | Lower |

### 14. Write Consistency & Quorum

**Purpose**: Durable writes with fault tolerance

**Write Path** :
1. Request routes to partition leader
2. Leader appends to Write-Ahead Log (WAL)
3. Leader replicates to follower replicas
4. Leader waits for `⌈2N/3⌉` acknowledgments
5. Client receives success response

**With N=3 replicas**:
```
Quorum size = 2
Leader + 1 follower = success
2 followers = success
1 replica failure = still writable (2 of 3 remain)
2 replica failure = writes unavailable (only 1 of 3)
```

**Tunable vs. Fixed Quorum** :

| Aspect | Original Dynamo | DynamoDB |
|--------|-----------------|----------|
| Write quorum | Configurable (R+W>N) | Fixed (2/3 majority) |
| Read quorum | Configurable | Fixed for strong reads |
| Consistency level | Per-request tuning | Simple yes/no |

### 15. Vector Clocks vs. LWW (Comparison)

**Purpose**: Understand conflict handling evolution

**Original Dynamo Approach** :

| Concept | Description |
|---------|-------------|
| **Vector clock** | `[node=version, ...]` tracking causal history |
| **Client resolution** | Application resolves conflicts on read |
| **Concurrent writes** | Both preserved, client chooses |

**DynamoDB Approach** :
- **Last Write Wins (LWW)** : Default conflict resolution
- **Server-side resolution**: No client involvement
- **Time-based tie-breaking**: Most recent timestamp wins

**Why LWW?**:
- Simpler (most applications don't need vector clocks)
- Faster (no reconciliation overhead)
- Global Tables possible (deterministic resolution)

**Trade-off**:
- Loss of concurrent write information
- Potential data loss with uncoordinated writes
- Applications must be idempotent or tolerant

### 16. Eventually Consistent Reads with Global Tables

**Purpose**: Cross-region replication with async propagation

**Global Tables Architecture** :
```
US-East Region:    EU-West Region:
├── Table (active)  ├── Table (active)
├── Application     └── Application (reads local)
writes to local          (async replication)
     ↓                         ↑
     └───────── Stream ────────┘
```

**Replication Characteristics** :
- **Asynchronous replication** between regions
- **Last-writer-wins** conflict resolution
- **Typical replication lag**: <1 second (variable)

**Warning for Transactions** :
> Transactions are only ACID within a single region. Items replicate to other regions individually, not as a transaction unit. Applications writing to the same item in multiple regions may experience torn reads and lost updates.

**Best Practice** :
Route all writes for a given item (or all items) to a single region. Use other regions for read scalability and disaster recovery.

---

## Throughput Management

### 17. Provisioned Throughput (RCU/WCU)

**Purpose**: Predictable performance capacity

**Capacity Units**:

| Unit | Operation | Size | Definition |
|------|-----------|------|------------|
| **RCU (Read Capacity Unit)** | 1 strongly consistent read | 4 KB/sec | Or 2 eventually consistent reads |
| **WCU (Write Capacity Unit)** | 1 write | 1 KB/sec | Fixed size regardless of consistency |

**Example Calculation**:
```
Item size: 10 KB
Read pattern: Eventually consistent, 100 reads/second
Required RCU = 100 × (10KB / 4KB) × 0.5 (eventual) = 125 RCU

Write pattern: 50 writes/second, 10 KB items
Required WCU = 50 × ceil(10KB / 1KB) = 500 WCU
```

### 18. Burst Capacity

**Purpose**: Absorb short-term traffic spikes

**How It Works** :
- DynamoDB reserves unused capacity for up to **5 minutes (300 seconds)**
- Burst capacity can be consumed faster than provisioned rate
- Allows occasional spikes without throttling

**Example**:
```
Provisioned: 100 WCU (100 KB/sec writes)
If idle for 300 seconds: 30,000 WCU burst capacity available
Can sustain 600 WCU for 50 seconds (500 KB/sec)
```

**Warning** :
DynamoDB also uses burst capacity for background maintenance tasks. Burst capacity is not guaranteed and should not be relied upon for production throughput.

### 19. Adaptive Capacity

**Purpose**: Handle uneven partition access patterns

**The Hot Partition Problem** :
```
Table with 4 partitions, 400 WCU provisioned:
Each partition normally handles: 100 WCU

Partition 4 receives 150 WCU → Throttles above 100 WCU
```

**Adaptive Capacity Response**:
```
DynamoDB detects hot partition (150 WCU)
Automatically increases partition capacity
Partition 4 now can handle 150 WCU without throttling
```

**Automatic Item Isolation** :
If a single item drives high traffic, adaptive capacity:
- Isolates the item to its own partition
- Enables up to 3000 RCU / 1000 WCU to that item
- Prevents other partitions from being affected

**Conditions That Prevent Isolation**:
- Local Secondary Indexes exist on the table
- Item collections require same partition

### 20. On-Demand Mode

**Purpose**: Auto-scaling capacity without provisioning

**Characteristics**:
- Pay per request (no provisioned capacity)
- Scales instantly to workload
- Higher per-request cost than provisioned (~3-5x)
- Use for unpredictable workloads

**Trade-offs**:
| Factor | Provisioned | On-Demand |
|--------|-------------|-----------|
| Cost predictability | High (fixed) | Low (variable) |
| Scaling speed | Manual or auto-scaling | Instant |
| Best for | Predictable workloads | Spiky, unpredictable |

---

## Transaction Processing

### 21. ACID Transactions (TransactWriteItems)

**Purpose**: Atomic, isolated, durable operations across multiple items

**Scope**:
- Single AWS region
- Up to 100 items per transaction
- Items can be in different partitions
- Total transaction size ≤ 4 MB

**Operations Supported**:
| Operation | Description |
|-----------|-------------|
| `TransactWriteItems` | Atomic write across multiple items |
| `TransactGetItems` | Isolated read across multiple items |
| `ConditionCheck` | Precondition for transaction |

**Implementation** :
- Uses Multi-Paxos for coordination
- Two-phase commit within region
- All-or-nothing semantics

**Limitations with Global Tables** :
> Transactions are only ACID within the region where they execute. Replicated items become visible in other regions individually, not as a transaction unit. Do not write transactionally to the same item from multiple regions concurrently.

### 22. Condition Expressions

**Purpose**: Optimistic concurrency control

**How It Works**:
Client provides condition that must be true for operation to succeed.

**Examples**:
```javascript
// Atomic counter (compare and set)
UpdateItem({
  Key: { user_id: "123" },
  UpdateExpression: "ADD balance :delta",
  ConditionExpression: "balance >= :delta",  // Prevent overdraft
  ExpressionAttributeValues: {
    ":delta": 50,
    ":balance": 50
  }
});

// Insert if not exists
PutItem({
  Item: { user_id: "123", email: "..." },
  ConditionExpression: "attribute_not_exists(user_id)"
});

// Version check
UpdateItem({
  Key: { doc_id: "456" },
  UpdateExpression: "SET content = :new",
  ConditionExpression: "version = :expected",
  ExpressionAttributeValues: {
    ":expected": 3,
    ":new": "updated content"
  }
});
```

---

## Global Tables (Multi-Region)

### 23. Active-Passive vs. Active-Active

**Purpose**: Multi-region replication for disaster recovery

**DynamoDB Global Tables** :
- **Active-Active** replication
- Applications can write to any region
- Asynchronous replication between regions
- Last-writer-wins conflict resolution

**Conflict Resolution** :
```
Region A write: timestamp T1
Region B write: timestamp T2 (T2 > T1)
Result: Region B's write wins (LWW)

Both writes stored in both regions eventually
```

**When This Breaks** :
```javascript
// What happens with concurrent increments?
Region A: UPDATE counter SET value = value + 1 (reads 5)
Region B: UPDATE counter SET value = value + 1 (reads 5)

If T2 > T1:
Final value = 6 (one increment lost!)
Expected: 7 if serializable
```

### 24. Last-Writer-Wins Conflict Resolution

**Purpose**: Deterministic cross-region merge

**Algorithm**:
```
For each attribute in each write:
  Compare timestamps (replication timestamp)
  Attribute with newer timestamp wins
  All other attributes preserved from their newest writes
```

**Implications**:
- Not suitable for counters or increment operations
- Safe for writes that replace entire item
- Applications must be idempotent

### 25. Cross-Region Replication Lag

**Purpose**: Understand replication timing characteristics

**Typical Behavior**:
- **Median lag**: < 1 second within same continent
- **Worst-case lag**: Several seconds (network issues)
- **No SLA guarantee** on replication time

**Read Your Own Writes** :
- Writes in local region immediately visible locally
- May not be visible in other regions for seconds
- Applications should tolerate stale reads in remote regions

---

## Failure Handling & Recovery

### 26. Replica Failure & Self-Healing

**Purpose**: Automatic recovery from replica loss

**Detection**:
- Control plane monitors replica health
- Unhealthy replica marked for replacement

**Recovery Process**:
```
1. Control plane detects replica failure
2. New replica provisioned in same AZ (if possible)
3. New replica bootstrapped from leader
4. Replication catches up from WAL
5. Replica added back to replication group
```

**During Recovery** :
- Write quorum still possible if ≤1 replica failing
- Read availability depends on consistency level
- Recovery time: minutes (fully automated)

### 27. AZ Outage Handling

**Purpose**: Survive entire availability zone failure

**With N=3, 3 AZs**:
```
Before outage: 3 AZs, 1 replica each
AZ fails: 1 replica lost, 2 remain

Write quorum = 2 needed
2 remain → writes still possible

Read availability: eventual reads from remaining AZs
```

**Automatic Response**:
1. Control plane detects AZ impairment
2. Replicas in failed AZ marked unhealthy
3. New replicas created in healthy AZs
4. Traffic re-routed automatically

### 28. Partition Failover

**Purpose**: Maintain availability if partition leader fails

**Leader Failure Detection** :
- Loss of heartbeats from leader
- Other replicas detect through control plane

**Leader Election**:
```
1. Replicas detect leader failure
2. Multi-Paxos election among remaining replicas
3. Candidate requires ≥2/3 votes
4. New leader elected (10-30 seconds typical)
```

**Client Impact**:
- Writes may be unavailable during election
- Reads with eventual consistency continue
- Transparent retry by SDKs

---

## Complete System Interaction Diagram

```
┌─────────────────────────────────────────────────────────────────────────────┐
│                          APPLICATION LAYER                                   │
│  ┌──────────────┐  ┌──────────────┐  ┌──────────────┐                       │
│  │  AWS SDK     │  │  DynamoDB    │  │  DAX Cache   │                       │
│  │  (HTTP/2)    │  │  Mapper      │  │  (In-memory) │                       │
│  └──────────────┘  └──────────────┘  └──────────────┘                       │
└───────────────────────────────────┬─────────────────────────────────────────┘
                                    │
                                    ▼
┌─────────────────────────────────────────────────────────────────────────────┐
│                      REQUEST ROUTING LAYER                                   │
│                                                                              │
│  ┌─────────────────────────────────────────────────────────────────────┐    │
│  │                    AWS CONTROL PLANE                                  │    │
│  │  • Partition mapping cache                                           │    │
│  │  • Request routing to partition leader                               │    │
│  │  • Throttling enforcement                                            │    │
│  │  • Capacity management (burst + adaptive)                            │    │
│  └─────────────────────────────────────────────────────────────────────┘    │
│                                                                              │
│  Consistent Hashing:                                                         │
│  partition_key → hash → (hash % total_partitions) → partition location      │
│                                                                              │
└───────────────────────────────────┬─────────────────────────────────────────┘
                                    │
                                    ▼
┌─────────────────────────────────────────────────────────────────────────────┐
│                        REPLICATION GROUP (Partition)                         │
│                                                                              │
│  ┌─────────────────────────────────────────────────────────────────────┐    │
│  │                         LEADER REPLICA                               │    │
│  │  • Handles all writes                                                │    │
│  │  • Write-Ahead Log (WAL) for durability                              │    │
│  │  • Coordinates replication to followers                              │    │
│  │  • Strongly consistent reads                                         │    │
│  │  • Transaction coordination                                           │    │
│  └─────────────────────────────────────────────────────────────────────┘    │
│                                    │                                         │
│                    (Replication with quorum)                                 │
│                                    │                                         │
│              ┌─────────────────────┼─────────────────────┐                   │
│              ▼                     ▼                     ▼                   │
│  ┌─────────────────┐   ┌─────────────────┐   ┌─────────────────┐            │
│  │ FOLLOWER (AZ1)  │   │ FOLLOWER (AZ2)  │   │ FOLLOWER (AZ3)  │            │
│  │ • Async replicate│   │ • Async replicate│   │ • Async replicate│            │
│  │ • Serve eventual│   │ • Serve eventual│   │ • Serve eventual│            │
│  │   reads         │   │   reads         │   │   reads         │            │
│  │ • Candidate for │   │ • Candidate for │   │ • Candidate for │            │
│  │   leader election│   │   leader election│   │   leader election│            │
│  └─────────────────┘   └─────────────────┘   └─────────────────┘            │
│                                                                              │
│  Write Quorum: ⌈2N/3⌉ = 2 (for N=3)                                         │
│  - Leader + 1 follower = success                                            │
│  - 2 replicas can fail before writes unavailable                             │
│                                                                              │
└─────────────────────────────────────────────────────────────────────────────┘
                                    │
                                    ▼
┌─────────────────────────────────────────────────────────────────────────────┐
│                           STORAGE LAYER                                      │
│                                                                              │
│  ┌─────────────────────────────────────────────────────────────────────┐    │
│  │                    PHYSICAL STORAGE NODES                            │    │
│  │                                                                       │    │
│  │  Node 1 (SSD-backed)      Node 2 (SSD-backed)      Node N            │    │
│  │  ┌─────────────────┐     ┌─────────────────┐     ┌─────────────────┐ │    │
│  │  │ Partition A     │     │ Partition B     │     │ Partition X     │ │    │
│  │  │ Partition D     │     │ Partition A     │     │ Partition C     │ │    │
│  │  │ Partition G     │     │ Partition E     │     │ Partition F     │ │    │
│  │  │ (multiple tables│     │ (many partitions│     │ (load balanced) │ │    │
│  │  │  per node)      │     │  per node)      │     │                 │ │    │
│  │  └─────────────────┘     └─────────────────┘     └─────────────────┘ │    │
│  │                                                                       │    │
│  └─────────────────────────────────────────────────────────────────────┘    │
│                                                                              │
│  Storage Features:                                                            │
│  • SSD-backed for low latency                                                │
│  • Replication across AZs for durability                                     │
│  • Automatic partition splitting at 10GB                                     │
│  • Adaptive capacity for hot partitions                                      │
│                                                                              │
└─────────────────────────────────────────────────────────────────────────────┘
                                    │
                                    ▼
┌─────────────────────────────────────────────────────────────────────────────┐
│                    GLOBAL TABLES (Multi-Region)                              │
│                                                                              │
│  Region A (US-East)              Region B (EU-West)                          │
│  ┌─────────────────────┐        ┌─────────────────────┐                     │
│  │  Active Table       │◄──────►│  Active Table       │                     │
│  │  • Writes from apps │  Async  │  • Writes from apps │                     │
│  │  • Local replication│ Stream  │  • Local replication│                     │
│  │  • LWW conflict     │        │  • LWW conflict     │                     │
│  │    resolution       │        │    resolution       │                     │
│  └─────────────────────┘        └─────────────────────┘                     │
│                                                                              │
│  Warning: Transactions are NOT ACID across regions [5][10]                   │
│  Warning: Concurrent writes to same item lose one update [10]               │
│                                                                              │
└─────────────────────────────────────────────────────────────────────────────┘
```

---

## Algorithm Summary Table

| # | Algorithm/Concept | Primary Purpose | DynamoDB Component |
|---|------------------|-----------------|---------------------|
| 1 | Consistent Hashing | Data distribution | Partition assignment |
| 2 | Virtual Partitions | Granular scaling | Auto-splitting |
| 3 | Multi-Paxos (per partition) | Strongly consistent replication | Write quorum |
| 4 | Single-Leader Replication | Simplified consistency | Partition leader |
| 5 | Quorum-based Commit (2/3) | Write durability | Write acknowledgment |
| 6 | Adaptive Capacity | Hot partition handling | Automatic throughput boost |
| 7 | Burst Capacity (5 min) | Short-term spikes | Unused capacity reserve |
| 8 | Last-Writer-Wins (LWW) | Conflict resolution | Global Tables, replication |
| 9 | Condition Expressions | Optimistic concurrency | Compare-and-set |
| 10 | Two-Phase Commit (regional) | ACID transactions | TransactWriteItems |
| 11 | Asynchronous Cross-Region Replication | Disaster recovery | Global Tables |
| 12 | Partition Key Hashing | Item → partition mapping | Primary key |
| 13 | Local Secondary Index (LSI) | Alternate sort within partition | Index |
| 14 | Global Secondary Index (GSI) | Cross-partition queries | Index |
| 15 | Write-Ahead Log (WAL) | Durability | Replica storage |
| 16 | Auto-Splitting (10GB/3000 RCU) | Horizontal scaling | Partition management |
| 17 | Leader Election (Multi-Paxos) | Failover | Replica promotion |

---

## Configuration Reference

### Table Settings

```json
// Provisioned Table
{
  "TableName": "Users",
  "AttributeDefinitions": [
    {"AttributeName": "user_id", "AttributeType": "S"},
    {"AttributeName": "email", "AttributeType": "S"}
  ],
  "KeySchema": [
    {"AttributeName": "user_id", "KeyType": "HASH"}
  ],
  "ProvisionedThroughput": {
    "ReadCapacityUnits": 1000,
    "WriteCapacityUnits": 500
  },
  "GlobalSecondaryIndexes": [{
    "IndexName": "EmailIndex",
    "KeySchema": [{"AttributeName": "email", "KeyType": "HASH"}],
    "Projection": {"ProjectionType": "ALL"},
    "ProvisionedThroughput": {
      "ReadCapacityUnits": 100,
      "WriteCapacityUnits": 50
    }
  }]
}
```

### On-Demand Table

```json
{
  "TableName": "Logs",
  "BillingMode": "PAY_PER_REQUEST",  // On-demand
  "KeySchema": [
    {"AttributeName": "partition_key", "KeyType": "HASH"},
    {"AttributeName": "sort_key", "KeyType": "RANGE"}
  ]
}
```

### Global Table Setup

```json
// Create replica in multiple regions
aws dynamodb create-global-table \
  --global-table-name GlobalUsers \
  --replication-group RegionName=us-east-1 RegionName=us-west-2 \
  --region us-east-1
```

### Transaction Example

```javascript
const params = {
  TransactItems: [
    {
      Update: {
        TableName: "Accounts",
        Key: { account_id: "checking-123" },
        UpdateExpression: "SET balance = balance - :amount",
        ConditionExpression: "balance >= :amount",
        ExpressionAttributeValues: { ":amount": 100 }
      }
    },
    {
      Update: {
        TableName: "Accounts",
        Key: { account_id: "savings-456" },
        UpdateExpression: "SET balance = balance + :amount",
        ExpressionAttributeValues: { ":amount": 100 }
      }
    }
  ]
};

await dynamodb.transactWriteItems(params).promise();
```

---

## Performance & Complexity Reference

| Operation | Complexity | Typical Latency (p99) |
|-----------|------------|----------------------|
| GetItem (eventual) | O(1) | <10 ms |
| GetItem (strong) | O(1) + quorum | <15 ms |
| PutItem (eventual) | O(1) | <10 ms |
| PutItem (strong) | O(1) + quorum | <15 ms |
| Query (100 items) | O(log N + 100) | <20 ms |
| Scan (full table) | O(N) | N × item latency |
| TransactWriteItems (up to 100) | O(coordinated) | <100 ms |
| GSI Query | O(log N) + eventual lag | <30 ms |

---

## Differences from Original Dynamo Paper

| Aspect | Original Dynamo (2007) | DynamoDB (Current) |
|--------|----------------------|---------------------|
| Replication | Leaderless (any replica writes) | Single-leader (per partition) |
| Consistency options | Tunable (R + W > N) | Fixed quorum for strong reads |
| Conflict resolution | Vector clocks (client resolves) | LWW (server resolves) |
| Membership | Gossip protocol | Centralized control plane |
| Operations | Self-managed | Fully managed (AWS) |
| Leader election | N/A (no leaders) | Multi-Paxos per partition |
| Node addition | Manual (hinted handoff) | Automatic (control plane) |
| Strong consistency | Requires R + W > N | Available as explicit option |

---

## Conclusion

DynamoDB represents a pragmatic evolution of the original Dynamo paper's principles, prioritizing:

- **Operational simplicity** (fully managed over decentralized control)
- **Strong consistency option** (client resolution complexity removed)
- **Predictable performance** (fixed quorums, provisioned capacity)
- **Auto-scaling intelligence** (adaptive capacity, auto-splitting)
- **Cross-region active-active** (Global Tables with LWW)

Key innovations include:
- **Single-leader replication with Multi-Paxos**: Strong consistency without client complexity
- **Adaptive capacity**: Automatic hot partition handling without schema changes
- **Global Tables**: Cross-region active-active with transparent conflict resolution
- **On-Demand mode**: Capacity scaling without provisioning overhead
- **Transactions**: ACID operations at NoSQL scale (regional only)

This combination of algorithms makes DynamoDB suitable for:
- **Mission-critical applications** (banking, gaming leaderboards)
- **High-scale user data** (Amazon customer profiles, Prime Video)
- **Real-time bidding** (ad tech, fraud detection)
- **IoT and sensor data** (at any scale)
- **Mobile backends** (consistent, predictable latency)

The service has evolved significantly from its Dynamo origins, favoring deterministic operations and managed simplicity over the peer-to-peer ideals that inspired it.

---

*Document Version: 1.0*
*Based on Amazon DynamoDB documentation, the original Dynamo paper, and AWS re:Post discussions*