
# 📘 Use Case: Distributed Access Control Caching with Event-Driven Invalidation

## 1. Overview

In a large-scale product ecosystem, a centralized **Access Control Service (ACS)** is responsible for evaluating:

* User roles
* Resource permissions
* Action-level authorization (READ / WRITE / DELETE)

This service acts as a **global policy decision engine** used by multiple downstream product services.

However, due to increasing traffic, ACS experiences **high load and rate limiting (HTTP 429 errors)**, which impacts multiple dependent services and leads to degraded customer experience across the ecosystem.

---

## 2. Problem Statement

### 2.1 Observed Issue

The following issues are observed in production:

* ACS is overloaded due to repeated authorization requests
* Frequent **HTTP 429 (rate limiting)** responses from ACS
* Multiple product services are impacted simultaneously
* Customer-facing workflows fail due to authorization delays
* Same authorization checks are repeatedly executed for identical requests

---

### 2.2 Root Cause

1. **Centralized authorization dependency**

   * Every product service calls ACS synchronously

2. **Repeated identical checks**

   * Same `(userId, resourceId, action)` queries are repeatedly evaluated

3. **Low policy change frequency**

   * Policies rarely change, making repeated calls unnecessary

4. **No persistent caching layer**

   * No local or distributed cache exists at product service level

---

## 3. Design Goals

The solution must:

* Reduce dependency load on ACS
* Eliminate repeated authorization calls
* Prevent system-wide failures due to ACS rate limiting
* Maintain correctness of authorization decisions
* Support eventual policy updates
* Provide scalable distributed caching per product service

---

## 4. Proposed Solution

### 4.1 High-Level Approach

Introduce a **Persistent Product-Level Cache Layer** inside each consuming service.

Each product service will:

* Store authorization decisions locally after ACS validation
* Persist cache entries in a durable store (Redis / DynamoDB / Cosmos DB)
* Serve future requests from cache instead of ACS
* Invalidate or update cache asynchronously via event-driven updates from ACS

---

## 5. Architecture Overview

---

### 5.1 Read Path (Authorization Request Flow)

```text id="read_flow_v2"
User Request
   ↓
Product Service
   ↓
Check Persistent Cache
   ↓
CACHE HIT → Return Authorization Decision
   ↓
CACHE MISS → Call ACS
   ↓
ACS evaluates policy
   ↓
Store result in Persistent Cache (with version)
   ↓
Return response to user
```

---

### 5.2 Write Path (Policy Update Flow)

```text id="write_flow_v2"
Policy Updated in ACS
   ↓
ACS publishes Policy Change Event
   ↓
Event Bus (Kafka / Event Grid / PubSub)
   ↓
Product Services consume event
   ↓
Each service filters relevant events
   ↓
Update cache version (invalidate old policy)
   ↓
Schedule cleanup of old versions (30 days retention)
```

---

## 6. Cache Design

### 6.1 Cache Key Structure

```text id="cache_key_v2"
(userId, resourceId, action)
```

Example:

```text id="cache_key_ex"
user:123 | order:789 | READ
```

---

### 6.2 Cache Value Structure (Versioned)

```json id="cache_value_v2"
{
  "decision": "ALLOW",
  "policyVersion": 15,
  "evaluatedAt": "2026-01-01T10:00:00Z"
}
```

---

### 6.3 Key Design Principle

* Cache is **NOT TTL-based for validity**
* Cache is **version-controlled**
* Old policy versions are retained temporarily (30 days) for rollback/debugging

---

## 7. Storage Strategy

Each product service selects storage based on cost and scale:

| Storage                   | Purpose                         |
| ------------------------- | ------------------------------- |
| Redis Labs                | Low latency in-memory cache     |
| Amazon DynamoDB           | Persistent scalable cache       |
| Microsoft Azure Cosmos DB | Multi-region global persistence |

---

## 8. Cache Invalidation Strategy

### 8.1 Event-Driven Model

When policy changes in ACS:

* ACS emits a **Policy Change Event**
* Event includes:

  * User information
  * Affected resources
  * Affected actions
  * New policy version

---

### 8.2 Event Flow

```text id="event_flow_v2"
ACS Policy Update
   ↓
Event Published (Kafka / Event Grid)
   ↓
All Product Services Consume Event
   ↓
Filter Relevant Events (by product/service domain)
   ↓
Update Cache Version
   ↓
Mark old version as stale
   ↓
Schedule deletion after 30 days
```

