# Kafka Internal Architecture Guide

## Raft, Controller, Storage, Coordination and Message Flow

---

# Overview

Apache Kafka is a distributed event streaming platform designed around:

* Distributed commit logs
* Partitioned data storage
* High throughput append-only writes
* Replication and fault tolerance
* Metadata management through Raft consensus
* Efficient disk-based storage with index lookup

This document explains the core implementation concepts found in the following Kafka modules:

```text
raft/
metadata/
controller/
storage/
server/
group-coordinator/
transaction-coordinator/
```

and how they work together.

---

# High-Level Architecture

```text
                +----------------------+
                |     Kafka Client     |
                +----------+-----------+
                           |
                           v
                +----------------------+
                |       Broker         |
                +----------+-----------+
                           |
           +---------------+----------------+
           |                                |
           v                                v

   +---------------+              +----------------+
   | Log Storage   |              |  Controller    |
   | (Partitions)  |              |   (KRaft)      |
   +---------------+              +----------------+
           |                                |
           +---------------+----------------+
                           |
                           v
                 +------------------+
                 |  Raft Quorum     |
                 | Leader/Follower  |
                 +------------------+
```

---

# KRaft Architecture

## What is KRaft?

KRaft (Kafka Raft) is Kafka's internal consensus protocol that replaces ZooKeeper.

Source code:

```text
raft/
metadata/
controller/
```

KRaft is responsible for:

* Controller election
* Metadata replication
* Metadata durability
* Cluster state management

Examples of metadata:

* Topics
* Partitions
* ISR
* Broker registrations
* ACLs
* Configurations

---

# Raft Components

## Leader

Only one node acts as leader.

Responsibilities:

* Accept metadata updates
* Append records
* Replicate to followers
* Advance high watermark

Files:

```text
raft/src/main/java/org/apache/kafka/raft/
```

Important classes:

```text
KafkaRaftClient
LeaderState
QuorumState
```

---

## Followers

Followers:

* Receive replicated entries
* Persist entries locally
* Acknowledge replication
* Participate in elections

Important classes:

```text
FollowerState
ReplicaState
```

---

## Candidate

A follower becomes candidate when:

```text
Election Timeout Expired
```

Candidate:

1. Increments epoch
2. Requests votes
3. Becomes leader if majority achieved

Files:

```text
CandidateState
VoteRequestData
VoteResponseData
```

---

# Raft Log Replication

Process:

```text
Leader
   |
   | Append Record
   v

Leader Log
   |
   | Replicate
   v

Follower Logs
```

Example:

```text
Offset 100
Offset 101
Offset 102
```

Leader writes locally first.

Then sends:

```text
FetchRequest
```

Followers append the same records.

After quorum acknowledgment:

```text
HighWatermark advances
```

Records become committed.

---

# Metadata Log

KRaft stores metadata as a replicated log.

Directory:

```text
metadata/
```

Examples:

```text
CreateTopicRecord
BrokerRegistrationRecord
PartitionRecord
ConfigRecord
```

Everything is represented as log records.

State is rebuilt by replaying the metadata log.

---

# Controller Architecture

Directory:

```text
controller/
```

Main class:

```text
QuorumController
```

Responsibilities:

* Topic creation
* Topic deletion
* Partition assignment
* Leader election
* Broker registration
* Metadata updates

The controller itself is backed by KRaft.

---

# Topic Architecture

A topic is a logical stream.

Example:

```text
orders
payments
notifications
```

Topics are divided into partitions.

---

# Partition Architecture

```text
Topic
 |
 +-- Partition-0
 |
 +-- Partition-1
 |
 +-- Partition-2
```

A partition is:

```text
Ordered Append Only Log
```

Files:

```text
storage/
core/
```

Classes:

```text
UnifiedLog
LocalLog
LogSegment
```

---

# Message Ordering

Ordering is guaranteed within a partition.

Example:

```text
Offset 0
Offset 1
Offset 2
Offset 3
```

Kafka never changes order inside a partition.

---

# Key-Based Partitioning

When a key is provided:

```java
partition = hash(key) % partitionCount
```

Example:

```text
Customer-100
Customer-100
Customer-100
```

All records go to the same partition.

Benefits:

* Ordering preserved
* Session affinity
* Entity grouping

Producer implementation:

```text
clients/
producer/
```

Classes:

```text
DefaultPartitioner
BuiltInPartitioner
```

---

# No-Key Partitioning

Without a key:

Kafka distributes records across partitions.

Modern Kafka uses sticky partitioning behavior rather than pure round-robin for batching efficiency.

Conceptually:

```text
P0
P1
P2
P3
```

