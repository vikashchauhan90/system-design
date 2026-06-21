# 📘 Use Case: Tenant-Isolated Medical Telemetry Processing System for Infusion Pumps Using Kafka & Actor-Based Processing

---

## 1. Overview

In a multi-tenant medical software platform, each hospital (tenant) operates an isolated instance of the product ecosystem. Each tenant may also have multiple branches, each treated as a separate tenant for strict data isolation.

The system manages **infusion pumps (~10K per hospital)** that:

* Generate continuous telemetry data
* Receive configuration updates (pump settings, dosage rules, etc.)
* Emit logs and operational events
* May be reassigned across patients over time

A key requirement is to ensure that:

> All telemetry, logs, and configuration events are correctly correlated to the **current patient context**, even when device assignment changes dynamically.

---

## 2. Problem Statement

### 2.1 Observed Challenges

* High-volume telemetry ingestion from thousands of devices per tenant
* Device-to-patient mapping changes frequently (device reassignment problem)
* Risk of **data misassociation across patients over time**
* Need for strict tenant-level isolation (no cross-tenant leakage)
* Requirement to preserve ordering and consistency of device events per patient
* Requirement to support near real-time processing of telemetry streams

---

### 2.2 Root Cause

1. **High-frequency event streams**

   * Continuous telemetry from infusion pumps

2. **Dynamic device ownership**

   * Same device can serve different patients over time

3. **Strong requirement for contextual correctness**

   * Data must be grouped by patient context, not just device ID

4. **Need for scalable ingestion system**

   * Millions of events per day across tenants

---

## 3. Design Goals

The system must:

* Ensure strict **tenant isolation**
* Maintain **correct patient-level event grouping**
* Handle **high-throughput telemetry ingestion**
* Preserve event ordering for a given patient context
* Support dynamic device reassignment safely
* Prevent data loss in streaming pipeline
* Scale horizontally across tenants and devices

---

## 4. Proposed Solution

### 4.1 High-Level Approach

We use a combination of:

* **Apache Kafka for event streaming**
* **Patient-based partitioning strategy**
* **Actor-based consumer processing model**
* **Stateful mapping service for device-to-patient resolution**

This ensures:

> All telemetry events belonging to a patient are processed in-order and together, even if device assignments change over time.

---

## 5. Architecture Overview

```text id="med_arch_v1"
      +----------------------------+
      | Infusion Pump Devices     |
      | (Telemetry + Logs)        |
      +-------------+--------------+
                    |
                    v
        +------------------------+
        | Ingestion Gateway      |
        | (Tenant Resolver)      |
        +-----------+------------+
                    |
                    v
        +------------------------+
        | Kafka Event Stream     |
        | Partitioned by         |
        | patientId              |
        +-----------+------------+
                    |
     -----------------------------------
     |               |                 |
     v               v                 v
+---------+    +---------+      +---------+
| Actor 1 |    | Actor 2 |      | Actor 3 |
| Patient |    | Patient |      | Patient |
| Group   |    | Group   |      | Group   |
+---------+    +---------+      +---------+
     |
     v
+---------------------------+
| Patient State Database    |
| + Device Mapping Store    |
+---------------------------+
```

---

## 6. Core Design Components

---

### 6.1 Kafka Partitioning Strategy

Kafka topics are partitioned by:

```text id="kafka_partition"
patientId
```

### Why patientId?

* Ensures all events for a patient go to the same partition
* Guarantees ordering per patient
* Handles device reassignment safely by remapping at ingestion layer

---

### 6.2 Device-to-Patient Mapping Layer

A centralized state store maintains:

* Device → Patient mapping
* Valid time window for assignment
* Tenant isolation boundary

Example:

```json id="device_mapping"
{
  "deviceId": "D001",
  "tenantId": "T100",
  "patientId": "P900",
  "validFrom": "2026-01-01T10:00:00Z",
  "validTo": null
}
```

This ensures:

* historical correctness
* time-bound association
* safe reassignment

---

### 6.3 Actor-Based Consumer Model

Each Kafka partition is consumed by an **actor instance per patient group**.

Actors:

* maintain in-memory patient state
* process events sequentially
* avoid concurrency conflicts
* update database deterministically

---

### 6.4 Event Flow

```text id="event_flow_med"
Pump generates telemetry
   ↓
Ingestion Gateway resolves tenant + patient mapping
   ↓
Attach patientId to event
   ↓
Publish to Kafka (partitioned by patientId)
   ↓
Actor consumes partition
   ↓
Update patient state + persist to DB
```

---

## 7. Key Design Principles

### 7.1 Tenant Isolation

* Each tenant has separate logical data streams
* No cross-tenant event sharing in Kafka partitions
* Separate namespaces for tenant topics (optional)

---

### 7.2 Patient-Centric Partitioning

* Ensures all related telemetry is grouped
* Maintains strict ordering per patient
* Avoids interleaving device reassignment issues

---

### 7.3 Actor Model Benefits

* Single-threaded processing per patient group
* Eliminates race conditions
* Simplifies state management
* Enables deterministic processing

---

Got it — I’ll incorporate **DotNetty + KEDA scaling based on CPU/memory + TCP port utilization** into the architecture and clearly indicate *where it fits*, so you can update your document cleanly.

---



## 8 High-Throughput Ingestion Layer (DotNetty + KEDA Scaling)

### 8.1 Purpose

To handle **massive-scale TCP telemetry ingestion** from infusion pump devices, we introduce a dedicated **high-performance network ingestion layer** built using:

* **DotNetty (high-performance .NET TCP framework)**
* **KEDA (Kubernetes Event-Driven Autoscaling)**

This layer is responsible for:

* Managing **large-scale persistent TCP connections from devices**
* Efficiently receiving high-frequency telemetry streams
* Scaling ingestion dynamically based on real system load

---

## 8.2 DotNetty-Based TCP Ingestion Layer

### Why DotNetty?

We use DotNetty because:

* Supports **high-performance asynchronous TCP communication**
* Handles **thousands to millions of concurrent connections**
* Low GC pressure and event-loop based architecture
* Suitable for **long-lived medical device connections**

---

### Responsibilities of DotNetty Layer:

* Maintain persistent TCP connections with infusion pumps
* Receive telemetry + log streams in real-time
* Perform lightweight validation (schema + tenant header)
* Forward normalized events to ingestion gateway → Kafka

---

### Flow:

```text id="dotnetty_flow"
Infusion Pumps (TCP Devices)
        ↓
DotNetty TCP Gateway
        ↓
Lightweight validation (tenant/device)
        ↓
Event normalization
        ↓
Kafka ingestion pipeline
```

---

## 6.X.3 KEDA-Based Auto-Scaling Strategy

We use **KEDA (Kubernetes Event Driven Autoscaling)** to dynamically scale ingestion services.

---

### Scaling Triggers:

KEDA scales DotNetty ingestion pods based on:

### 1. CPU Utilization

* High CPU → increase ingestion pods
* Low CPU → scale down

---

### 2. Memory Utilization

* Prevent memory saturation from TCP buffers
* Ensures stable connection handling

---

### 3. TCP Connection Load (Custom Metric)

* Number of active TCP connections per pod
* Helps scale based on **real device traffic load**

---

### 4. Kafka Lag (Backpressure Signal)

* If Kafka lag increases → ingestion is saturated
* Trigger scale-out automatically

---

## 6.X.4 System-Level Flow with DotNetty + KEDA

```text id="scaled_ingestion_flow"
Infusion Pumps
     ↓
DotNetty TCP Layer (Auto-scaled by KEDA)
     ↓
Ingestion Gateway
     ↓
Kafka (partitioned by patientId)
     ↓
Actor Consumers
     ↓
Database / State Store
```

---

## 6.X.5 Key Benefits of This Addition

### 1. High Connection Scalability

* Supports **10K–100K+ concurrent TCP device connections**

---

### 2. Real-Time Ingestion Stability

* No packet loss due to backpressure-aware scaling

---

### 3. Adaptive Resource Scaling

* Automatically scales based on:

  * CPU
  * Memory
  * TCP load
  * Kafka backlog

---

### 4. Better Separation of Concerns

