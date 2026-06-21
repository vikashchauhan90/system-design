

# 📘 Use Case: Scalable Multi-Entity Access Management for ERP with Persistent Cache & Event-Driven Synchronization

---

## 1. Overview

In a multi-tenant ERP ecosystem, users often operate across **multiple business entities** (e.g., subsidiaries, branches, organizations).

A single user may be associated with:

* 5,000 to 10,000 business entities
* Different permission scopes per entity
* Frequently changing access assignments (though relatively low frequency overall)

The ERP must support:

* Listing all entities a user has access to (onboarding experience)
* Selecting a specific entity for operations
* Fetching entity-specific details and performing actions

A centralized **Business Entity Service (BES)** provides this data, but it becomes a **scalability bottleneck due to high read traffic across multiple consuming product services**, resulting in:

* Rate limiting (HTTP 429)
* Latency spikes
* Customer churn due to degraded onboarding experience

---

## 2. Problem Statement

### 2.1 Observed Issue

* BES is heavily consumed across multiple product services
* High-frequency repeated requests for the same user-entity mappings
* Large payload responses (5K–10K entities per user)
* Rate limiting impacts critical onboarding flows
* Downstream product services fail independently due to dependency overload

---

### 2.2 Root Cause

1. **Centralized dependency on Business Entity Service**

   * All services synchronously fetch entity mappings

2. **Large dataset per user**

   * High cardinality (up to 10K entities per user)

3. **Repeated reads of same dataset**

   * Same user entity list fetched multiple times across sessions and services

4. **No persistent caching layer**

   * Every request hits BES

5. **Fan-out amplification**

   * Multiple products → same BES → rate limit threshold exceeded

---

## 3. Design Goals

The solution must:

* Reduce load on BES significantly
* Prevent rate limiting from impacting customer experience
* Efficiently handle large datasets (5K–10K entities per user)
* Support near real-time access updates
* Optimize storage cost and response size
* Enable fast entity listing + entity-level access lookup

---

## 4. Proposed Solution

### 4.1 High-Level Approach

Introduce a **Persistent Distributed Entity Access Cache Layer** at the product service level.

This cache stores:

1. **User → All Entities Mapping (compressed bulk dataset)**
2. **User + Entity → Entity Access Metadata (fine-grained access record)**

The system uses:

* Persistent cache storage (Redis / DynamoDB / Cosmos DB)
* Compression for large dataset storage
* Event-driven updates from BES
* Dual-cache model for optimization

---

## 5. Architecture Overview

---

### 5.1 Read Flow (Entity Listing & Access)

```text id="read_flow_erp_v1"
User Login / Onboarding Request
   ↓
Product Service
   ↓
Check Persistent Cache
   ↓
CACHE HIT →
      Decompress entity list (if bulk cache)
      OR fetch entity-specific cache
   ↓
Return entity list / entity context
   ↓
CACHE MISS →
      Call Business Entity Service (BES)
   ↓
Store response in cache (compressed + indexed format)
   ↓
Return response to user
```

---

### 5.2 Write Flow (Access Update Event)

```text id="write_flow_erp_v1"
Access Change in Business Entity Service
   ↓
Event Published (User + Entity + Action)
   ↓
Event Bus (Kafka / PubSub / Event Grid)
   ↓
Product Services Consume Event
   ↓
If ACCESS GRANTED:
      - Update entity list cache
      - Add user+entity cache entry

If ACCESS REVOKED:
      - Remove user+entity cache entry
      - Update compressed entity list
   ↓
Persist updated cache version
```

---

## 6. Cache Design

---

### 6.1 Cache Types (Dual Layer Strategy)

### 1. Bulk Entity Cache (Compressed)

Used for listing all entities for a user.

```text id="cache_bulk_key"
user:{userId}:entities
```

### Value (Compressed)

```json id="bulk_cache_value"
{
  "compressedData": "<gzip/base64 encoded entity list>",
  "version": 42,
  "updatedAt": "2026-01-01T10:00:00Z"
}
```

---

### 2. Entity-Level Cache (Fine-Grained Access)

Used for fast entity-specific authorization or details.

```text id="cache_entity_key"
user:{userId}:entity:{entityId}
```

### Value (Non-compressed)

```json id="entity_cache_value"
{
  "entityId": "E12345",
  "access": "ALLOWED",
  "role": "ADMIN",
  "version": 42
}
```

---

### 6.2 Design Principle

* Bulk cache → optimized for **listing**
* Entity cache → optimized for **action-level access**
* Compression used only for bulk payloads
* Fine-grained cache remains uncompressed for speed

---

## 7. Storage Strategy

