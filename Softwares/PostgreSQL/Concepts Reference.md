# PostgreSQL: Complete Database Management System Reference

## Document Overview

This document provides a comprehensive analysis of PostgreSQL's architectural patterns, storage algorithms, query processing techniques, and distributed systems features. PostgreSQL is an advanced, open-source object-relational database management system known for its extensibility, standards compliance, and robust architecture. Unlike many other databases, PostgreSQL is designed as a **process-per-user** rather than thread-based system, and it implements a sophisticated **Multi-Version Concurrency Control (MVCC)** mechanism that allows readers and writers to coexist without blocking . This document covers the core architecture, storage engines, indexing algorithms, concurrency control, query optimization, replication strategies, and advanced features that power PostgreSQL.

---

## Table of Contents

1. [Core Architectural Patterns](#core-architectural-patterns)
2. [Storage Engine & Page Layout](#storage-engine--page-layout)
3. [TOAST (The Oversized-Attribute Storage Technique)](#toast-the-oversized-attribute-storage-technique)
4. [Free Space Map & Visibility Map](#free-space-map--visibility-map)
5. [Indexing Algorithms](#indexing-algorithms)
6. [Multi-Version Concurrency Control (MVCC)](#multi-version-concurrency-control-mvcc)
7. [Query Processing & Optimization](#query-processing--optimization)
8. [Replication & High Availability](#replication--high-availability)
9. [Complete System Interaction Diagram](#complete-system-interaction-diagram)

---

## Core Architectural Patterns

### 1. Process-Based Architecture

**Purpose**: Provide robust isolation between client connections using operating system processes rather than threads.

**Core Components**:

| Component | Process Name | Purpose |
|-----------|--------------|---------|
| **Postmaster** | `postmaster` | Listens for connections, forks backend processes |
| **Backend Process** | `postgres <database> <user>` | Handles a single client connection |
| **Background Writer** | `postgres: writer` | Writes dirty buffers to disk |
| **Checkpointer** | `postgres: checkpointer` | Performs checkpoint operations |
| **WAL Writer** | `postgres: walwriter` | Writes WAL buffers to disk |
| **Autovacuum Worker** | `postgres: autovacuum worker` | Performs vacuum operations |
| **Stats Collector** | `postgres: stats collector` | Collects system statistics |

**Why Process-Based?** :

| Aspect | Process-Based | Thread-Based |
|--------|--------------|--------------|
| **Memory isolation** | Complete (crash isolation) | Shared memory (one thread can corrupt others) |
| **Concurrency model** | OS-scheduled processes | OS-scheduled threads |
| **Memory overhead** | Higher per connection | Lower per connection |
| **Crash recovery** | Single process crash doesn't affect others | Thread crash may crash entire process |
| **Debugging** | Simpler (attach gdb to specific PID) | More complex |

**Process Communication**:

Processes communicate via:
- **Shared memory**: For buffer cache and lock tables
- **Signal**-based notifications
- **Message queues** for process status updates

### 2. Shared Buffer Architecture

**Purpose**: Cache database pages in memory to reduce disk I/O.

**Buffer Pool Organization**:

PostgreSQL uses a **buffer pool** managed by the `shared_buffers` parameter. The pool is organized as an array of 8 KB pages (or other configured block size).

**Buffer Descriptor Structure**:
- Page content (data)
- Buffer tag (relfilenode, block number)
- Status flags (dirty, valid, pinned)
- Usage count (for clock sweep algorithm)

### 3. Client-Server Protocol

**Purpose**: Manage communication between clients and database server.

**Connection Flow**:
1. Client connects to Postmaster listening on specified port (default 5432)
2. Postmaster authenticates the client
3. Postmaster forks a new backend process for the connection
4. Backend handles all subsequent queries
5. Connection state is isolated in the dedicated backend

**Protocol Types**:
- **Simple Query Protocol**: Single-command, text-based
- **Extended Query Protocol**: Prepare, bind, execute stages (prepared statements)
- **Replication Protocol**: Used for streaming replication

---

## Storage Engine & Page Layout

### 4. Database Page Layout

**Purpose**: Provide the physical storage format for tables and indexes.

**Page Size**: PostgreSQL typically uses 8 KB pages, though this can be configured at compile time .

**Page Structure**:

| Section | Offset | Size | Description |
|---------|--------|------|-------------|
| **PageHeaderData** | 0 | 24 bytes | Page metadata (LSN, checksum, flags, free space pointers) |
| **ItemIdData** | 24 | 4 bytes per item | Array of (offset, length) pointers to actual items |
| **Free Space** | Variable | Variable | Unallocated space (new items start at end, new pointers at start) |
| **Items** | Variable | Variable | Actual row data or index entries |
| **Special Space** | End | Variable | Index access method-specific data (empty for heap tables) |

**PageHeaderData Fields** :

| Field | Size | Description |
|-------|------|-------------|
| `pd_lsn` | 8 bytes | Page's last WAL log sequence number |
| `pd_checksum` | 2 bytes | Page checksum (if enabled) |
| `pd_flags` | 2 bytes | Status flags (all-visible, etc.) |
| `pd_lower` | 2 bytes | Offset to start of free space |
| `pd_upper` | 2 bytes | Offset to end of free space |
| `pd_special` | 2 bytes | Offset to start of special space |
| `pd_pagesize_version` | 2 bytes | Page size and version indicator |
| `pd_prune_xid` | 4 bytes | Oldest XID that may need pruning |

### 5. Heap Tuple Format

**Purpose**: Store row data on heap pages.

**Tuple Header** (`HeapTupleHeaderData`):

| Field | Size | Description |
|-------|------|-------------|
| `t_xmin` | 4 bytes | Inserting transaction ID (XID) |
| `t_xmax` | 4 bytes | Deleting/updating transaction ID |
| `t_cid` | 4 bytes | Command ID within transaction |
| `t_ctid` | 6 bytes | Current tuple ID (block + offset) of this or newer version |
| `t_infomask2` | 2 bytes | Number of attributes + flags |
| `t_infomask` | 2 bytes | Tuple status flags |
| `t_hoff` | 1 byte | Offset to user data |

**Tuple Types**:
- **Heap tuple**: Regular row data
- **TOAST tuple**: Oversized attribute data (stored in TOAST table)
- **Index tuple**: Index entry data

### 6. Tuple Versioning (MVCC Foundation)

**Purpose**: Support MVCC by keeping multiple versions of a row.

**How Tuple Versioning Works**:

When a row is updated, PostgreSQL:
1. Marks the old tuple as "dead" (sets `t_xmax` to current transaction ID)
2. Inserts a new tuple with the new values
3. Sets the new tuple's `t_xmin` to current transaction ID
4. Links the new tuple to the old via `t_ctid`

**Visual Representation**:
```
Version 1 (original):
  t_xmin = 100, t_xmax = 200, t_ctid = (2,1)

Version 2 (updated):
  t_xmin = 200, t_xmax = 0, t_ctid = (2,2)
```

Transaction 150 sees only version 1 (since t_xmax > 150). Transaction 250 sees version 2 (since t_xmin ≤ 250 and t_xmax = 0).

---

## TOAST (The Oversized-Attribute Storage Technique)

### 7. TOAST Architecture

**Purpose**: Store large field values that exceed the page size limit .

**The Problem**:
- Page size is typically 8 KB
- A row must fit within a single page
- Large text, JSON, JSONB, or BYTEA fields can exceed this limit

**TOAST Solution**:
- Large values are compressed (optionally)
- Values exceeding ~2 KB are stored in a separate TOAST table
- The main table stores a TOAST pointer

**TOAST Table**:
Each table with TOAST-able columns has an associated TOAST table named `pg_toast.pg_toast_<oid>`.

### 8. TOAST Strategies

**Compression and Storage Strategies**:

| Strategy | Behavior | Use Case |
|----------|----------|----------|
| **PLAIN** | No compression, no out-of-line storage | Fixed-length, non-TOAST-able types |
| **EXTENDED** | Compression + out-of-line storage (default) | Balance of space and performance |
| **EXTERNAL** | Out-of-line storage only, no compression | Large values that are frequently accessed partially |
| **MAIN** | Compression first, then out-of-line if needed | Moderate-sized values |

**TOAST Pointer Structure** :

A TOAST pointer in the main table contains:
- **Pointer to TOAST table** (relation OID + item pointer)
- **Original data size** (uncompressed)
- **Compressed data size** (if compressed)
- **Flags** indicating compression method

### 9. TOAST Compression Algorithms

**PG_LZ (LZ-based Compression)** (Pre-PostgreSQL 14):
- Fast compression/decompression
- Good compression ratio for text

**LZ4 Compression** (PostgreSQL 14+):
- Much faster than PG_LZ
- Slightly lower compression ratio
- Default when `default_toast_compression = 'lz4'` 

**PGLZ** (PostgreSQL's internal LZ implementation):
- Legacy default
- Good balance of speed and ratio

**Compression Decision Flow**:
1. Value size ≤ TOAST_TUPLE_THRESHOLD (2 KB) → Store inline
2. Attempt compression
3. Compressed size ≤ TOAST_TUPLE_THRESHOLD → Store compressed inline
4. Otherwise → Move to TOAST table

### 10. In-Memory TOAST Storage

**Purpose**: Provide optimized in-memory representations for complex data types .

**Expanded/Deconstructed Representation**:
- Pre-processed data structure for efficient computation
- Arrays: Pre-computed element offsets for O(1) access
- Eliminates repeated scanning for Nth element access

**Expanded TOAST Pointer Types** :

| Type | Description | Access |
|------|-------------|--------|
| **Read-write pointer** | Allows in-place modification | Can be modified without copying |
| **Read-only pointer** | Prevents modification | Must be copied before modification |

**Storage Process**:
- In-memory TOAST pointers are **never persisted to disk**
- Before storage, they are expanded to standard varlena format
- Then may be compressed and TOASTed if needed

---

## Free Space Map & Visibility Map

### 11. Free Space Map (FSM)

**Purpose**: Track available space on each heap page to efficiently locate pages for new tuples .

**FSM Architecture**:
- Stored in a separate relation fork: `<filenode>_fsm`
- Organized as a **tree of FSM pages**

**FSM Page Structure** :
```
Each FSM page contains a binary tree stored in an array
- Leaf nodes: Free space for each heap page (1 byte each)
- Internal nodes: Maximum of children's values
- Root node: Maximum free space in the entire relation
```

**Search Algorithm**:
1. Start at root, find child with value ≥ requested size
2. Navigate down to leaf node
3. Return page number with sufficient free space

**Update Strategy**: Conservative (FSM may slightly underestimate free space to avoid scanning for space that isn't actually available).

**Usage**:
- `INSERT` operations use FSM to find pages with space
- `VACUUM` updates FSM with newly freed space

### 12. Visibility Map (VM)

**Purpose**: Track which heap pages contain only tuples visible to all active transactions .

**VM Architecture**:
- Stored in a separate relation fork: `<filenode>_vm`
- Each heap page has **two bits** in the VM

**Bit Meanings** :

| Bit | Name | Meaning |
|-----|------|---------|
| **Bit 0** | All-Visible | All tuples on page are visible to all active transactions |
| **Bit 1** | All-Frozen | All tuples on page have been frozen (anti-wraparound) |

**All-Visible Bit Benefits**:
- **Index-Only Scans**: Query can be answered using only the index without heap access
- **VACUUM**: Can skip pages marked all-visible
- **HOT (Heap-Only Tuples)**: Enables certain optimizations

**Bit Setting Rules**:
- Bits are **set** by `VACUUM` operations
- Bits are **cleared** by any data-modifying operations on the page
- Conservative: If unsure, bit is cleared (safe to clear, unsafe to set incorrectly)

**Index-Only Scan Flow**:
1. Check VM for page containing the heap tuple
2. If all-visible bit set, skip heap access
3. Return tuple directly from index

---

## Indexing Algorithms

### 13. B-Tree Indexes

**Purpose**: Provide efficient equality and range queries with O(log n) complexity .

**Structure**:
```
B-Tree (balanced tree)
        Root
      /   |   \
  Node  Node Node
   / \   / \   / \
Leaf Leaf Leaf Leaf ...

Each node contains key values that guide search direction
```

**Search Algorithm** :
1. Start at root node
2. Compare search key with node's key ranges
3. Navigate to appropriate child node
4. Repeat until reaching leaf node
5. Return data from leaf node

**Time Complexity**: O(log n) for search, insert, delete

**B-Tree Use Cases** :

| Operator | Use Case | Index Used |
|----------|----------|------------|
| `=` | Equality | ✓ |
| `<`, `<=`, `>`, `>=` | Range queries | ✓ |
| `BETWEEN` | Range queries | ✓ |
| `ORDER BY` | Sorting | ✓ (returns data in index order) |

**Creation Example**:
```sql
CREATE INDEX idx_employees_salary ON employees (salary);
```

### 14. GiST (Generalized Search Tree)

**Purpose**: Support non-scalar data types and complex search operations.

**Use Cases**:
- Geometric types (points, polygons, circles)
- Full-text search
- Array operations (overlap, contains)
- Range types (overlap, contains)

**Structure**: Balanced tree with user-defined methods (consistent, union, compress, decompress, penalty, picksplit, equal).

**Supported Operators**:
- `<<` (strictly left of)
- `&<` (overlaps or left of)
- `&&` (overlaps)
- `@>` (contains)
- `<@` (contained in)
- `>>` (strictly right of)

### 15. GIN (Generalized Inverted Index)

**Purpose**: Index composite values where multiple keys map to a single value (arrays, JSONB, full-text search).

**Structure**:
- Builds inverted index: key → list of tuple IDs (posting list)

**Use Cases**:
- JSONB keys and values
- Array containment (`@>`, `&&`, `<@`)
- Full-text search (`@@`)

**Example**:
```sql
-- JSONB indexing
CREATE INDEX idx_data ON documents USING GIN (data);

-- Full-text search
CREATE INDEX idx_fts ON documents USING GIN (to_tsvector('english', content));
```

### 16. BRIN (Block Range Index)

**Purpose**: Index very large tables with natural correlation to physical storage.

**Structure**:
- Summarizes min/max values for block ranges (typically 128 pages)
- Very small index size (typically 1-2% of table size)

**Use Case**:
- Very large tables (100M+ rows)
- Natural correlation with physical order (e.g., timestamp columns in append-only tables)
- Queries that access ranges of values (BETWEEN, >, <)

**Space Comparison**:
- B-Tree on 1B rows: ~30 GB
- BRIN on same table: ~200 MB

### 17. Hash Indexes

**Purpose**: Fast equality lookups for specific use cases.

**Structure**: Static hash table (unlike B-Tree)

**Characteristics**:
- Only supports `=` operator
- Non-unique hash indexes allowed
- Not WAL-logged for performance (until PostgreSQL 10+)
- Smaller than B-Tree for equality-only lookups

**Example**:
```sql
CREATE INDEX idx_users_email_hash ON users USING HASH (email);
```

---

## Multi-Version Concurrency Control (MVCC)

### 18. MVCC Fundamentals

**Purpose**: Allow concurrent reads and writes without blocking, maintaining transaction isolation .

**Core Principle**: Instead of locking rows, PostgreSQL maintains multiple versions of each row. Each transaction sees a **snapshot** of data as it existed at a point in time.

**Key Property**: 
> **Readers never block writers, and writers never block readers.**

This is fundamentally different from traditional locking databases where read locks can conflict with write locks.

### 19. Transaction ID (XID) Management

**Purpose**: Uniquely identify transactions for visibility decisions.

**XID Characteristics**:
- 32-bit unsigned integer (wraps around at ~4 billion)
- Monotonically increasing
- `pg_current_xact_id()` returns current XID

**Special XIDs**:
| XID | Meaning |
|-----|---------|
| `0` | Invalid transaction |
| `1` | Bootstrap (initial database creation) |
| `2` | Frozen XID |

**XID Wraparound**:
- XID space is finite (≈4 billion)
- Wraparound causes visibility issues (new XIDs appear older than frozen ones)
- **Vacuum freezing** marks old tuples as frozen (XID=2)

### 20. Tuple Visibility Rules

**Purpose**: Determine whether a transaction should see a particular tuple version.

**Visibility Decision Based on t_xmin and t_xmax**:

| Condition | Visibility |
|-----------|------------|
| `t_xmin` is aborted | Invisible |
| `t_xmin` is in progress and not current transaction | Invisible |
| `t_xmin` is committed and `t_xmax` = 0 | Visible |
| `t_xmin` is committed and `t_xmax` is in progress or aborted | Visible |
| `t_xmin` is committed and `t_xmax` is committed and `t_xmax` ≥ current XID | Visible (deleted by future transaction) |
| `t_xmin` is committed and `t_xmax` is committed and `t_xmax` < current XID | **Invisible** (deleted by committed transaction) |

**Snapshot Types**:

| Snapshot Type | Includes | Use Case |
|---------------|----------|----------|
| `SnapshotData` (Normal) | Committed transactions before snapshot creation | READ COMMITTED isolation |
| `SerializableSnapshot` | For SERIALIZABLE isolation | Serializable transactions |

### 21. Transaction Isolation Levels

PostgreSQL supports four isolation levels (but only three are actually distinct):

| Isolation Level | Dirty Read | Non-Repeatable Read | Phantom Read | Serialization Anomaly |
|----------------|-----------|---------------------|--------------|----------------------|
| READ UNCOMMITTED | Possible | Possible | Possible | Possible |
| READ COMMITTED | Not possible | Possible | Possible | Possible |
| REPEATABLE READ | Not possible | Not possible | Not possible | Possible |
| SERIALIZABLE | Not possible | Not possible | Not possible | Not possible |

**Implementation Mechanisms**:

| Level | Snapshot Timing | Behavior |
|-------|-----------------|----------|
| **READ COMMITTED** | New snapshot per statement | Sees changes committed before statement starts |
| **REPEATABLE READ** | Single snapshot for transaction | Sees consistent state as of first query |
| **SERIALIZABLE** | REPEATABLE READ + predicate locking | Detects serialization anomalies |

### 22. Serializable Snapshot Isolation (SSI)

**Purpose**: Detect and prevent serialization anomalies at SERIALIZABLE isolation level.

**Key Insight**:
- READ COMMITTED and REPEATABLE READ use standard MVCC
- SERIALIZABLE must detect conflicts that would cause non-serializable behavior

**SSI Detection Mechanism**:
- Tracks read/write dependencies between transactions
- Detects dangerous structures (cycles in dependency graph)
- Aborts transactions causing anomalies

**Predicate Locking in SSI**:
- Locks on **predicates** (e.g., `WHERE age > 25`), not just individual rows
- Implemented via SIREAD locks
- Over-conservative but safe

### 23. Vacuum & Tuple Dead Space Reclamation

**Purpose**: Remove dead tuple versions and reclaim storage.

**Why Vacuum is Necessary**:
- MVCC creates dead tuples (updated/deleted versions)
- Dead tuples occupy space and cause table bloat
- XID wraparound requires freezing

**Vacuum Types**:

| Type | Command | Description |
|------|---------|-------------|
| **Concurrent Vacuum** | `VACUUM` | Marks space as reusable, does not return to OS |
| **Full Vacuum** | `VACUUM FULL` | Rewrites entire table, returns space to OS (exclusive lock) |
| **Autovacuum** | Automatic | Background process, configurable thresholds |

**Vacuum Process**:
1. Scan table using Visibility Map to skip all-visible pages
2. Remove dead tuple versions
3. Update FSM with freed space
4. Update VM for pages that become all-visible
5. Freeze old tuples (set t_xmin = 2)

**Autovacuum Triggers** :
- Number of dead tuples exceeds `autovacuum_vacuum_threshold` (default 50) + `autovacuum_vacuum_scale_factor` × table size
- Transaction age exceeds `autovacuum_freeze_max_age` (200M)
- Enforced by background autovacuum workers

---

## Query Processing & Optimization

### 24. Query Lifecycle

**Purpose**: Transform SQL text into executed results.

**Stages**:

```
SQL Query
    │
    ▼
┌──────────────┐
│  Parser      │ → Parse tree (raw syntax)
└──────┬───────┘
       │
       ▼
┌──────────────┐
│  Analyzer    │ → Query tree (resolved names, types)
└──────┬───────┘
       │
       ▼
┌──────────────┐
│  Rewriter    │ → Query tree (rules applied)
└──────┬───────┘
       │
       ▼
┌──────────────┐
│  Planner     │ → Paths (possible execution methods)
└──────┬───────┘
       │
       ▼
┌──────────────┐
│  Optimizer   │ → Plan (cheapest path)
└──────┬───────┘
       │
       ▼
┌──────────────┐
│  Executor    │ → Results
└──────────────┘
```

### 25. Planner & Optimizer

**Purpose**: Generate the most efficient execution plan for a given query .

**Core Responsibility**: A given SQL query can be executed in many different ways. The optimizer examines possible execution plans and selects the one expected to run fastest .

**Path Generation**:

For each base relation (table):
- **Sequential scan** (always available)
- **Index scan(s)** (if matching indexes exist)
- **Bitmap heap scan** (combining multiple indexes)

For joins:
- **Nested Loop Join**: Right relation scanned once for each left row
- **Merge Join**: Both relations sorted on join key, then merged
- **Hash Join**: Build hash table on right relation, probe with left 

**Join Order Search**: For queries with many joins, the search space is factorial. PostgreSQL uses:
- Exhaustive search for < `geqo_threshold` joins (default 12)
- **Genetic Query Optimizer (GEQO)** for larger queries 

### 26. Genetic Query Optimizer (GEQO)

**Purpose**: Find reasonable (not necessarily optimal) join order for complex queries.

**When Used**: When the number of joins exceeds `geqo_threshold` (default 12) .

**Algorithm**: Uses genetic algorithm to explore join order space efficiently.

**GEQO Configuration**:

| Parameter | Default | Description |
|-----------|---------|-------------|
| `geqo_threshold` | 12 | Minimum joins to use GEQO |
| `geqo_pool_size` | 0 (auto) | Population size |
| `geqo_effort` | 5 | Effort (1-10) for optimization |
| `geqo_generations` | 0 (auto) | Number of generations |

### 27. Join Algorithms

**Nested Loop Join** :
- Best for small outer relations
- Works well with inner index scans
- Complexity: O(|L| × |R|)

**Merge Join** :
- Requires sorted input on join key
- Each relation scanned once
- Complexity: O(|L| + |R|)
- Good for large, sorted relations

**Hash Join** :
- Build hash table from smaller relation
- Probe with larger relation
- Complexity: O(|L| + |R|) amortized
- Best for large, unsorted relations
- Memory intensive (may spill to disk)

### 28. Join Order Search Strategies

**Exhaustive Search** (≤ geqo_threshold):
- Generates all possible join sequences
- Prunes using dynamic programming
- Keeps cheapest path for each join combination

**Genetic Search** (> geqo_threshold):
- Evolves a population of join orders
- Fitness = estimated cost
- Crossover and mutation operations
- Returns best found solution

### 29. Cost Model

**Purpose**: Estimate the cost of executing a plan.

**Cost Units**: Arbitrary (disk I/O + CPU) normalized units.

**Cost Components**:

| Component | Formula | Description |
|-----------|---------|-------------|
| Startup cost | `startup` | Cost before first tuple |
| Total cost | `total` | Cost to retrieve all tuples |

**Parameter Influence**:

| Parameter | Effect |
|-----------|--------|
| `seq_page_cost` | Cost of sequential page read (default 1.0) |
| `random_page_cost` | Cost of random page read (default 4.0) |
| `cpu_tuple_cost` | Cost to process a tuple (default 0.01) |
| `cpu_index_tuple_cost` | Cost to process index tuple (default 0.005) |
| `cpu_operator_cost` | Cost per operator (default 0.0025) |

**Cardinality Estimation**:
- Uses table statistics (`pg_statistic`, `pg_class`)
- Estimates selectivity of WHERE clauses
- Accuracy impacts plan quality significantly

### 30. Adaptive Query Processing

**Research Context**: Recent VLDB 2023 research has evaluated adaptive query processing techniques that adjust execution based on runtime feedback .

**LIP (Lookahead Information Passing)**:
- Uses Bloom filters to implement semijoins
- Orders filters adaptively at runtime
- Can match optimal join order performance even with suboptimal plan 

**AJA (Adaptive Join Algorithm)**:
- Monitors join execution at runtime
- Can switch between Nested Loop and Hash Join dynamically
- Avoids suboptimal algorithm choices 

**Key Finding**: Simple adaptive techniques can match or outperform learned (ML-based) query optimizers in many scenarios .

---

## Replication & High Availability

### 31. Physical (Streaming) Replication

**Purpose**: Provide real-time copying of entire database cluster to standby servers .

**Architecture**:

```
Primary Server                     Standby Server
┌─────────────────┐               ┌─────────────────┐
│  Transaction    │               │  Recovery       │
│  executes       │               │  Process        │
└────────┬────────┘               └────────▲────────┘
         │                                 │
         ▼                                 │
┌─────────────────┐     WAL Records        │
│  WAL Generated  │────────────────────────┤
│  (pg_wal)       │                        │
└────────┬────────┘                        │
         │                                 │
         ▼                                 │
┌─────────────────┐               ┌────────┴────────┐
│  WAL Sender     │               │  WAL Receiver   │
│  Process        │──────────────▶│  Process        │
└─────────────────┘               └─────────────────┘
```

**Components** :
- **WAL Sender** (primary): Streams WAL records to standby
- **WAL Receiver** (standby): Receives and applies WAL
- **Recovery Process** (standby): Applies WAL to data pages

**Synchronous vs. Asynchronous** :

| Mode | Behavior | Data Loss Risk | Performance |
|------|----------|----------------|-------------|
| **Asynchronous** | Commit acknowledged when primary writes WAL | Small window (unshipped WAL) | Highest |
| **Synchronous** | Wait for standby confirmation | Near-zero (if ≥1 standby confirms) | Lower latency |

**Synchronous Commit Options**:
- `synchronous_commit = on`: Wait for sync standby
- `synchronous_commit = remote_write`: Wait for WAL receipt but not apply
- `synchronous_commit = off`: No waiting

### 32. Logical Replication

**Purpose**: Replicate at the table level rather than entire cluster .

**Architecture**: Publisher-Subscriber model .

**Components** :

| Component | Purpose | Configuration |
|-----------|---------|---------------|
| **Publication** | Set of tables to replicate | `CREATE PUBLICATION` |
| **Subscription** | Connection to publisher + tables to receive | `CREATE SUBSCRIPTION` |
| **Logical Decoder** | Extracts changes from WAL | `pgoutput` plugin |

**Logical vs. Physical Replication** :

| Feature | Physical Replication | Logical Replication |
|---------|---------------------|---------------------|
| **Scope** | Entire cluster | Specific tables |
| **Version compatibility** | Same version required | Different versions allowed |
| **Write location** | Read-only standby | Can be writable (but conflicts possible) |
| **DML filtering** | No | Yes (by WHERE clause) |
| **DDL replication** | Yes (full schema) | No |

**Publication Example**:
```sql
-- All tables
CREATE PUBLICATION mypub FOR ALL TABLES;

-- Specific tables
CREATE PUBLICATION sales_pub FOR TABLE orders, customers;

-- Filtered rows (PostgreSQL 15+)
CREATE PUBLICATION online_orders FOR TABLE orders WHERE status = 'online';
```

**Subscription Example**:
```sql
CREATE SUBSCRIPTION mysub 
CONNECTION 'host=primary port=5432 dbname=mydb' 
PUBLICATION mypub;
```

### 33. Logical Replication Slots

**Purpose**: Ensure changes are retained until confirmed by all subscribers .

**How Replication Slots Work**:
- On the publisher, slot tracks LSN of oldest change not yet received by subscriber
- PostgreSQL prevents removal of WAL that still has dependent slot
- Prevents subscriber from falling behind and losing data

**Slot Lifecycle**:
1. Created automatically when subscription created
2. Updated as subscriber acknowledges receipt
3. Dropped when subscription removed
4. Must be manually cleaned if orphaned

**Monitoring**:
```sql
SELECT * FROM pg_replication_slots;
```

### 34. Hot Standby

**Purpose**: Allow read-only queries on a standby server while it applies WAL .

**Benefits**:
- Offload read traffic from primary
- Near real-time data access on replicas
- Test query performance without affecting production

**Limitations**:
- Conflicts can occur when recovery needs to modify a page being read
- Conflicts cause query cancellation (configurable via `max_standby_streaming_delay`)

**Conflict Types**:
| Conflict | Solution |
|----------|----------|
| Access exclusive lock on primary | Query may be cancelled |
| Dropped tablespace | Query cancellation |
| Deadlock with recovery | Query cancellation |

### 35. Multi-Master Replication

**Purpose**: Allow write operations on multiple master nodes simultaneously.

**Challenges** :
- **Write conflicts**: Same data modified on different nodes
- **Conflict resolution**: Deterministic (LWW) or user-defined
- **Convergence**: All nodes eventually consistent
- **Performance**: Replication overhead increases

**Third-Party Solutions for PostgreSQL** :
| Solution | Description |
|----------|-------------|
| **Bucardo** | Asynchronous multi-master |
| **pgpool-II** | Statement-based replication |
| **Postgres-BDR** | Logical replication-based multi-master (EOL) |
| **pgEdge** | Active-active multi-master |

**Native Support**: PostgreSQL does not have native multi-master; relies on extensions.

---

## Complete System Interaction Diagram

```
┌─────────────────────────────────────────────────────────────────────────────┐
│                          CLIENT APPLICATION                                  │
│  ┌──────────────┐  ┌──────────────┐  ┌──────────────┐                       │
│  │  psql        │  │  JDBC        │  │  psycopg2    │                       │
│  │  (terminal)  │  │  Driver      │  │  (Python)    │                       │
│  └──────┬───────┘  └──────┬───────┘  └──────┬───────┘                       │
│         │                 │                 │                               │
│         └─────────────────┼─────────────────┘                               │
│                           │ (TCP connection, default port 5432)             │
└───────────────────────────┼─────────────────────────────────────────────────┘
                            │
                            ▼
┌─────────────────────────────────────────────────────────────────────────────┐
│                         POSTMASTER (postmaster)                              │
│                                                                              │
│  ┌─────────────────────────────────────────────────────────────────────┐    │
│  │  • Listens for new connections                                       │    │
│  │  • Authenticates client                                              │    │
│  │  • Forks new backend process on connection                          │    │
│  │  • Spawns utility processes:                                         │    │
│  │    - Background Writer                                               │    │
│  │    - Checkpointer                                                    │    │
│  │    - WAL Writer                                                      │    │
│  │    - Autovacuum Workers                                             │    │
│  │    - Stats Collector                                                 │    │
│  └─────────────────────────────────────────────────────────────────────┘    │
│                                                                              │
└─────────────────────────────────────────────────────────────────────────────┘
                            │
                            ▼
┌─────────────────────────────────────────────────────────────────────────────┐
│                         BACKEND PROCESS (per connection)                    │
│                                                                              │
│  ┌─────────────────────────────────────────────────────────────────────┐    │
│  │                         QUERY PROCESSING PIPELINE                    │    │
│  │                                                                       │    │
│  │  SQL Text ──► Parser ──► Analyzer ──► Rewriter ──► Planner ──► Executor │    │
│  │                                                                       │    │
│  │  ┌────────────┐  ┌────────────┐  ┌────────────┐  ┌────────────┐     │    │
│  │  │ Parse Tree │  │ Query Tree │  │ Query Tree │  │   Paths    │     │    │
│  │  │            │  │ (resolved) │  │ (with      │  │  (possible │     │    │
│  │  │            │  │            │  │  rules)    │  │   plans)   │     │    │
│  │  └────────────┘  └────────────┘  └────────────┘  └────────────┘     │    │
│  │                                                                       │    │
│  └─────────────────────────────────────────────────────────────────────┘    │
│                                                                              │
└─────────────────────────────────────────────────────────────────────────────┘
                            │
          ┌─────────────────┼─────────────────┬─────────────────┐
          │                 │                 │                 │
          ▼                 ▼                 ▼                 ▼
┌─────────────────┐  ┌─────────────────┐  ┌─────────────────┐  ┌─────────────────┐
│   SHARED        │  │   LOCAL         │  │   WAL BUFFER    │  │   TEMP FILES    │
│   BUFFERS       │  │   PROCESS       │  │                 │  │                 │
│                 │  │   MEMORY        │  │                 │  │                 │
│  ┌───────────┐  │  │                 │  │  ┌───────────┐  │  │                 │
│  │ 8KB Pages │  │  │  • Sort memory │  │  │ WAL       │  │  │  • Spill to    │
│  │           │  │  │  • Hash tables │  │  │ Records   │  │  │    disk        │
│  │ (shared_  │  │  │  • Tuple       │  │  │           │  │  │  • Temporary   │
│  │  buffers) │  │  │    storage    │  │  └───────────┘  │  │    tables      │
│  └───────────┘  │  │                 │  │                 │  │                 │
└─────────────────┘  └─────────────────┘  └─────────────────┘  └─────────────────┘
          │                 │                 │                 │
          └─────────────────┼─────────────────┴─────────────────┘
                            │
                            ▼
┌─────────────────────────────────────────────────────────────────────────────┐
│                         STORAGE LAYER                                        │
│                                                                              │
│  ┌─────────────────────────────────────────────────────────────────────┐    │
│  │  $PGDATA/base/                                                       │    │
│  │                                                                       │    │
│  │  Database OID 12345/                                                 │    │
│  │  ├── 12345          ← Main table file (heap)                         │    │
│  │  ├── 12345_fsm      ← Free Space Map                                 │    │
│  │  ├── 12345_vm       ← Visibility Map                                 │    │
│  │  ├── 12346          ← TOAST table                                    │    │
│  │  ├── 12346_fsm                                                        │    │
│  │  ├── 12346_vm                                                         │    │
│  │  └── 12347          ← Index (B-tree, GIN, etc.)                      │    │
│  └─────────────────────────────────────────────────────────────────────┘    │
│                                                                              │
│  ┌─────────────────────────────────────────────────────────────────────┐    │
│  │  $PGDATA/pg_wal/        ← Write-Ahead Log (WAL)                      │    │
│  │  ├── 000000010000000000000001  (16 MB segment)                       │    │
│  │  └── ...                                                              │    │
│  └─────────────────────────────────────────────────────────────────────┘    │
│                                                                              │
└─────────────────────────────────────────────────────────────────────────────┘
                            │
                            ▼
┌─────────────────────────────────────────────────────────────────────────────┐
│                         WAL & REPLICATION                                    │
│                                                                              │
│  Primary:                            Standby:                               │
│  ┌─────────────────────────────┐    ┌─────────────────────────────────┐    │
│  │  Transaction Commit          │    │  WAL Receiver                  │    │
│  │         │                    │    │         │                       │    │
│  │         ▼                    │    │         ▼                       │    │
│  │  WAL Written (pg_wal)        │    │  WAL Received (pg_wal)          │    │
│  │         │                    │    │         │                       │    │
│  │         ▼                    │    │         ▼                       │    │
│  │  WAL Sender ─────────────────┼───►│  Recovery Process              │    │
│  │                              │    │         │                       │    │
│  │                              │    │         ▼                       │    │
│  │                              │    │  Applied to Data Pages         │    │
│  │                              │    │                                 │    │
│  │  Replication Slots:          │    │  Hot Standby Queries:          │    │
│  │  - Name: mysub               │    │  - Read-only access            │    │
│  │  - Restart LSN: 0/3000000    │    │  - MVCC snapshot               │    │
│  │  - Active: true              │    │                                 │    │
│  └─────────────────────────────┘    └─────────────────────────────────┘    │
│                                                                              │
└─────────────────────────────────────────────────────────────────────────────┘
```

---

## Algorithm & Pattern Summary Table

| # | Algorithm/Pattern | Primary Purpose | PostgreSQL Component |
|---|------------------|-----------------|---------------------|
| 1 | Process-per-User | Connection isolation | Postmaster + Backend |
| 2 | Shared Buffer Pool | Page caching | `shared_buffers` |
| 3 | MVCC with Tuple Versioning | Concurrent access without locks | `t_xmin`/`t_xmax` |
| 4 | Transaction ID (XID) | Transaction identification | `pg_xact` |
| 5 | Snapshot Isolation | Consistent view per transaction | `SnapshotData` |
| 6 | Serializable Snapshot Isolation (SSI) | True serializable isolation | SSI detection |
| 7 | Tuple Visibility Rules | Determine which version visible | Visibility check |
| 8 | Vacuum (Concurrent/Full) | Dead tuple reclamation | Autovacuum, VACUUM |
| 9 | TOAST (Oversized storage) | Large value handling | TOAST tables |
| 10 | TOAST Compression (LZ4/PGLZ) | Space reduction | `default_toast_compression` |
| 11 | Free Space Map (Tree) | Free space tracking | `_fsm` files |
| 12 | Visibility Map (Bits) | All-visible/all-frozen tracking | `_vm` files |
| 13 | B-Tree Index (O(log n)) | Equality and range queries | `btree` access method |
| 14 | GiST Index | Generalized search tree | `gist` access method |
| 15 | GIN Index (Inverted) | Compound value indexing | `gin` access method |
| 16 | BRIN Index (Block Range) | Large correlated tables | `brin` access method |
| 17 | Nested Loop Join | Small outer relations | Join executor |
| 18 | Merge Join | Sorted inputs | Join executor |
| 19 | Hash Join | Large unsorted relations | Join executor |
| 20 | Genetic Query Optimizer (GEQO) | Complex join ordering | GEQO planner |
| 21 | Cost-based Optimization | Plan selection | Query optimizer |
| 22 | Cardinality Estimation | Row count prediction | `pg_statistic` |
| 23 | WAL (Write-Ahead Log) | Durability | `pg_wal` |
| 24 | Streaming Replication | Real-time copy | WAL sender/receiver |
| 25 | Logical Replication (Pub/Sub) | Table-level replication | Publication/Subscription |
| 26 | Hot Standby | Read-only replicas | Recovery process |
| 27 | Replication Slots | Change retention | `pg_replication_slots` |
| 28 | Checkpoint | Durability + recovery | Checkpointer |
| 29 | Adaptive Query Processing (LIP+AJA) | Runtime plan adjustment | Research/Extensions |

---

## Configuration Reference

### Postgresql.conf (Key Settings)

```ini
# Connections
listen_addresses = 'localhost'
port = 5432
max_connections = 100

# Memory
shared_buffers = 128MB           # Buffer pool size (typically 25% of RAM)
work_mem = 4MB                   # Sort/hash operations per query
maintenance_work_mem = 64MB      # Vacuum, index creation
effective_cache_size = 4GB       # OS cache estimate

# Write-Ahead Log
wal_level = replica              # minimal, replica, logical
synchronous_commit = on          # on, off, remote_write, remote_apply
max_wal_senders = 10
wal_keep_size = 1GB

# Checkpoints
checkpoint_timeout = 5min
max_wal_size = 1GB
min_wal_size = 80MB

# Query Planning
enable_seqscan = on
enable_indexscan = on
enable_hashjoin = on
enable_mergejoin = on
geqo_threshold = 12              # Use GEQO for >12 joins
geqo_effort = 5

# Cost Parameters
seq_page_cost = 1.0
random_page_cost = 4.0
cpu_tuple_cost = 0.01
cpu_index_tuple_cost = 0.005
cpu_operator_cost = 0.0025

# Autovacuum
autovacuum = on
autovacuum_vacuum_threshold = 50
autovacuum_vacuum_scale_factor = 0.2
autovacuum_freeze_max_age = 200000000

# Replication (standby)
primary_conninfo = 'host=primary port=5432 user=replica'
hot_standby = on
max_standby_streaming_delay = 30s
```

### Index Creation Examples

```sql
-- B-Tree (default)
CREATE INDEX idx_name ON table (column);

-- B-Tree with operator class
CREATE INDEX idx_text_pattern ON table (text_col text_pattern_ops);

-- GIN for JSONB
CREATE INDEX idx_jsonb ON table USING GIN (jsonb_col);

-- GIN for Full-Text Search
CREATE INDEX idx_fts ON table USING GIN (to_tsvector('english', content));

-- BRIN for large time-series
CREATE INDEX idx_brin ON logs USING BRIN (created_at);

-- Hash index
CREATE INDEX idx_hash ON users USING HASH (email);

-- Partial index
CREATE INDEX idx_active ON users (email) WHERE active = true;

-- Expression index
CREATE INDEX idx_lower_email ON users (LOWER(email));
```

### Publication & Subscription Examples

```sql
-- Create publication
CREATE PUBLICATION sales_pub FOR TABLE orders, customers;

-- Create subscription
CREATE SUBSCRIPTION sales_sub 
CONNECTION 'host=192.168.1.10 dbname=prod'
PUBLICATION sales_pub;

-- Drop subscription (with slot cleanup)
DROP SUBSCRIPTION sales_sub;

-- Add table to publication
ALTER PUBLICATION sales_pub ADD TABLE new_table;
```

---

## Performance & Complexity Reference

| Operation | Complexity | Typical Factors |
|-----------|------------|-----------------|
| B-Tree point lookup | O(log n) | Tree depth = 3-5 for millions of rows |
| B-Tree range scan | O(log n + m) | m = number of rows returned |
| Sequential scan | O(n) | n = table size in pages |
| Hash join | O(n + m) | Assumes hash table fits in memory |
| Merge join | O(n + m) | Requires sorted input |
| Nested loop join | O(n × m) | With inner index: O(n × log m) |
| Tuple visibility check | O(1) | Checks XID status |
| INSERT (with index) | O(log n per index) | Multiple indexes increase cost |
| VACUUM full | O(n) | Rewrites entire table |
| Autovacuum (concurrent) | O(dead tuples) | Leaves table accessible |
| WAL write | O(1) per transaction | Group commit optimizes |
| TOAST compression | O(value size) | CPU-bound |
| GIN index update | O(keys) | Multiple keys per value |
| BRIN index scan | O(stripe count) | Very fast for correlated data |

---

## Comparison with Other Databases

| Feature | PostgreSQL | MySQL (InnoDB) | SQL Server |
|---------|------------|----------------|------------|
| Concurrency model | MVCC (tuple versions) | MVCC (undo log) | MVCC (TempDB) |
| Index types | B-tree, GIN, GiST, SP-GiST, BRIN, Hash | B-tree, Hash, Full-text | B-tree, Columnstore, Hash, XML |
| Full-text search | Native (tsvector) | Native | Native |
| JSON support | JSONB (binary) | JSON | JSON |
| Replication | Physical + Logical | Async (binary log) | AlwaysOn |
| Multi-master | Extensions only | Group Replication | AlwaysOn |
| Query parallelism | Yes (scan/join) | Yes (scan only) | Yes |
| Open source | Yes (PostgreSQL License) | Yes (GPL) | No |

---

## Source Code Reference

| Component | Location (PostgreSQL Git) |
|-----------|--------------------------|
| Storage (heap) | `src/backend/access/heap/` |
| Index Access Methods | `src/backend/access/{btree,gin,gist,hash,brin}/` |
| TOAST | `src/backend/access/common/toast_internals.c` |
| MVCC | `src/backend/storage/lmgr/` |
| WAL | `src/backend/access/transam/` |
| Planner/Optimizer | `src/backend/optimizer/` |
| Replication | `src/backend/replication/` |
| Buffer Management | `src/backend/storage/buffer/` |
| Free Space / Visibility Map | `src/backend/storage/freespace/` |

---

## Conclusion

PostgreSQL's design philosophy emphasizes:

- **Standards compliance**: SQL:2023 conformance
- **Extensibility**: Custom data types, operators, indexes, access methods
- **Data integrity**: ACID compliance, foreign keys, constraints
- **Concurrency without blocking**: MVCC readers never block writers
- **Reliability**: Write-ahead logging, point-in-time recovery
- **Rich feature set**: JSONB, full-text search, geospatial (PostGIS), full-text search

Key innovations and algorithms include:

- **Multi-Version Concurrency Control (MVCC)**: Tuple versioning with XMIN/XMAX for non-blocking reads/writes
- **Serializable Snapshot Isolation (SSI)**: Detecting serialization anomalies at SERIALIZABLE level
- **TOAST (Oversized-Attribute Storage)**: Transparent out-of-line storage for large values with compression
- **Free Space Map & Visibility Map**: Page-level free space and visibility tracking for efficiency
- **Genetic Query Optimizer (GEQO)**: Reasonable join order for complex queries (>12 joins)
- **Diverse Indexing Methods**: B-tree (default), GIN (arrays, JSONB), GiST (geometric), BRIN (time-series), Hash
- **WAL-based Replication**: Physical (streaming) and logical (pub/sub) replication
- **Process-based architecture**: OS-level isolation for connections

This combination of algorithms and patterns makes PostgreSQL suitable for:
- **OLTP workloads**: High-concurrency transaction processing
- **Analytics**: Advanced indexing, parallel queries, partitioning
- **Geospatial applications**: PostGIS extension
- **Full-text search**: Built-in capabilities
- **JSON document stores**: JSONB with GIN indexing
- **Enterprise applications**: ACID compliance, referential integrity

---

*Document Version: 1.0*
*Based on PostgreSQL official documentation, internals guides, and academic research*