---

### 8.3 Version-Based Invalidation Strategy

Instead of deleting cache immediately:

* New policy → increment `policyVersion`
* Cache updated with new version
* Old version retained for 30 days
* Background cleanup job removes stale entries

---

## 9. Consistency Model

| Component      | Consistency                          |
| -------------- | ------------------------------------ |
| ACS            | Strong consistency (source of truth) |
| Cache Layer    | Eventual consistency                 |
| Policy Updates | Asynchronous propagation             |
| Cache Cleanup  | Delayed (30 days retention)          |

---

## 10. Benefits

### 10.1 Performance Improvements

* Eliminates repeated ACS calls for same decision
* Reduces ACS load by up to **90%+**
* Avoids system-wide rate limiting failures

---

### 10.2 Reliability Improvements

* Removes ACS as a runtime bottleneck
* Prevents cascading failures across services
* Ensures service continuity during ACS throttling

---

### 10.3 Scalability

* Each service independently manages its cache
* No centralized caching dependency
* Horizontally scalable architecture

---

### 10.4 Cost Optimization

* Reduces ACS compute overhead
* Allows cost-optimized storage selection per service

---

## 11. Trade-offs

| Trade-off            | Explanation                                     |
| -------------------- | ----------------------------------------------- |
| Eventual Consistency | Policy updates are not instantaneous everywhere |
| Increased Complexity | Each service manages cache lifecycle            |
| Storage Overhead     | Persistent cache maintained per service         |
| Event Dependency     | Requires reliable event delivery system         |

---

## 12. Failure Scenarios & Mitigations

### 12.1 Event Loss

Mitigation:

* Periodic reconciliation job from ACS snapshot
* Replay mechanism for missed events

---

### 12.2 Stale Cache Usage

Mitigation:

* Policy version validation on every request
* Automatic fallback to ACS if version mismatch

---

### 12.3 ACS Downtime

System behavior:

* Cache HIT → continues serving requests normally
* Cache MISS → fallback failure or degraded mode
* System remains operational for cached requests

---

## 13. Final Architecture

```text id="final_arch_v2"
                +----------------------+
                |         ACS          |
                | Policy Decision Layer |
                +----------+-----------+
                           |
                    Policy Events
                           ↓
              +------------------------+
              |   Event Streaming      |
              | Kafka / Event Grid     |
              +----------+-------------+
                         |
        -----------------------------------------
        |                  |                   |
+---------------+ +---------------+ +---------------+
| Product A     | | Product B     | | Product C     |
| Cache Layer   | | Cache Layer   | | Cache Layer   |
| (Persistent)  | | (Persistent)  | | (Persistent)  |
+---------------+ +---------------+ +---------------+
        |
 Local Authorization Decision (Fast Path)
```

---

## 14. Key Takeaway

This architecture transforms ACS from a **real-time synchronous dependency system** into a **policy authority system**, while shifting runtime authorization decisions to a **distributed, versioned, event-driven cache layer at the edge (product services)**.

### Result:

* ACS load significantly reduced
* System resilience improved
* Latency optimized
* Customer impact from rate limiting eliminated


# 🧠 Interview Follow-up Questions & Answers

---

## Q1. Why not just scale the Access Control Service (ACS) instead of adding caching?

**Answer:**
Scaling ACS only addresses compute capacity, not the root issue:

* Same authorization checks are repeatedly executed across services
* Traffic grows linearly with number of consumers
* Rate limiting is often external (DB / downstream policy store bottleneck)

Caching removes redundant evaluation entirely, reducing load by ~90%+ rather than just distributing it.

---

## Q2. How do you ensure cache correctness if policy updates are delayed?

**Answer:**
We use a **multi-layer correctness model**:

1. Policy version is attached to every cache entry
2. Each request validates version before use
3. If mismatch is detected:

   * fallback to ACS
   * refresh cache entry

Additionally:

* Event-driven updates propagate changes
* Reconciliation job ensures eventual convergence

---

## Q3. What happens if event delivery is duplicated or out of order?

**Answer:**
We handle this using **idempotency + versioning**:

* Each event has a `policyVersion`
* Cache only updates if incoming version > stored version
* Duplicate or older events are ignored safely

