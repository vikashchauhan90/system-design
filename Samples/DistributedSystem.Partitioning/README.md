# Partitioning

A collection of partitioning algorithms used by distributed databases,
streaming systems, caches, and message brokers.

## Purpose

Partitioning determines which logical partition receives a key.

Key
 ↓
Partition

```
Given a key,
which partition should receive it?
```
Examples:

```
customer-123
        ↓
Partition 7
```

## Included Algorithms

- Round Robin
- Hash Partitioning
- Range Partitioning
- Consistent Hashing
- Rendezvous Hashing

## Real World Systems

| System | Strategy |
|----------|------------|
| Kafka | Round Robin / Hash |
| Cassandra | Consistent Hashing |
| Dynamo | Consistent Hashing |
| Bigtable | Range Partitioning |
| HBase | Range Partitioning |
| Envoy | Rendezvous Hashing |


# Round Robin Partitioning

Distributes records evenly across partitions.

msg1 -> P0
msg2 -> P1
msg3 -> P2
msg4 -> P0

## Advantages

- Perfect distribution
- Simple implementation

## Disadvantages

- No key affinity
- No ordering guarantees


# Hash Partitioning

Maps a key to a partition using:

partition = hash(key) % N

## Real Systems

- Kafka
- Pulsar
- MongoDB Hashed Shards


# Range Partitioning

Stores related values together.

0-999      -> P1
1000-1999  -> P2
2000-2999  -> P3

## Real Systems

- Bigtable
- HBase
- CockroachDB


# Consistent Hashing

Maps keys to nodes using a hash ring.

Only a small percentage of keys move
when nodes join or leave.

## Real Systems

- Cassandra
- Dynamo
- Riak


# Rendezvous Hashing

Assigns keys to nodes using highest score wins.

score(node,key)

Pick node with highest score.

## Advantages

- Simpler than consistent hashing
- Excellent balancing
- Minimal data movement