Messages are balanced across partitions.

Benefits:

* Better load distribution
* Higher throughput

---

# Storage Engine

Directory:

```text
storage/
```

Kafka storage is:

```text
Append Only
```

No in-place update.

No random modification.

Records are appended sequentially.

Benefits:

* Sequential disk writes
* OS page cache optimization
* High throughput

---

# Log Segment Architecture

A partition consists of many segments.

Example:

```text
Partition-0

00000000000000000000.log
00000000000000000000.index
00000000000000000000.timeindex

00000000001000000000.log
00000000001000000000.index
00000000001000000000.timeindex
```

Classes:

```text
LogSegment
LazyIndex
OffsetIndex
TimeIndex
```

---

# Segment Rolling

Kafka rolls segments based on:

## Size

```text
log.segment.bytes
```

Example:

```text
1 GB
```

When reached:

```text
new segment created
```

---

## Time

```text
log.roll.ms
log.roll.hours
```

When expired:

```text
new segment created
```

---

# Binary Log File

File:

```text
*.log
```

Contains:

```text
Record Batches
```

Stored in binary format.

Classes:

```text
FileRecords
MemoryRecords
DefaultRecordBatch
```

Structure:

```text
Batch Header
Records
Checksum
Timestamp
Offset Delta
```

---

# Offset Management

Every record has a logical offset.

Example:

```text
0
1
2
3
4
5
```

Offsets are sequential.

Kafka tracks:

```text
Base Offset
Last Offset
Log End Offset
High Watermark
```

---

# Offset Index

File:

```text
*.index
```

Purpose:

```text
Offset -> File Position
```

Stores sparse entries.

Example:

```text
Offset      Position

1000        0
2000        4096
3000        8192
```

Search process:

```text
Offset
   |
Binary Search
   |
File Position
   |
Read Log
```

Benefits:

* Fast lookup
* Small index files

Classes:

```text
OffsetIndex
```

---

# Time Index

File:

```text
*.timeindex
```

Purpose:

```text
Timestamp -> Offset
```

Example:

```text
Timestamp      Offset

10:00          1000
10:05          2000
10:10          3000
```

Used for:

```text
offsetForTimes()
```

Classes:

```text
TimeIndex
```

---

# Sparse Index Design

Kafka does NOT index every message.

Instead:

```text
Every N bytes
```

An index entry is written.

Benefits:

* Small indexes
* Fast startup
* Efficient disk usage

---

# High Watermark

High Watermark:

```text
Highest Replicated Offset
```

Only records below HW are visible to consumers.

Example:

```text
LEO = 1000
HW  = 995
```

Consumers can only read:

```text
0 - 995
```

---

# Consumer Read Flow

```text
Consumer
    |
FetchRequest
    |
Broker
    |
Offset Index Lookup
    |
Log Segment
    |
Records Returned
```

Lookup steps:

1. Find segment
2. Search offset index
3. Seek file position
4. Read records
5. Return batches

---

# Group Coordinator

Directory:

```text
group-coordinator/
```

Responsibilities:

* Consumer groups
* Membership tracking
* Rebalancing
* Offset commits

Important classes:

```text
GroupCoordinator
GroupMetadataManager
```

---

# Consumer Group Rebalancing

Example:

```text
Consumer A
Consumer B
Consumer C
```

Partitions:

```text
P0
P1
P2
```

Coordinator assigns:

```text
A -> P0
B -> P1
C -> P2
```

If a member leaves:

```text
Rebalance occurs
```

Assignments are recalculated.

---

# Transaction Coordinator

Directory:

```text
transaction-coordinator/
```

Responsibilities:

* Exactly-once semantics
* Transaction lifecycle
* Producer fencing
* Commit coordination

Classes:

```text
TransactionCoordinator
TransactionStateManager
```

---

# End-to-End Message Flow

## Produce

```text
Producer
   |
Partition Selection
   |
Leader Partition
   |
Append Log
   |
Replicate Followers
   |
High Watermark Advance
   |
ACK
```

---

## Consume

```text
Consumer
   |
Fetch Offset
   |
Offset Index
   |
Locate Segment
   |
Read Binary Log
   |
Return Records
```

---

# Core Design Principles

Kafka achieves scalability through:

1. Append-only log storage
2. Partitioned data model
3. Sequential disk writes
4. Sparse indexing
5. Raft-based metadata quorum
6. Leader/follower replication
7. Segment rolling architecture
8. Binary record storage
9. Consumer pull model
10. Distributed coordination

Together these components allow Kafka to provide high-throughput, fault-tolerant, durable event streaming at massive scale.