This ensures correct ordering even in distributed systems.

---

## Q4. Why is versioning preferred over TTL for cache invalidation?

**Answer:**
TTL is time-based, but access control is **event-based**.

Problems with TTL:

* Either expires too early → unnecessary ACS calls
* Or too late → stale authorization risk

Versioning ensures:

* deterministic correctness
* immediate invalidation when policy changes
* no unnecessary refresh cycles

---

## Q5. What happens if ACS publishes incorrect policy data?

**Answer:**
Since ACS is the **source of truth**, incorrect data is rare but critical.

Mitigation:

* reconciliation from authoritative policy store
* audit logs for policy changes
* ability to rollback to previous `policyVersion`

In extreme cases, system can revert cache to last known good version.

---

## Q6. How do you handle cache stampede when many requests miss simultaneously?

**Answer:**
We prevent stampede using:

* request coalescing (single flight pattern)
* distributed locking (per `(userId, resourceId, action)`)
* probabilistic early refresh
* pre-warming for hot keys

This ensures ACS is not overwhelmed during cold starts.

---

## Q7. Why store cache at product-service level instead of a shared cache layer?

**Answer:**
A shared cache introduces:

* network latency
* central bottleneck
* cross-service contention

Local cache per service ensures:

* isolation
* horizontal scalability
* failure containment
* reduced dependency coupling

---

## Q8. What happens during ACS downtime?

**Answer:**
System behaves in degraded mode:

* Cache HIT → fully functional
* Cache MISS → fallback to:

  * stale cache (if allowed)
  * or deny-by-default / limited access mode

Since most requests are cached, system remains operational.

---

## Q9. How do you ensure cache does not grow indefinitely?

**Answer:**
We control growth using:

* retention policy (e.g., 30 days for stale versions)
* LRU eviction for inactive users
* selective caching (active users only)
* periodic cleanup jobs

---

## Q10. How do you audit authorization decisions if most are served from cache?

**Answer:**
We maintain:

* cache write logs (decision + version + timestamp)
* event logs from ACS
* periodic reconciliation reports

This allows reconstruction of decision history without querying ACS every time.

---

## Q11. What is the biggest risk of this design?

**Answer:**
The biggest risk is **temporary stale authorization data**.

We mitigate it using:

* version validation on every request
* event-driven updates
* reconciliation fallback
* ACS fallback on mismatch

Trade-off is intentional: availability + performance over strict real-time consistency.

---

## Q12. Why not query ACS only once per user session instead of caching per request?

**Answer:**
Because:

* authorization is resource + action specific
* user may access thousands of resources per session
* session-level caching would still require repeated checks internally

Per `(userId, resourceId, action)` caching is necessary for fine-grained control.

---

## Q13. How does your system behave under extremely high traffic spikes?

**Answer:**
Under spikes:

* cache HIT ratio increases (steady-state protection)
* ACS is protected via reduced miss traffic
* batch fallback or request coalescing handles bursts
* system gracefully degrades instead of failing

---

## Q14. Can a malicious actor exploit cache to gain unauthorized access?

**Answer:**
Risk exists if cache becomes stale or poisoned.

Mitigation:

* strict version enforcement
* signed event validation
* fallback to ACS on sensitive operations
* audit logs for anomaly detection

---

## Q15. Why is event-driven invalidation necessary instead of periodic refresh?

**Answer:**
Event-driven model provides:

* immediate propagation of policy changes
* lower load (only changes propagate)
* reduced unnecessary refresh cycles

Periodic refresh alone would:

* increase load significantly
* still risk temporary inconsistency windows

---

## Q16. What happens if two services disagree on policy state?

**Answer:**
This is expected in eventual consistency systems.

Resolution:

* ACS remains source of truth
* reconciliation aligns both services
* version-based override resolves conflicts automatically

---

## Q17. How do you handle multi-region consistency?

**Answer:**
We use:

* region-aware event replication
* eventual consistency across regions
* region-local caches
* periodic cross-region reconciliation

Global strong consistency is avoided due to latency cost.

---

## Q18. Why is this better than embedding authorization logic in each service?

**Answer:**
Embedding logic leads to:

* duplication of policy logic
* inconsistent enforcement
* harder maintenance

Centralized ACS + cache ensures:

* single source of truth
* consistent policy evaluation
* scalable enforcement layer
