# 📘 Use Case: Tenant-Isolated Medical Telemetry Processing System for Infusion Pumps Using Kafka & Actor-Based Processing

---

## 1. Overview

In a multi-tenant medical software platform, each hospital (tenant) operates an isolated instance of the product ecosystem. Each tenant may also have multiple branches, each treated as a separate tenant for strict data isolation.

The system manages **infusion pumps (~10K per hospital)** that:

* Generate continuous telemetry data
* Receive configuration updates (pump settings, dosage rules, etc.)
* Emit logs and operational events
* May be reassigned across patients over time

A key requirement is:

> All telemetry, logs, and configuration events must be correctly correlated to the **current patient context**, even when device assignment changes dynamically.

---

## 2. Problem Statement

### 2.1 Observed Challenges

* High-volume telemetry ingestion from thousands of devices per tenant
* Device-to-patient mapping changes frequently (reassignment problem)
* Risk of **data misassociation across patients over time**
* Strict tenant-level isolation required (no cross-tenant leakage)
* Need to preserve ordering and consistency per patient
* Near real-time telemetry processing required

---

### 2.2 Root Cause

1. **High-frequency event streams**

   * Continuous telemetry from infusion pumps

2. **Dynamic device ownership**

   * Same device can serve different patients over time

3. **Context-sensitive correctness requirement**

   * Data must be grouped by patient, not device

4. **High scalability requirement**

   * Millions of events per day across tenants

---

## 3. Design Goals

* Ensure strict **tenant isolation**
* Maintain **correct patient-level grouping**
* Handle **high-throughput telemetry ingestion**
* Preserve ordering per patient context
* Support safe device reassignment
* Prevent data loss in streaming pipeline
* Scale horizontally across tenants and devices

---

## 4. Proposed Solution

We use:

* **Apache Kafka for event streaming**
* **Patient-based partitioning strategy**
* **Actor-based consumer processing model**
* **Stateful device-to-patient mapping service**

---

## 5. Architecture Overview

```text
Infusion Pump Devices
        ↓
Ingestion Layer (DotNetty + KEDA Autoscaling)
        ↓
Ingestion Gateway (Tenant Resolver + Validation)
        ↓
Kafka Event Stream (Partitioned by patientId)
        ↓
Actor-Based Consumers
        ↓
Patient State Database + Device Mapping Store
```

---

## 6. Core Design Components

---

### 6.1 Kafka Partitioning Strategy

Kafka topics are partitioned by:

```
patientId
```

### Why patientId?

* Ensures all events for a patient go to the same partition
* Guarantees ordering per patient
* Handles device reassignment safely via ingestion-layer mapping

---

### 6.2 Device-to-Patient Mapping Layer

A centralized state store maintains:

* Device → Patient mapping
* Valid time window for assignment
* Tenant isolation boundary

Example:

```json
{
  "deviceId": "D001",
  "tenantId": "T100",
  "patientId": "P900",
  "validFrom": "2026-01-01T10:00:00Z",
  "validTo": null
}
```

---

### 6.3 Actor-Based Consumer Model

Each Kafka partition is consumed by an **actor representing a patient group**.

Actors:

* Maintain in-memory patient state
* Process events sequentially
* Avoid concurrency issues
* Ensure deterministic processing

---

### 6.4 Event Flow

```text
Pump generates telemetry
   ↓
Ingestion Layer resolves tenant + patient mapping
   ↓
Attach patientId
   ↓
Publish to Kafka (partitioned by patientId)
   ↓
Actor consumes events
   ↓
Update state + persist to DB
```

---

## 7. High-Throughput Ingestion Layer (DotNetty + KEDA)

---

### 7.1 Purpose

To handle **massive-scale TCP telemetry ingestion**, we use:

* **DotNetty** → high-performance TCP framework
* **KEDA** → Kubernetes autoscaling based on workload

---

### 7.2 DotNetty-Based TCP Layer

Responsibilities:

* Maintain persistent TCP connections
* Handle high-throughput device telemetry
* Perform lightweight validation
* Forward normalized events downstream

---

Flow:

```text
Devices (TCP)
   ↓
DotNetty Gateway
   ↓
Validation Layer
   ↓
Ingestion Gateway
   ↓
Kafka
```

---

### 7.3 KEDA Scaling Strategy

KEDA scales ingestion pods based on:

* CPU utilization
* Memory utilization
* Active TCP connections
* Kafka lag (backpressure signal)

