# Datadog: Complete Observability Platform Reference

## Document Overview

This document provides a comprehensive analysis of Datadog's architectural patterns, storage algorithms, data processing techniques, and observability strategies. Datadog is a cloud-scale observability platform that monitors infrastructure, applications, logs, and security in a unified platform. Unlike traditional monitoring tools that store data as-is, Datadog operates as a real-time metrics and event-processing system designed for high cardinality, massive throughput, and global scale . This document covers the core architecture, timeseries storage engine evolution, event storage compaction strategies, distributed tracing sampling mechanisms, and data ingestion optimization techniques that power Datadog's observability platform.

---

## Table of Contents

1. [Core Architectural Patterns](#core-architectural-patterns)
2. [Timeseries Storage Engine Evolution](#timeseries-storage-engine-evolution)
3. [LSM-Tree Storage & Compaction (Husky)](#lsm-tree-storage--compaction-husky)
4. [Intake & Sharding Architecture](#intake--sharding-architecture)
5. [Distributed Tracing & Sampling](#distributed-tracing--sampling)
6. [Database Monitoring & Index Optimization](#database-monitoring--index-optimization)
7. [Log Ingestion (BYOC) Architecture](#log-ingestion-byoc-architecture)
8. [Complete System Interaction Diagram](#complete-system-interaction-diagram)

---

## Core Architectural Patterns

### 1. Unified Observability Data Model

**Purpose**: Provide a single platform for metrics, traces, logs, and security data with consistent querying and visualization.

**Core Telemetry Types at Datadog**:

| Telemetry Type | Description | Storage System | Query Pattern |
|----------------|-------------|----------------|---------------|
| **Metrics** | Timeseries data points (e.g., CPU, memory, request rate) | RTDB (Real-time database) + Long-term storage | Aggregations over time ranges |
| **Traces** | Distributed request spans | Husky (event store) + Trace storage | Trace ID lookup, latency analysis |
| **Logs** | Structured and unstructured text | Husky (event store) | Full-text search, time-based queries |
| **Distributions** | DDSketch percentile data | RocksDB (in Gen 5, now unified) | Percentile estimations at query time |

**The Unified Platform Advantage**:
- **Correlation across telemetry types**: Traces linked to logs, metrics from same hosts
- **Single query language**: Standardized query syntax across all data types
- **Global tag system**: Consistent tagging across all ingested data

### 2. Separation of Storage Responsibilities

**Purpose**: Split real-time and long-term storage concerns into independently scalable services .

**Architectural Split**:

```
┌─────────────────────────────────────────────────────────────────┐
│                    Datadog Metrics Platform                       │
├─────────────────────────────────────────────────────────────────┤
│                                                                   │
│  ┌─────────────────────────┐    ┌─────────────────────────┐     │
│  │   Index Database        │    │   Real-Time Database    │     │
│  │   (Timeseries metadata) │    │   (RTDB - Metric values)│     │
│  │                         │    │                         │     │
│  │ Stores:                 │    │ Stores:                 │     │
│  │ - Metric identifiers    │    │ - <timeseries_id,       │     │
│  │ - Tag key-value pairs   │    │   timestamp, value>     │     │
│  │ - Timeseries mappings   │    │ - Point-in-time data    │     │
│  └─────────────────────────┘    └─────────────────────────┘     │
│                                                                   │
└─────────────────────────────────────────────────────────────────┘
```

**Why Separation Matters**:

| Storage Type | Query Pattern | Scale Characteristics |
|--------------|---------------|----------------------|
| **Index Database** | Lookup by tags, metadata search | High cardinality, moderate throughput |
| **RTDB** | Time-range scans, aggregations | Massive write throughput, time-based reads |

### 3. Shard-Per-Core Architecture

**Purpose**: Distribute data and processing evenly across all CPU cores to eliminate contention and maximize parallelism .

**Core Principle**: Each unit of data—identified by a timeseries key—is assigned to a shard using consistent hashing. Each shard is a self-contained unit responsible for a portion of the keyspace and operates on a dedicated CPU core.

**Sharding Across All Layers**:

| Layer | Sharding Strategy | Benefit |
|-------|------------------|---------|
| **Ingestion pipelines** | Data processed in parallel per Kafka partition | Even load distribution |
| **Caches** | Each shard manages its own cache of hot data | No cross-shard contention |
| **Storage files** | Each shard writes to its own files | Isolated I/O, simpler compaction |
| **Query processing** | Each shard executes queries for its subset in parallel | Linear scalability with cores |

**Concurrency Model**: Each shard operates almost like a single-tenant system. With proper distribution, contention remains minimal even in worst-case scenarios .

---

## Timeseries Storage Engine Evolution

### 4. Gen 1: Cassandra

**Purpose**: Initial storage backend providing write scalability .

**Characteristics**:
- Inspired by OpenTSDB and HBase-based systems
- Good write scalability
- Limited query flexibility for real-time alerting
- Struggled with large dataset returns

**Key Limitations**: Could not support breadth or complexity of real-time queries needed for alerting and analysis.

### 5. Gen 2: Redis

**Purpose**: Performance improvement with in-memory storage .

**Strengths**:
- Fast reads, flexible querying
- Simple operational model

**Limitations**:
- No built-in clustering (Datadog managed many independent instances)
- Single-threaded nature limited snapshotting for durability
- Rare but severe failure modes under memory pressure
- Constant (de)serialization overhead
- Suboptimal memory layout and disk I/O patterns

**Operational Insight**: Redis gave valuable visibility into what a purpose-built system would need—tighter integration, full I/O control, and better efficiency.

### 6. Gen 3: MDBM (Memory-Mapped Key-Value Store)

**Purpose**: Leverage OS page cache via `mmap` for efficient I/O .

**How It Worked**:
- Used memory-mapped I/O to load database pages on demand
- Treated on-disk data as in-memory structures
- Leveraged OS page cache automatically

**Limitations**:
- Performance didn't scale to largest workloads
- Subtle performance and correctness issues with `mmap` at scale
- Many systems that started with `mmap` eventually switched to explicit I/O management

### 7. Gen 4: Go-Based B+ Tree

**Purpose**: Custom storage engine for improved performance .

**Key Features**:
- First step toward thread-per-core execution model
- Go's scheduler supported parallelism reasonably well
- Major improvements in throughput and latency

**Limitation**: Optimized for scalar floats, not easily extendable to distribution metrics (DDSketch).

### 8. Gen 5: Distribution Metrics + RocksDB

**Purpose**: Support for DDSketch percentile estimation .

**DDSketch**: Probabilistic data structure that provides accurate percentile estimates with bounded error.

**Architecture Decision**: Integrated RocksDB as storage engine for DDSketch data while keeping Go engine for scalars.

**Result**: Two parallel timeseries databases—adding complexity and duplication.

### 9. Gen 6: Unified Rust-Based LSM-Tree Engine

**Purpose**: Consolidate scalar and distribution metrics into a single engine with better performance .

**Results Achieved**:

| Metric | Improvement |
|--------|-------------|
| Ingestion performance | **60x increase** |
| Query performance | **5x faster** at peak scale |

**Why Rust**:
- Low-level control without sacrificing safety
- Strong results already seen in other parts of Datadog infrastructure
- Modularity for reusable components across the company

**Architecture**:
- Log-Structured Merge Tree (LSM tree) for write-heavy workloads
- Early sharding across entire system (not just storage)
- Async ingestion pipeline with Tokio

**Ingestion Pipeline**:

```
Intake Workers (per Kafka partition)
         │
         │ MPSC channels (tachyonix)
         ▼
Storage Shards (per CPU core)
         │
         ▼
LSM Tree (buffered writes → flushed to disk)
```

**Channel Configuration**: In typical deployment, number of storage shards exceeds number of intake workers .

**Cooperative Scheduling**: Storage tasks yield periodically to prevent starvation, enabling simpler logic elsewhere in the system.

---

## LSM-Tree Storage & Compaction (Husky)

### 10. Husky Event Store Architecture

**Purpose**: Distributed storage system layered over object storage (S3, GCS, Azure Blob) for observability data .

**Key Observation**: Observability data (logs, traces) follows a distinct pattern—massive write load, few updates, queries biased toward recent data .

**Storage Model**: Data aggregated into files called **fragments**, saved to object storage. Separate metadata for each fragment enables query planning.

**Query Execution**:

```
Query Request
      │
      ▼
Metadata Service ──► Discover relevant fragments
      │
      ▼
Worker Pool (distributed)
      │
      ├── Worker 1 ──► Scan Fragment 1
      ├── Worker 2 ──► Scan Fragment 2
      └── Worker N ──► Scan Fragment N
      │
      ▼
Merge results ──► Response
```

**Query Cost Factors**:
1. Number of fragments fetched from object storage
2. Number of events scanned within files

### 11. Compact-While-Write (CWW) Strategy

**Purpose**: Control file count and organize data for efficient query scanning .

**Husky's Compaction Approach**:

Traditional compaction:

| Strategy | Behavior | Trade-off |
|----------|----------|-----------|
| **Size-tiered** | Merge similar-sized files | High write amplification, simpler |
| **Leveled** | Maintain size tiers | Lower read amplification, more CPU |

**Husky's Innovation**: Streaming merge approach that compacts while writing, avoiding separate heavy compaction phases .

**File Organization Goals**:
- Sort data for efficient range queries
- Build indexes on sorted columns
- Spread data to limit events scanned per query

### 12. Throttling Mechanisms

**Purpose**: Maintain stability under traffic surges or expensive queries .

**RTDB Throttling Types**:

| Type | Mechanism | Use Case |
|------|-----------|----------|
| **Permit-based** | Request must acquire permit before processing | High-priority operations, rate limiting |
| **Scheduling-based** | Fair scheduling across competing tasks | Background operations, bulk processing |

**Why Throttling Is Critical**: Even perfect sharding cannot prevent a single expensive query from overwhelming a node during traffic spikes .

---

## Intake & Sharding Architecture

### 13. DogStatsD Internal Pipeline

**Purpose**: Efficient metrics aggregation and forwarding from the Datadog Agent .

**Pipeline Stages**:

```
UDP Socket          PacketAssembler      PacketsBuffer         Worker(s)           Batcher           Demultiplexer
     │                    │                    │                  │                  │                   │
     │  Datagram          │                    │                  │                  │                   │
     ├───────────────────►│                    │                  │                  │                   │
     │                     │  Packet (bytes)    │                  │                  │                   │
     │                     ├───────────────────►│                  │                  │                   │
     │                     │                    │  Packets slice   │                  │                   │
     │                     │                    ├─────────────────►│                  │                   │
     │                     │                    │                  │                  │                   │
     │                     │                    │                  │  MetricSamples    │                   │
     │                     │                    │                  ├─────────────────►│                   │
     │                     │                    │                  │                  │  Batch (32)       │
     │                     │                    │                  │                  ├──────────────────►│
     │                     │                    │                  │                  │                   │
```

**Component Details**:

| Component | Input | Output | Key Optimizations |
|-----------|-------|--------|-------------------|
| **PacketAssembler** | UDP datagrams | Packets (multiple metrics) | PacketPool for buffer reuse; not used for UDS (origin tags prevent packing)  |
| **PacketsBuffer** | Packets | Packets slice | Buffers up to 32 packets OR 100ms timeout; Go buffered channel for backpressure |
| **Worker** | Packets slice | MetricSamples | StringInterner for string deduplication; number based on CPU cores |
| **Batcher** | MetricSamples | Batch of 32 | Reduces Demultiplexer calls |

**Worker Count Calculation** :

| Mode | Formula |
|------|---------|
| Standard | `max(cores - 2, 2)` |
| Multiple sampling pipelines | `max(cores / 2, 2)` |

**StringInterner**: Caches finite number of strings; when full, empties and restarts caching. Configurable size via `dogstatsd_string_interner_size` .

**Batcher Sizing**:
- Default batch size: 32 MetricSamples
- Channel buffer: 100 batches
- Max memory per TimeSampler: ~844KB (with typical 264-byte samples)

### 14. TimeSampler & Aggregation Strategies

**Purpose**: Aggregate incoming metric samples by time intervals before flushing .

**Pipeline Strategies**:

| Strategy | Behavior | Use Case |
|----------|----------|----------|
| **Standard** | Single TimeSampler pipeline | Basic aggregation |
| **Max Throughput** | `(cores/2) - 1` TimeSamplers | High-volume environments |
| **Per Origin** | One pipeline per data origin | Shared environments, better compression |

**Memory Usage Calculation** :
- Formula: `packet_buffer_size × packet_size × channel_buffer_size`
- Default: `32 × 8192 × 1024 = 256MB`
- Add per-listener buffers for UDS/UDP

**NoAggregationStreamWorker**: Special pipeline that bypasses aggregation entirely, batching directly to intake. Enabled via `dogstatsd_no_aggregation_pipeline` .

### 15. Adaptive vs. Resource-Based Sampling

**Purpose**: Control trace ingestion volume to stay within budget while maintaining visibility .

**Comparison**:

| Aspect | Resource-Based Sampling | Adaptive Sampling |
|--------|------------------------|-------------------|
| **Decision basis** | Per endpoint (resource) configuration | Dynamic based on monthly ingestion target |
| **Configuration** | User-defined rates per resource | Set monthly target, system adjusts |
| **Response to surges** | Manual adjustment required | Automatic reduction of lower-priority traces |
| **Minimum visibility** | None | At least one trace per (service, resource, env) every 5 minutes |

**Resource-Based Sampling Example** :
```
POST /cart/checkout → 100% sampling (critical)
Remaining resources → 10% sampling (automatic from Agent)
```

**Adaptive Sampling Target**: Set monthly ingestion volume (e.g., 120TB). System automatically adjusts to meet target while preserving visibility for low-traffic services .

---

## Distributed Tracing & Sampling

### 16. Head-Based Sampling Strategy

**Purpose**: Make sampling decision at trace root, propagate to all spans to ensure trace completeness .

**Why Head-Based**:

| Benefit | Description |
|---------|-------------|
| **Cost efficiency** | Reduces egress costs; only sampled traces transmitted |
| **Trace completeness** | All spans from selected trace are collected |
| **Ease of deployment** | Single Agent, no additional infrastructure |

**How It Works**:
1. Decision made at root span (beginning of request)
2. Decision propagated downstream via trace context (HTTP headers)
3. All spans follow same decision—entire trace kept or dropped

**Trace Metrics**: Calculated from 100% of traffic (unsampled), providing accurate data for dashboards, monitors, and SLOs .

### 17. Tail-Based Sampling Challenges

**Purpose**: Make sampling decision after trace completion .

**Challenges**:
- Requires buffering all spans until trace assumed complete
- Additional infrastructure for span consolidation
- Higher operational complexity
- Risk of incomplete traces

**Advantage**: Can capture errors and high-latency traces after seeing full trace.

### 18. Error Trace Protection

**Purpose**: Ensure critical error traces are ingested even under aggressive sampling .

**Mechanism**:
- Additional sampling built into Datadog Agent
- Captures sampling of critical error traces even when head-based sampling would drop them
- Ingestion reason tracked via `ingestion_reason` field

**Ingestion Reason Values**:
| Value | Meaning |
|-------|---------|
| `auto` | Default Agent sampling (10 traces/second) |
| `resource` | Resource-based sampling rule applied |
| `error` | Error trace protection triggered |
| `manual` | User-specified rate |

### 19. Intelligent Retention Filtering

**Purpose**: Automatically retain diverse trace samples for 15 days without user configuration .

**Mechanisms**:
- **Diversity sampling**: Ensures variety across services, endpoints, statuses
- **Flat sampling (1%)** : Baseline retention for all traces

**Custom Retention**: Tag-based filters for specific traces (e.g., `env:prod` retention for 30 days).

---

## Database Monitoring & Index Optimization

### 20. Suboptimal Index Scan Detection

**Purpose**: Automatically identify inefficient index usage despite correct index selection .

**The Problem**: B-tree indexes are ordered left-to-right by columns. Mismatch between index column order and query predicates forces range scans instead of seeks.

**Example** :
```
Index: (entity, dbms, org_id, type)
Query WHERE: org_id = X AND dbms = Y
```
- Leading column `entity` not filtered → PostgreSQL cannot seek directly
- Must scan larger index range, apply filters as it goes

**Detection Metrics**:

| Metric | High-Value Query | Problem Query |
|--------|-----------------|---------------|
| Node cost | Low (e.g., 100) | High (e.g., 317,000) |
| Rows returned | Proportional to cost | Small (25) vs. high cost mismatch |
| Latency | <10ms | 300ms |

**Improvement**:
```sql
-- Targeted index matching query predicates
CREATE INDEX idx_target ON recommendations (org_id, dbms);
```
Result: Latency reduced from 300ms to **38μs** (>99% reduction) .

**DBM Recommendations**: Automatically detect suboptimal index scans and suggest targeted indexes .

### 21. EXPLAIN ANALYZE Integration

**Purpose**: Automatically capture actual query execution plans for slow queries .

**Key Features**:
- Auto-collection via PostgreSQL `auto_explain` extension
- Correlation with APM traces
- Interactive plan visualization

**Diagnostic Value**:

| Observation | Indication | Action |
|-------------|------------|--------|
| Actual >> estimated rows | Stale table statistics | Run `ANALYZE` |
| High Disk Read, Low Shared Hit | Working set > RAM | Add more RAM |
| Low Disk Read, High Shared Hit | Cache sufficient, query suboptimal | Optimize query plan |

---

## Log Ingestion (BYOC) Architecture

### 22. Bring Your Own Cloud (BYOC) Design

**Purpose**: Run logs indexing and search within customer infrastructure while querying from Datadog UI .

**Component Architecture**:

```
┌─────────────────────────────────────────────────────────────────┐
│                    Customer Infrastructure (EKS)                 │
├─────────────────────────────────────────────────────────────────┤
│                                                                   │
│  Applications ──► Datadog Agent ──► Indexers                     │
│                                           │                       │
│                                           ▼                       │
│                                    Object Storage (S3/GCS)       │
│                                      (splits/index files)        │
│                                           │                       │
│  Datadog UI ◄── secure connection ◄── Searchers ◄─── Metastore   │
│                                           │        (PostgreSQL)   │
│                                           │                       │
│                                    Control Plane                 │
│                                    Janitor (GC, retention)       │
│                                                                   │
└─────────────────────────────────────────────────────────────────┘
```

**Key Components** :

| Component | Responsibility |
|-----------|----------------|
| **Indexers** | Receive logs, process, index, store as splits in object storage |
| **Searchers** | Handle search queries, read Metastore, fetch from object storage |
| **Metastore** (PostgreSQL) | Metadata about indexes, split locations |
| **Control Plane** | Schedule indexing jobs on indexers |
| **Janitor** | Retention policies, garbage collection, delete jobs |

**Data Flow**:
- **Ingestion**: Logs → Agent → Indexers → Object storage (no log data leaves customer environment)
- **Query**: UI query → Datadog backend → secure connection → Searchers → Metastore lookup → Object storage fetch

---

## Complete System Interaction Diagram

```
┌─────────────────────────────────────────────────────────────────────────────────────┐
│                              CUSTOMER APPLICATION                                    │
│                                                                                      │
│  ┌──────────────┐  ┌──────────────┐  ┌──────────────┐  ┌──────────────┐            │
│  │  Service A   │  │  Service B   │  │  Database    │  │  Load        │            │
│  │  (traces)    │  │  (metrics)   │  │  (logs)      │  │  Balancer    │            │
│  └──────┬───────┘  └──────┬───────┘  └──────┬───────┘  └──────┬───────┘            │
│         │                 │                 │                 │                     │
└─────────┼─────────────────┼─────────────────┼─────────────────┼─────────────────────┘
          │                 │                 │                 │
          ▼                 ▼                 ▼                 ▼
┌─────────────────────────────────────────────────────────────────────────────────────┐
│                              DATADOG AGENT                                          │
│                                                                                      │
│  ┌─────────────────────────────────────────────────────────────────────────────┐    │
│  │                           DogStatsD Pipeline                                 │    │
│  │                                                                               │    │
│  │  UDP ──► PacketAssembler ──► PacketsBuffer ──► Worker(s) ──► Batcher        │    │
│  │              (pooled)           (32/100ms)      (cores-2)       (32)          │    │
│  │                                                                               │    │
│  │  StringInterner (dedup)  │  TimeSampler(s)  │  Forwarder                     │    │
│  └─────────────────────────────────────────────────────────────────────────────┘    │
│                                                                                      │
│  ┌─────────────────────────────────────────────────────────────────────────────┐    │
│  │                           Trace Sampling                                     │    │
│  │                                                                               │    │
│  │  Head-based decision at root → propagate via headers → sample/drop           │    │
│  │                                                                               │    │
│  │  Adaptive Sampling: Target monthly volume (e.g., 120TB)                     │    │
│  │  Resource-Based: per-endpoint rates (e.g., /checkout: 100%)                 │    │
│  │  Error Protection: Always sample errors                                     │    │
│  └─────────────────────────────────────────────────────────────────────────────┘    │
│                                                                                      │
└─────────────────────────────────────────────────────────────────────────────────────┘
          │
          │ (HTTPS/WSS)
          ▼
┌─────────────────────────────────────────────────────────────────────────────────────┐
│                              DATADOG INGESTION                                       │
│                                                                                      │
│  ┌─────────────────────────────────────────────────────────────────────────────┐    │
│  │                           Kafka (Streaming)                                  │    │
│  │                                                                               │    │
│  │  Partition 0  │  Partition 1  │  Partition 2  │  Partition N                │    │
│  └─────────────────────────────────────────────────────────────────────────────┘    │
│                                                                                      │
│  Intake Workers (per partition) → Route by consistent hash                          │
│                                                                                      │
└─────────────────────────────────────────────────────────────────────────────────────┘
          │
          ▼
┌─────────────────────────────────────────────────────────────────────────────────────┐
│                              RTDB (Real-Time Database)                               │
│                                                                                      │
│  ┌─────────────────────────────────────────────────────────────────────────────┐    │
│  │                      Shard-per-Core Architecture                             │    │
│  │                                                                               │    │
│  │  Core 0: Shard 0          Core 1: Shard 1          Core N: Shard N           │    │
│  │  ┌─────────────────┐      ┌─────────────────┐      ┌─────────────────┐       │    │
│  │  │ LSM Tree        │      │ LSM Tree        │      │ LSM Tree        │       │    │
│  │  │ Memtable        │      │ Memtable        │      │ Memtable        │       │    │
│  │  │ SSTables (disk) │      │ SSTables (disk) │      │ SSTables (disk) │       │    │
│  │  └─────────────────┘      └─────────────────┘      └─────────────────┘       │    │
│  │                                                                               │    │
│  │  Per-shard cache, no cross-shard synchronization, channel communication      │    │
│  └─────────────────────────────────────────────────────────────────────────────┘    │
│                                                                                      │
│  Throttling: Permit-based (high-priority) + Scheduling-based (background)          │
│                                                                                      │
└─────────────────────────────────────────────────────────────────────────────────────┘
          │
          ▼
┌─────────────────────────────────────────────────────────────────────────────────────┐
│                              INDEX DATABASE                                          │
│                                                                                      │
│  ┌─────────────────────────────────────────────────────────────────────────────┐    │
│  │  Timeseries metadata:                                                        │    │
│  │  - metric name → timeseries_id                                             │    │
│  │  - tag key-value pairs → timeseries mapping                                │    │
│  │  - Query routing to appropriate RTDB nodes                                 │    │
│  └─────────────────────────────────────────────────────────────────────────────┘    │
│                                                                                      │
└─────────────────────────────────────────────────────────────────────────────────────┘
          │
          ▼
┌─────────────────────────────────────────────────────────────────────────────────────┐
│                              HUSKY EVENT STORE (Logs + Traces)                       │
│                                                                                      │
│  ┌─────────────────────────────────────────────────────────────────────────────┐    │
│  │  Ingestion: → Aggregate → Fragments → Object Storage (S3/GCS/Azure)        │    │
│  │                                                                               │    │
│  │  Compaction: Streaming merge (CWW - Compact While Write)                    │    │
│  │                                                                               │    │
│  │  Query: Metadata lookup → Fragment discovery → Parallel scan → Merge        │    │
│  └─────────────────────────────────────────────────────────────────────────────┘    │
│                                                                                      │
└─────────────────────────────────────────────────────────────────────────────────────┘
          │
          ▼
┌─────────────────────────────────────────────────────────────────────────────────────┐
│                              BYOC LOGS (Customer Infrastructure)                     │
│                                                                                      │
│  ┌─────────────────────────────────────────────────────────────────────────────┐    │
│  │  Customer EKS Cluster:                                                       │    │
│  │  - Indexers (receive → process → split files)                               │    │
│  │  - Object Storage (splits stored locally)                                   │    │
│  │  - Metastore (PostgreSQL)                                                   │    │
│  │                                                                               │    │
│  │  No log data leaves customer environment during ingestion                   │    │
│  │                                                                               │    │
│  │  Secure query path: UI → Datadog backend → Searchers → Object Storage      │    │
│  └─────────────────────────────────────────────────────────────────────────────┘    │
│                                                                                      │
└─────────────────────────────────────────────────────────────────────────────────────┘
```

---

## Algorithm & Pattern Summary Table

| # | Algorithm/Pattern | Primary Purpose | Datadog Component |
|---|------------------|-----------------|-------------------|
| 1 | LSM Tree (6th Gen) | Write-optimized timeseries storage | RTDB storage engine |
| 2 | Shard-per-Core Architecture | Eliminate contention, maximize parallelism | RTDB, entire platform |
| 3 | Consistent Hashing | Distribute data across CPU cores | Shard assignment |
| 4 | Raft Consensus | Strongly consistent index database | Index Database (not detailed) |
| 5 | Head-Based Sampling | Trace completeness with cost control | Distributed Tracing |
| 6 | Adaptive Sampling | Dynamic budget-based sampling | Trace ingestion control |
| 7 | Resource-Based Sampling | Per-endpoint sampling rates | Trace configuration |
| 8 | DDSketch | Percentile estimation with bounded error | Distribution metrics |
| 9 | Compact-While-Write (CWW) | Efficient streaming compaction | Husky event store |
| 10 | Packet Pooling | Reuse byte buffers to reduce allocations | DogStatsD UDP intake |
| 11 | String Interning | Deduplicate strings in memory | DogStatsD Worker |
| 12 | Batching (PacketsBuffer/Batcher) | Reduce per-event overhead | DogStatsD pipeline |
| 13 | Permit-based Throttling | Rate limiting for high-priority ops | RTDB |
| 14 | Scheduling-based Throttling | Fairness across background tasks | RTDB |
| 15 | Fragments | Immutable event files in object storage | Husky |
| 16 | Index Splits | Time-based log indexing | BYOC Logs |
| 17 | Diversity Sampling | Representative trace retention | Intelligent Retention |
| 18 | Nested Metric Queries | Multilayer space/time aggregation | Metrics query engine |
| 19 | Tag-based Filtering | Custom retention rules | All telemetry types |
| 20 | B-Tree Index Optimization | Column order matching query predicates | Database Monitoring |

---

## Configuration Reference

### DogStatsD Configuration

```yaml
# dogstatsd.yaml (Agent configuration)
dogstatsd_buffer_size: 8192                # Max packet size (bytes)
dogstatsd_packet_buffer_size: 32           # Packets to buffer before processing
dogstatsd_packet_buffer_flush_timeout: 100ms
dogstatsd_queue_size: 1024                 # Channel buffer size
dogstatsd_string_interner_size: 2048       # String cache size

# Worker configuration
dogstatsd_pipeline_count: 4                # Number of TimeSampler pipelines
dogstatsd_pipeline_autoadjust: true        # Auto-adjust based on cores
dogstatsd_pipeline_autoadjust_strategy: max_throughput  # or per_origin

# No-aggregation mode
dogstatsd_no_aggregation_pipeline: false
dogstatsd_no_aggregation_pipeline_batch_size: 2048
```

### Trace Sampling Configuration

```yaml
# Agent configuration (datadog.yaml)
apm_config:
  enabled: true
  max_traces_per_second: 10                 # Default Agent sampling rate
  
  # Sampling rules (resource-based)
  sampling_rules:
    - name: "critical-checkout"
      service: "checkout-service"
      resource: "POST /cart/checkout"
      sample_rate: 1.0
    - name: "default"
      sample_rate: 0.1
```

### Adaptive Sampling Configuration

```yaml
# Service-level configuration
apm_config:
  adaptive_sampling:
    enabled: true
    target_ingestion_volume_mb: 120000      # Monthly target in MB
    enrolled_services:
      - "adservice"
      - "checkout-service"
```

### Database Monitoring (DDM) Settings

```sql
-- Enable auto_explain for slow queries
ALTER SYSTEM SET auto_explain.log_min_duration = '1s';
ALTER SYSTEM SET auto_explain.log_analyze = true;
ALTER SYSTEM SET auto_explain.log_buffers = true;
ALTER SYSTEM SET auto_explain.log_timing = true;
ALTER SYSTEM SET auto_explain.log_triggers = true;
ALTER SYSTEM SET auto_explain.log_verbose = true;
ALTER SYSTEM SET auto_explain.log_format = 'json';
```

---

## Performance & Scale Characteristics

| Component | Scale | Notes |
|-----------|-------|-------|
| Ingestion throughput | 60x improvement (Gen 6 vs. Gen 5) | Rust-based LSM engine |
| Query performance | 5x faster at peak scale | Shard-per-core optimization |
| Query latency improvement | 99%+ (300ms → 38μs) | Targeted index optimization  |
| Trace data volume | ~5× log volume (unsampled) | Sampling reduces to ~2×  |
| DogStatsD buffer | 256MB default | Configurable via packet_size × buffer_size × queue_size  |
| BYOC log storage | Customer-controlled | S3/GCS/Azure Blob  |
| Adaptive sampling target | Monthly volume | System adjusts dynamically  |
| LSM compaction | Streaming (CWW) | No separate heavy compaction phase  |

---

## Comparison with Other Observability Platforms

| Feature | Datadog | New Relic | Splunk | Dynatrace |
|---------|---------|-----------|--------|------------|
| Unified data model | Metrics, logs, traces | Unified with NRL | Separate indexes | Unified |
| Head-based tracing | Yes | Yes | Yes (sampling) | Yes (PurePath) |
| Adaptive sampling | Yes (budget-based) | No | No | No |
| Timeseries engine | 6th gen (Rust LSM) | NRDB (proprietary) | TSIDX | Davis AI |
| DDSketch (percentiles) | Yes | No | No | Yes |
| BYOC logs | Yes (indexing/search) | No | Yes (Splunk Cloud) | No |
| Database monitoring | Deep (incl. EXPLAIN ANALYZE) | Limited | Limited | Limited |
| AI-powered detection | Watchdog | Applied Intelligence | Splunk ML | Davis AI |

---

## Source Code & Documentation Reference

| Component | Location |
|-----------|----------|
| DogStatsD internals | `datadog-agent/docs/public/architecture/dogstatsd/internals.md`  |
| Husky event store | Datadog Engineering Blog  |
| RTDB 6th gen | Datadog Engineering Blog  |
| Distributed tracing | Datadog Architecture documentation  |
| BYOC Logs | Datadog Cloud Prem documentation  |
| DBM EXPLAIN ANALYZE | Datadog DBM docs  |
| Index optimization | Datadog Blog  |

---

## Conclusion

Datadog's design philosophy emphasizes:

- **Unified observability**: Single platform for metrics, logs, traces, and security
- **Performance at scale**: Purpose-built storage engines evolving over 15 years
- **Cost-aware sampling**: Adaptive and resource-based strategies to control ingestion
- **Customer data sovereignty**: BYOC options for logs and other sensitive data
- **Developer productivity**: Automatic detection of performance issues (e.g., suboptimal indexes)

Key innovations and algorithms include:

- **6th generation LSM-Tree engine in Rust**: 60x ingestion improvement, 5x faster queries 
- **Shard-per-core architecture**: Eliminates cross-thread synchronization, ensures even load distribution
- **Head-based sampling with trace completeness**: Cost-efficient while preserving full traces 
- **Adaptive sampling**: Dynamic budget-based adjustments without manual tuning 
- **Husky event store with Compact-While-Write**: Streaming compaction for logs and traces 
- **DDSketch for distribution metrics**: Accurate percentile estimation
- **DogStatsD pipeline optimizations**: Packet pooling, string interning, batched processing 
- **Database Monitoring with auto-index detection**: Suboptimal index scan identification with targeted index recommendations 

This combination of algorithms and patterns makes Datadog suitable for:
- **Cloud-native microservices**: End-to-end tracing and metric correlation
- **High-cardinality metrics**: Billions of unique timeseries
- **Real-time alerting**: Millisecond-latency anomaly detection
- **Security and compliance**: Audit logging, data sovereignty (BYOC)
- **Database performance optimization**: Automatic EXPLAIN ANALYZE and index recommendations
- **Multi-cloud and hybrid environments**: Unified observability across AWS, Azure, GCP, on-prem

---

*Document Version: 1.0*
*Based on Datadog engineering blogs, official documentation, and GitHub repositories*