| Storage   | Purpose                   |
| --------- | ------------------------- |
| Redis     | Low-latency access        |
| DynamoDB  | Persistent cache storage  |
| Cosmos DB | Multi-region availability |

---

## 8. Event-Driven Invalidation Strategy

---

### 8.1 Event Payload

```json id="event_payload_erp"
{
  "userId": "U123",
  "entityId": "E456",
  "action": "GRANTED | REVOKED",
  "updatedAt": "2026-01-01T10:00:00Z"
}
```

---

### 8.2 Invalidation Logic

#### If ACCESS GRANTED:

* Add entity to:

  * bulk cache list (compressed update)
  * entity-level cache entry

#### If ACCESS REVOKED:

* Remove from:

  * bulk cache list
  * entity-level cache entry

---

## 9. Performance Optimization Techniques

### 9.1 Compression Strategy

* Bulk entity lists compressed using gzip/snappy
* Reduces storage footprint significantly (up to 70–90%)

### 9.2 Lazy Decompression

* Decompression happens only at read time
* In-memory expansion only for active session usage

### 9.3 Partial Updates

* Only affected entities updated via events
* No full dataset reload unless reconciliation is triggered

---

## 10. Consistency Model

| Layer                   | Consistency                |
| ----------------------- | -------------------------- |
| Business Entity Service | Strong consistency         |
| Event Bus               | At-least-once delivery     |
| Cache Layer             | Eventual consistency       |
| Bulk cache updates      | Eventually consistent      |
| Entity-level cache      | Near real-time consistency |

---

## 11. Benefits

### 11.1 Performance

* Eliminates repeated BES calls
* Reduces read load by **85–95%**
* Faster onboarding experience

---

### 11.2 Scalability

* Supports 10K+ entities per user efficiently
* Avoids fan-out load on BES
* Decouples product services from BES bottleneck

---

### 11.3 Reliability

* System continues operating even if BES is throttled
* Cached data enables degraded-but-functional mode

---

### 11.4 Cost Optimization

* Reduced BES compute cost
* Efficient storage via compression
* Reduced network payload size

---

## 12. Trade-offs

| Trade-off                    | Explanation                                           |
| ---------------------------- | ----------------------------------------------------- |
| Eventual consistency         | Updates may take time to reflect across services      |
| Stale data risk              | Cache may temporarily serve outdated entity access    |
| Complexity increase          | Dual cache model + compression + event logic          |
| Debugging overhead           | Harder to trace access propagation                    |
| Storage vs compute trade-off | Compression reduces storage but increases CPU on read |
| Event dependency             | Requires reliable event delivery pipeline             |

---

## 13. Failure Scenarios & Mitigations

---

### 13.1 Event Loss

**Risk:** Access updates not reflected in cache

**Mitigation:**

* Periodic reconciliation from BES snapshot
* Version-based resync mechanism

---

### 13.2 Stale Access Data

**Risk:** Revoked entity still accessible

**Mitigation:**

* Version check on every sensitive operation
* Fallback to BES on mismatch or high-risk operations

---

### 13.3 Cache Corruption / Desync

**Risk:** Partial or inconsistent entity list

**Mitigation:**

* Full rebuild triggered via version gap detection
* Backup snapshot from BES

---

### 13.4 BES Downtime

**Risk:** Cannot fetch fresh data on cache miss

**Mitigation:**

* Serve from cache only mode
* Graceful degradation for new users (limited onboarding view)

---

## 14. Final Architecture

```text id="final_arch_erp"
                 +---------------------------+
                 | Business Entity Service   |
                 | (Source of Truth)         |
                 +------------+--------------+
                              |
                      Access Events
                              |
                 +---------------------------+
                 | Event Streaming System   |
                 | Kafka / PubSub / Grid    |
                 +------------+--------------+
                              |
      ----------------------------------------------------
      |                          |                        |
+----------------+   +----------------+     +----------------+
| Product A      |   | Product B      |     | Product C      |
| Cache Layer    |   | Cache Layer    |     | Cache Layer    |
| (Compressed +  |   | (Compressed +  |     | (Compressed +  |
|  Entity Index)  |   |  Entity Index) |     |  Entity Index) |
+----------------+   +----------------+     +----------------+
      |
 Fast Entity Listing + Entity-Level Access (Cache First)
```

---

## 15. Key Takeaway

This architecture transforms the ERP access model from:

> centralized real-time dependency on Business Entity Service

to:

> **distributed, event-driven, compressed persistent cache system optimized for large-scale entity ownership models**

---

## 16. Final Outcome

* Eliminates BES bottleneck under high load
* Enables efficient handling of 10K+ entities per user
* Prevents rate limiting from impacting customer onboarding
* Provides scalable, near real-time access updates
* Improves overall ERP responsiveness and reliability

 

 # 🧠 Interview Follow-up Questions & Answers