---

### 7.4 Benefits

* Handles 10K–100K+ TCP connections
* Auto-scales based on real traffic
* Prevents ingestion bottlenecks
* Separates connection handling from processing

---

### 7.5 Trade-offs

* Operational complexity
* Harder debugging across TCP → Kafka chain
* Resource tuning required
* Long-lived connection management overhead

---

### 7.6 Failure Handling

* Node failure → connections rebalance
* Burst traffic → backpressure via Kafka
* Scaling misconfig → mitigated via multi-metric KEDA

---

## 8. Failure Handling Strategy

---

### 8.1 Device Reassignment

* Mapping updated with timestamp
* New events use updated mapping
* Historical events preserved

---

### 8.2 Kafka Failures

* Replay from partitions
* Dead-letter queue for failures
* Checkpoint recovery

---

### 8.3 Actor Failure

* State rebuilt via Kafka replay
* Supervisor restarts actor

---

### 8.4 Data Loss Prevention

* Kafka replication factor ≥ 3
* At-least-once delivery
* Acknowledged ingestion writes

---

## 9. Consistency Model

| Component        | Consistency                 |
| ---------------- | --------------------------- |
| Kafka            | At-least-once               |
| Actor Processing | Strong per-patient ordering |
| Mapping Service  | Eventual consistency        |
| Database         | Strong consistency          |

---

## 10. Benefits

* Correct patient-level grouping
* No cross-patient data mixing
* Horizontal scalability
* Fault-tolerant processing
* Replayable event system

---

## 11. Trade-offs

* High system complexity
* Eventual consistency in mapping
* Storage overhead (Kafka + DB duplication)
* Operational monitoring overhead
* Distributed debugging complexity

---

## 12. Key Risks & Mitigations

---

### 12.1 Stale Mapping Risk

* Timestamp validation
* Replay correction

---

### 12.2 Hot Partition Problem

* Partition monitoring
* Adaptive partitioning strategy

---

### 12.3 Actor Overload

* Horizontal scaling
* Partition reassignment

---

## 13. Final Architecture Summary

This system ensures:

* Tenant isolation
* Patient-level correctness
* High scalability
* Reliable streaming ingestion
* Safe device reassignment handling

---

## 14. Key Takeaway

This architecture creates a:

> patient-centric, event-driven, highly scalable telemetry processing system with strict ordering, fault tolerance, and medical-grade correctness guarantees.

---


# 🧠 Interview Follow-up Questions & Answers

---

## Q1. Why did you choose Kafka for this system instead of a traditional database or message queue?

**Answer:**
Kafka is chosen because:

* It supports **high-throughput streaming (millions of events/sec)**
* Provides **durable event storage (prevents data loss)**
* Enables **replayability**, which is critical for medical telemetry
* Supports **partitioned ordering**, which we use for patient-level consistency

Traditional queues cannot:

* replay historical telemetry
* guarantee ordering at scale
* handle long retention efficiently

---

## Q2. Why is partitioning done by `patientId` instead of `deviceId`?

**Answer:**
Because the **business correctness requirement is patient-centric, not device-centric**.

If we partition by device:

* device reassignment breaks historical grouping
* patient-level analysis becomes inconsistent

Partitioning by `patientId` ensures:

* all telemetry for a patient is ordered
* cross-device continuity is preserved
* patient history remains intact even if device changes

---

## Q3. How do you handle device reassignment without corrupting historical data?

**Answer:**
We use a **time-bound device-to-patient mapping system**:

* Each event is tagged at ingestion time with resolved `patientId`
* Mapping includes `validFrom` and `validTo`
* Old events remain associated with original patient context
* New events follow updated mapping

This ensures:

> historical correctness is preserved, while future routing adapts dynamically

---

## Q4. What happens if the mapping service gives incorrect patient association?

**Answer:**
Mitigation strategies:

* Mapping is versioned and timestamp-based
* Events carry ingestion-time resolution snapshot
* Kafka replay can correct downstream state
* Audit logs allow post-facto correction if needed

Worst-case:

* correction events are reprocessed via Kafka replay

---

## Q5. Why did you choose the Actor model for consumers?

**Answer:**
Actor model provides:

* Single-threaded processing per patient group
* No shared-state concurrency issues
* Natural alignment with Kafka partitions
* Deterministic event ordering per patient

This simplifies:

* state management
* consistency guarantees
* debugging and recovery

---

## Q6. What happens if an actor crashes?

**Answer:**
Actors are stateless in terms of durability:

* State is persisted in database + Kafka
* On crash:

  * actor is restarted
  * state is rebuilt by replaying Kafka partition
* No data loss due to Kafka retention

This ensures:

> fault tolerance with automatic recovery

---

## Q7. How do you prevent hot partition issues if one patient generates excessive telemetry?

**Answer:**
Mitigation includes:

* monitoring partition load imbalance
* dynamic partition scaling strategies
* potential refinement:

  * patientId + deviceGroup key (if needed)
* backpressure handling in ingestion layer

---

## Q8. What is the biggest risk in using patientId as partition key?

**Answer:**
Risk: **uneven load distribution (hot patients)**

Because:

* some patients may generate significantly higher telemetry volume

Mitigation:

* partition monitoring
* adaptive rebalancing strategies
* hybrid key design if required

---

## Q9. How do you ensure no telemetry data loss?

**Answer:**
We ensure durability through:

* Kafka replication factor ≥ 3
* Acknowledged writes from ingestion gateway
* At-least-once delivery guarantees
* Replay capability for recovery
* Dead-letter queues for failed processing

---

## Q10. How do you handle out-of-order events?

**Answer:**
Out-of-order handling is managed by:

* Kafka ordering per partition (patientId ensures ordering)
* timestamp validation in actor layer
* ignoring stale events using event timestamps
* replay correction if inconsistencies are detected

---

## Q11. Why not store telemetry directly in a database instead of Kafka?

**Answer:**
Because databases:

* cannot handle high ingestion throughput efficiently
* lack native streaming + replay capabilities
* introduce write bottlenecks under load

Kafka acts as:

> durable buffer + event backbone + replay system

Database is used only for:

* final state storage
* analytics queries

---

## Q12. How do you ensure tenant isolation?

**Answer:**
We enforce isolation at multiple levels:

* tenant-aware ingestion gateway
* logical separation of topics (or headers)
* strict access control in consumer layer
* separate namespaces per tenant (if needed)

This ensures no cross-tenant data leakage.

---

## Q13. What happens if Kafka becomes slow or unavailable?

**Answer:**
Mitigation:

* ingestion gateway buffers temporarily
* backpressure applied to device streams
* fallback retry mechanisms
* multi-broker replication ensures availability

If degraded:

* system shifts to temporary buffering mode

---

## Q14. Why not process events directly without Kafka (real-time pipeline)?

**Answer:**
Direct processing would cause:

* tight coupling between producers and consumers
* no replay capability
* risk of data loss
* scaling limitations

Kafka decouples:

> ingestion, processing, and storage layers

---

## Q15. How do you ensure correct sequencing when device switches patients?

**Answer:**
We rely on:

* timestamp-based mapping resolution
* ingestion-time patient resolution
* Kafka ordering per patientId
* actor-level sequential processing

This ensures:

> device reuse does not break patient-level consistency

---

## Q16. How do you monitor system health?

**Answer:**
We track:

* Kafka lag per partition
* actor processing latency
* ingestion throughput per tenant
* error rates per device/patient
* DLQ (dead-letter queue) volume

---

## Q17. What is the biggest trade-off in your design?

**Answer:**
The biggest trade-off is:

> system complexity vs correctness + scalability

We introduced:

* Kafka streaming layer
* actor model
* mapping service

This increases complexity but ensures:

* strong ordering guarantees
* high scalability
* fault tolerance
* data correctness for medical systems

---

## Q18. Why is this design suitable for a medical system specifically?

**Answer:**
Because medical systems require:

* strict data correctness
* auditability
* no data loss tolerance
* historical traceability
* deterministic processing

Kafka + actor model ensures:

> reproducible, traceable, and reliable event processing under strict compliance constraints

---

## Q19. How do you handle replay of historical telemetry?

**Answer:**
Kafka enables:

* replay from offset per partition (patientId)
* rebuilding actor state
* reprocessing for analytics or corrections

This is critical for:

* audits
* compliance
* debugging medical incidents

---

## Q20. What would you improve in this architecture?

**Answer:**
Possible improvements:

* introduce stream processing engine (Flink / Spark Streaming)
* better partition balancing strategy for hot patients
* schema registry for strict telemetry validation
* stronger real-time anomaly detection layer
* adaptive partition key strategy