* DotNetty = connection management
* Kafka = event backbone
* Actors = processing logic

---

## 6.X.6 Trade-offs Introduced

| Trade-off                 | Explanation                                              |
| ------------------------- | -------------------------------------------------------- |
| Operational complexity    | Requires managing TCP + Kubernetes + KEDA together       |
| Debugging difficulty      | Harder to trace TCP → Kafka → actor flow                 |
| Resource tuning overhead  | Requires careful CPU/memory/KEDA threshold tuning        |
| Connection state handling | Long-lived TCP connections require robust recovery logic |

---

## 6.X.7 Failure Handling Additions

### 1. DotNetty Node Failure

* TCP connections are rebalanced to other nodes
* Devices reconnect automatically

---

### 2. KEDA Mis-scaling Risk

* Over-scaling → cost overhead
* Under-scaling → latency spikes
* Mitigated using multi-metric scaling strategy

---

### 3. TCP Burst Traffic

* Backpressure applied at ingestion layer
* Kafka acts as buffer to prevent data loss

---



## 8. Failure Handling Strategy

---

### 8.1 Device Reassignment Issue

**Problem:** Device switches from Patient A → Patient B

**Solution:**

* Mapping service updates association with timestamp
* New events use updated mapping
* Old events remain historically linked via event timestamp

---

### 8.2 Kafka Lag or Consumer Failure

**Mitigation:**

* Partition replay capability
* Checkpoint-based recovery
* Dead-letter queue for failed events

---

### 8.3 Actor Failure

**Mitigation:**

* Actor state rebuilt from Kafka replay
* Persistent state store backup
* Supervisor-based restart mechanism

---

### 8.4 Data Loss Prevention

**Mitigation:**

* Kafka durability (replication factor > 3)
* Acknowledged writes from ingestion layer
* At-least-once delivery semantics

---

## 9. Consistency Model

| Component        | Consistency                        |
| ---------------- | ---------------------------------- |
| Kafka            | At-least-once delivery             |
| Actor processing | Strong per-patient ordering        |
| Device mapping   | Eventual consistency               |
| Database writes  | Strong consistency per transaction |

---

## 10. Benefits

### 10.1 Data Integrity

* Ensures correct patient-level grouping
* Prevents cross-patient data mixing

---

### 10.2 Scalability

* Kafka partitions scale horizontally
* Actor model distributes processing load
* Supports 10K+ devices per tenant

---

### 10.3 Reliability

* Fault-tolerant event processing
* Replay capability ensures no data loss
* Strong ordering guarantees per patient

---

### 10.4 Observability

* Per-patient event tracing
* Tenant-level monitoring
* Partition-level lag tracking

---

## 11. Trade-offs

| Trade-off                       | Explanation                                 |
| ------------------------------- | ------------------------------------------- |
| Increased system complexity     | Actor + Kafka + mapping layer               |
| Eventual consistency in mapping | Device reassignment may lag briefly         |
| Storage overhead                | Kafka retention + state storage duplication |
| Operational overhead            | Requires monitoring partitions and actors   |
| Debug complexity                | Requires distributed tracing across systems |

---

## 12. Key Risks & Mitigations

---

### 12.1 Misrouting Due to Stale Mapping

**Risk:** Event assigned to wrong patient temporarily

**Mitigation:**

* timestamp-based validation
* correction events for reassignment
* replay mechanism

---

### 12.2 Hot Partition Problem

**Risk:** One patient generating too many events

**Mitigation:**

* dynamic partition scaling
* partition key refinement (patient + device group if needed)

---

### 12.3 Actor Overload

**Risk:** Too many active patients per actor

**Mitigation:**

* horizontal actor scaling
* partition reassignment strategy

---

## 13. Final Architecture Summary

This system ensures:

* strict tenant isolation
* patient-level event consistency
* scalable telemetry ingestion
* safe device reassignment handling
* reliable stream processing via Kafka + actor model

---

## 14. Key Takeaway

This architecture transforms a **high-volume medical telemetry system** into a:

> patient-centric, event-driven, ordered stream processing platform that guarantees correctness, scalability, and tenant isolation even under dynamic device reassignment scenarios.

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