---

## Q1. Why do you need a distributed cache instead of a centralized cache?

**Answer:**
A centralized cache would still create a bottleneck similar to the Business Entity Service. Since multiple product services generate high read traffic, a centralized cache would:

* become a throughput bottleneck
* increase latency due to network hops
* create a single point of failure

A distributed cache per service ensures:

* horizontal scalability
* isolation between products
* reduced blast radius during failures

---

## Q2. How do you ensure cache consistency when multiple services update the same user’s entity access?

**Answer:**
We use an **event-driven model with versioning**:

* Every access change emits an event with `userId`, `entityId`, and `policyVersion`
* All services apply updates idempotently using version checks
* Only the latest version is accepted

Additionally, periodic reconciliation ensures convergence if any service misses updates.

---

## Q3. What happens if event delivery fails or is delayed?

**Answer:**
We handle this using a **multi-layer safety mechanism**:

1. At-least-once delivery ensures retry from the event bus
2. Services detect version gaps (`localVersion < BESVersion`)
3. A reconciliation job pulls delta updates from BES snapshot API
4. Cache is repaired selectively, not fully rebuilt

This ensures eventual correctness even under partial failure.

---

## Q4. Why not rely purely on TTL-based caching?

**Answer:**
TTL caching is not suitable because:

* Access control changes are event-driven, not time-based
* TTL can cause premature invalidation → unnecessary BES load
* Or delayed invalidation → stale authorization risk

Instead, **version-based invalidation provides deterministic correctness**.

---

## Q5. How do you handle 10K entities per user efficiently?

**Answer:**
We optimize using:

* **Compression (gzip/snappy)** for bulk entity lists
* **Dual cache model**:

  * bulk cache for listing
  * entity-level cache for fine-grained access
* Lazy decompression only when needed
* Delta updates instead of full dataset refresh

This reduces both storage and compute overhead.

---

## Q6. What is the risk of stale data in your system, and how do you mitigate it?

**Answer:**
Risk: revoked access may still be present in cache temporarily.

Mitigation:

* Version check on every sensitive operation
* Event-driven updates reduce staleness window
* Reconciliation job fixes drift
* Critical actions can fallback to BES validation if mismatch detected

---

## Q7. How do you prevent cache explosion given large user bases?

**Answer:**
We apply:

* selective caching (only active users)
* compression for bulk data
* TTL for inactive users
* delta-based updates instead of full reloads
* cleanup jobs for stale or unused keys

---

## Q8. What happens during Business Entity Service downtime?

**Answer:**
System operates in degraded mode:

* Cache HIT → continues normally
* Cache MISS → may fail or return limited fallback response
* No full outage since cache serves majority of traffic

This improves availability and decouples runtime dependency from BES.

---

## Q9. Why maintain both bulk cache and entity-level cache?

**Answer:**
Because they solve different problems:

* Bulk cache → optimized for onboarding and listing (large dataset)
* Entity cache → optimized for authorization checks (fast lookup)

Without separation:

* we'd either over-fetch data or over-decompress large payloads repeatedly

---

## Q10. How do you ensure idempotency in event processing?

**Answer:**
Each event carries a `policyVersion` or `eventId`.

On processing:

* If event version ≤ cached version → ignore
* If duplicate event → safely skipped
* Ensures repeated delivery does not corrupt cache state

---

## Q11. How does your design reduce load on Business Entity Service?

**Answer:**
By shifting from:

* repeated synchronous reads

to:

* cache-first reads
* event-driven updates
* batch reconciliation only when needed

This reduces BES traffic by ~85–95%.

---

## Q12. What is your fallback strategy if both cache and event system fail?

**Answer:**
We fall back in layers:

1. Cache (primary)
2. BES direct call (secondary fallback)
3. Graceful degradation mode (limited access / read-only view)

This ensures system availability even under multiple failures.

---

## Q13. What is the biggest trade-off in your design?

**Answer:**
The biggest trade-off is **eventual consistency vs real-time correctness**.

We accept slight delays in access updates in exchange for:

* massive scalability gains
* reduced BES load
* improved system stability

---

## Q14. How do you handle cache rebuild after major data corruption?

**Answer:**
We trigger a full rebuild using:

* BES snapshot API
* version-based reset
* background rehydration of cache

This is a controlled recovery process, not runtime dependency.

---

## Q15. Why is this design better than direct service-to-service calls?

**Answer:**
Direct calls cause:

* high latency
* cascading failures
* rate limiting issues
* poor scalability under fan-out traffic

Our design replaces runtime dependency with:

> asynchronous propagation + local caching + reconciliation safety net
