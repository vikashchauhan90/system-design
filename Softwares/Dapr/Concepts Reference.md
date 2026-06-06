# Dapr: Complete Distributed Application Runtime Reference

## Document Overview

This document provides a comprehensive analysis of Dapr's (Distributed Application Runtime) architectural patterns, distributed systems algorithms, and building block implementations. Dapr is a portable, event-driven runtime that makes it easy for any developer to build resilient, stateless, and stateful applications that run on the cloud and edge . Unlike traditional frameworks that require embedding SDKs, Dapr uses a **sidecar architecture** that operates alongside your application, providing distributed system capabilities through standard HTTP/gRPC APIs . This document covers the core sidecar architecture, each building block's implementation details, the actor model, workflow engine, placement and coordination mechanisms, and cross-cutting concerns like resiliency and observability.

---

## Table of Contents

1. [Core Architectural Patterns](#core-architectural-patterns)
2. [Sidecar Architecture & Runtime](#sidecar-architecture--runtime)
3. [Service Invocation Building Block](#service-invocation-building-block)
4. [State Management Building Block](#state-management-building-block)
5. [Publish & Subscribe Building Block](#publish--subscribe-building-block)
6. [Virtual Actor Model (Actors Building Block)](#virtual-actor-model-actors-building-block)
7. [Workflow Building Block](#workflow-building-block)
8. [Placement Service & Distribution](#placement-service--distribution)
9. [Additional Building Blocks](#additional-building-blocks)
10. [Resiliency & Observability](#resiliency--observability)
11. [Complete System Interaction Diagram](#complete-system-interaction-diagram)

---

## Core Architectural Patterns

### 1. Sidecar Pattern

**Purpose**: Decouple distributed systems concerns from application business logic by running Dapr as a companion process alongside each application instance .

**Core Principle**:
Instead of embedding service discovery, retry logic, state management, or pub/sub code into your application, Dapr runs as a separate process (sidecar) that your application calls via local HTTP/gRPC endpoints.

**Sidecar Architecture Visualization**:

```
┌─────────────────────────────────────────────────────────────┐
│                         POD / VM                             │
│  ┌─────────────────────┐    ┌─────────────────────────────┐ │
│  │   Application       │    │    Dapr Sidecar (daprd)     │ │
│  │   Container/Process │    │                             │ │
│  │                     │    │  ┌─────────────────────────┐│ │
│  │   http://localhost  │───▶│  │ Building Block APIs:    ││ │
│  │   :3500/v1.0/...   │    │  │ - Service Invocation    ││ │
│  │                     │◀───│  │ - State Management      ││ │
│  │                     │    │  │ - Pub/Sub               ││ │
│  │                     │    │  │ - Actors                ││ │
│  │                     │    │  │ - Workflows             ││ │
│  │                     │    │  │ - Bindings              ││ │
│  │                     │    │  │ - Secrets               ││ │
│  │                     │    │  └─────────────────────────┘│ │
│  └─────────────────────┘    └─────────────────────────────┘ │
└─────────────────────────────────────────────────────────────┘
```

**Why Sidecar Over SDK**:

| Aspect | Traditional SDK | Dapr Sidecar |
|--------|----------------|--------------|
| **Language support** | Requires SDK per language | Any language (HTTP/gRPC) |
| **Upgrades** | Rebuild and redeploy app | Sidecar updated independently |
| **Cross-cutting concerns** | Embedded in business logic | Separated, reusable |
| **Portability** | Tied to specific implementations | Swap components via config |
| **Versioning** | App and SDK version coupled | Independent versioning |

### 2. Building Block Abstraction

**Purpose**: Codify best practices for distributed systems into independent, portable APIs .

**Building Block Architecture**:

```
Application Code
      │
      │ HTTP/gRPC call to local sidecar
      ▼
┌─────────────────────────────────────────────────────────────┐
│                    Dapr Sidecar (daprd)                      │
│  ┌─────────────┐ ┌─────────────┐ ┌─────────────┐            │
│  │  Service    │ │   State     │ │   Pub/Sub   │            │
│  │  Invocation │ │  Management │ │             │            │
│  │  API        │ │  API        │ │  API        │            │
│  └──────┬──────┘ └──────┬──────┘ └──────┬──────┘            │
│         │               │               │                    │
│  ┌──────┴──────┐ ┌──────┴──────┐ ┌──────┴──────┐            │
│  │ Component  │ │ Component   │ │ Component  │            │
│  │ (e.g.,     │ │ (e.g.,      │ │ (e.g.,     │            │
│  │ mDNS/      │ │ Redis/      │ │ Kafka/     │            │
│  │ Consul)    │ │ CosmosDB)   │ │ SNS/SQS)   │            │
│  └────────────┘ └────────────┘ └────────────┘              │
└─────────────────────────────────────────────────────────────┘
```

**Available Building Blocks** :

| Building Block | Primary Purpose | Key Capabilities |
|----------------|-----------------|------------------|
| **Service-to-service invocation** | Resilient service communication | Retries, timeouts, mTLS, service discovery |
| **State management** | Key/value state persistence | CRUD operations, querying, concurrency control |
| **Publish & subscribe** | Event-driven messaging | At-least-once delivery, consumer groups, TTL |
| **Workflows** | Long-running durable processes | Orchestration, replay, activity scheduling |
| **Actors** | Stateful, single-threaded objects | Virtual actor pattern, timers, reminders |
| **Bindings** | External system integration | Input/output bindings, triggers |
| **Secrets** | Secure configuration management | Integration with secret stores |
| **Configuration** | Dynamic app configuration | Retrieval, subscription to changes |
| **Distributed lock** | Resource exclusivity | Lease-based locking across instances |
| **Cryptography** | Key management abstraction | Encrypt/decrypt without key exposure |
| **Jobs** | Scheduled execution | Time or interval-based job scheduling |

### 3. Portability via Components

**Purpose**: Enable application portability across infrastructure by swapping component implementations via configuration .

**Component Configuration Example** (Pub/Sub with RabbitMQ):

```yaml
apiVersion: dapr.io/v1alpha1
kind: Component
metadata:
  name: order-pub-sub
spec:
  type: pubsub.rabbitmq
  version: v1
  metadata:
  - name: host
    value: "amqp://localhost:5672"
scopes:
  - orderprocessing
  - checkout
```

**Switching to Redis** (no code change!):
```yaml
apiVersion: dapr.io/v1alpha1
kind: Component
metadata:
  name: order-pub-sub
spec:
  type: pubsub.redis    # Only the type changes
  version: v1
  metadata:
  - name: redisHost
    value: "localhost:6379"
```

**Portability Benefits**:
- Application code remains unchanged when switching between cloud providers
- Same code runs on local development, staging, and production
- No vendor lock-in for state, messaging, or secret management

---

## Sidecar Architecture & Runtime

### 4. daprd Process

**Purpose**: The Dapr sidecar process that runs alongside each application instance .

**Exposed APIs** :

| API Endpoint | Purpose |
|--------------|---------|
| `http://localhost:3500/v1.0/...` | HTTP API for all building blocks |
| `http://localhost:50001` (gRPC) | gRPC API (higher performance) |
| `http://localhost:3500/v1.0/metadata` | Discover capabilities and set attributes |
| `http://localhost:3500/v1.0/healthz` | Health and readiness status |

**Readiness State**:
The Dapr sidecar reaches readiness only after the application is accessible on its configured port . This ensures components are only invoked when the full stack is ready.

### 5. Sidecar Injection (Kubernetes)

**How Dapr Injects Sidecars in Kubernetes** :

- **dapr-sidecar-injector**: Mutating webhook that adds Dapr container to pods
- **dapr-operator**: Manages Dapr component lifecycle
- **dapr-sentry**: Certificate authority for mTLS (discussed later)

**Kubernetes Pod with Dapr Sidecar**:

```yaml
apiVersion: apps/v1
kind: Deployment
metadata:
  name: order-service
spec:
  template:
    metadata:
      annotations:
        dapr.io/enabled: "true"        # Enables sidecar injection
        dapr.io/app-id: "order-service"
        dapr.io/app-port: "80"
    spec:
      containers:
      - name: order-service              # Your application container
        image: myregistry/order-service
      - name: daprd                      # Injected automatically
        image: daprio/daprd:latest
```

---

## Service Invocation Building Block

### 6. Service Discovery & Invocation

**Purpose**: Enable resilient service-to-service communication with built-in retries, timeouts, and observability .

**Invocation Pattern**:

```http
# Invoke method on remote service
POST http://localhost:3500/v1.0/invoke/cart/method/order

# Where:
# - cart: target application ID (service name)
# - order: method name
```

**How It Works** :
1. Application calls its local Dapr sidecar
2. Sidecar acts as reverse proxy with integrated service discovery
3. Sidecar routes request to target service's sidecar
4. Target sidecar forwards to target application
5. Response returns through the same path

**Name Resolution**:
- **Self-hosted**: Uses mDNS (multicast DNS) by default, supports Hashicorp Consul 
- **Kubernetes**: Uses Kubernetes DNS service discovery

**Resiliency Features**:
- Automatic retries with backoff
- Circuit breaker patterns
- Timeout configuration
- Distributed tracing across invocations

### 7. Service Invocation Flow

```
┌──────────┐      ┌──────────┐      ┌──────────┐      ┌──────────┐
│ Service A│      │Sidecar A │      │Sidecar B │      │ Service B│
└────┬─────┘      └────┬─────┘      └────┬─────┘      └────┬─────┘
     │                 │                 │                 │
     │ POST /invoke/B/method            │                 │
     │────────────────▶│                 │                 │
     │                 │                 │                 │
     │                 │ Resolve B location               │
     │                 │ (mDNS/K8s DNS/Consul)            │
     │                 │                 │                 │
     │                 │ Forward request  │                 │
     │                 │────────────────▶│                 │
     │                 │                 │                 │
     │                 │                 │ Forward to B    │
     │                 │                 │────────────────▶│
     │                 │                 │                 │
     │                 │                 │◀────────────────│
     │                 │                 │ Response        │
     │                 │◀────────────────│                 │
     │                 │ Response        │                 │
     │◀────────────────│                 │                 │
     │ Response        │                 │                 │
```

---

## State Management Building Block

### 8. Key/Value State API

**Purpose**: Provide pluggable, durable state storage with consistent APIs across different storage backends .

**Basic Operations**:

```http
# Get state
GET http://localhost:3500/v1.0/state/inventory/item50

# Save state
POST http://localhost:3500/v1.0/state/inventory
{
  "key": "item50",
  "value": { "name": "Widget", "quantity": 100 }
}

# Delete state
DELETE http://localhost:3500/v1.0/state/inventory/item50
```

**Supported State Stores** :
- Redis
- AWS DynamoDB
- Azure Cosmos DB
- Azure SQL Server
- GCP Firestore
- PostgreSQL
- MongoDB
- And many more

### 9. Concurrency Control

**Purpose**: Prevent lost updates in distributed environments.

**Optimistic Concurrency with ETags**:

```http
# First read includes ETag
GET http://localhost:3500/v1.0/state/inventory/item50
Response:
{
  "key": "item50",
  "value": { "quantity": 100 },
  "etag": "1234567890"
}

# Conditional update (fails if ETag doesn't match)
POST http://localhost:3500/v1.0/state/inventory
{
  "key": "item50",
  "value": { "quantity": 99 },
  "etag": "1234567890",
  "options": {
    "concurrency": "first-write"
  }
}
```

### 10. State Querying

**Purpose**: Query state stores that support filtering and pagination.

**Query API**:
```http
POST http://localhost:3500/v1.0/state/inventory/query
{
  "query": {
    "filter": {
      "EQ": { "quantity": 0 }
    },
    "sort": [
      { "key": "name", "order": "ASC" }
    ],
    "limit": 10
  }
}
```

---

## Publish & Subscribe Building Block

### 11. CloudEvents & Topic Routing

**Purpose**: Enable event-driven architectures with decoupled publishers and subscribers .

**Publishing Events**:

```http
# Publish to topic
POST http://localhost:3500/v1.0/publish/order-pub-sub/order-messages
Content-Type: application/json

{
  "orderId": "123",
  "customerId": "456",
  "total": 99.95
}
```

**Declarative Subscription** (Kubernetes):

```yaml
apiVersion: dapr.io/v1alpha1
kind: Subscription
metadata:
  name: order-subscription
spec:
  topic: order-messages
  route: /orders
  pubsubname: order-pub-sub
scopes:
- order-processing-service
```

**Programmatic Subscription** (.NET SDK):
```csharp
app.MapPost("/orders", async (Order order) =>
{
    await _orderProcessor.ProcessAsync(order);
    return Results.Ok();
});
```

### 12. Message Delivery Guarantees

**At-Least-Once Delivery** :
- Dapr guarantees messages are delivered at least once to each subscriber
- Subscribers must handle idempotency (duplicate messages possible)

**Consumer Groups**:
- Multiple instances of same service form consumer group
- Messages distributed across instances for horizontal scaling

**Message TTL (Time To Live)**:
- Messages expire after configured TTL
- Prevents processing of stale events

### 13. Pub/Sub Component Architecture

```
Publisher                    Dapr Sidecar                        Subscriber
     │                             │                                   │
     │  Publish to topic           │                                   │
     │────────────────────────────▶│                                   │
     │                             │                                   │
     │                             │  Forward to message broker        │
     │                             │  (Redis, Kafka, Service Bus)      │
     │                             │                                   │
     │                             │◀──────────────────────────────────│
     │                             │  Message delivered                │
     │                             │                                   │
     │                             │  Forward to subscriber            │
     │                             │──────────────────────────────────▶│
```

---

## Virtual Actor Model (Actors Building Block)

### 14. Virtual Actor Pattern

**Purpose**: Provide stateful, single-threaded units of computation with automatic lifecycle management .

**What Makes Actors "Virtual"** :

| Traditional Actors | Virtual Actors (Dapr/Orleans) |
|--------------------|-------------------------------|
| Explicit creation/destruction | Automatically activated on first use |
| Manual lifecycle management | Automatically deactivated when idle |
| Local state in memory | State persisted to external storage |
| Need to track actor addresses | Identity-based (type + ID) addressing |
| Scaling requires custom logic | Runtime handles distribution |

**Core Actor Properties** :
- **Stateful**: Maintains state across invocations
- **Single-threaded**: Processes one message at a time (no concurrency within actor)
- **Isolated**: No shared state with other actors
- **Message-driven**: Communicates only via messages
- **Location-transparent**: Client doesn't need to know where actor runs

### 15. Actor Identity & Addressing

**Actor Identity** :
- Uniquely identified by combination of **Actor Type** and **Actor ID**
- Example: `LightActor` (type) + `42` (ID) = `LightActor-42`

**Addressing Flow** :
1. Client calls `http://localhost:3500/v1.0/actors/LightActor/42/method/TurnOn`
2. Dapr sidecar hashes actor type + ID
3. Uses placement table to determine which node hosts this actor
4. Routes request to that node's sidecar
5. Target sidecar activates actor if not already running
6. Actor processes request and returns response

### 16. Actor Lifecycle

**Activation** :
- Occurs automatically when a method is called on a non-existent actor
- Dapr sidecar loads actor state from configured state store
- Actor instance is created in memory

**Deactivation**:
- Occurs after actor is idle for configured timeout period
- In-memory instance is garbage collected
- State persists in state store for reactivation

**State Persistence**:
- Actor state stored in pluggable state store (same as state management building block)
- State is saved between activations
- Allows actors to be moved between nodes

### 17. Actor Concurrency (Turn-Based)

**Purpose**: Eliminate locking and race conditions by processing one message at a time.

**Turn-Based Processing** :
- Each actor processes messages sequentially
- While an actor is processing, other requests queue
- No two threads execute within same actor simultaneously

**Reentrancy** (v1.15+):
- Actors can optionally allow reentrant calls
- Enables scenarios where actor calls another actor that calls back

### 18. Timers & Reminders

**Timers** :
- Fire once or on a schedule while actor is active
- Do NOT survive actor deactivation
- Used for temporary, while-active scheduling

```csharp
// Register timer that fires every 10 seconds
await actor.RegisterTimerAsync(
    "MyTimer",
    nameof(MyTimerCallback),
    TimeSpan.FromSeconds(5),  // Initial delay
    TimeSpan.FromSeconds(10)); // Period
```

**Reminders** :
- Fire on schedule and survive actor deactivation
- Dapr reactivates actor when reminder fires
- Persisted to state store for durability

```csharp
// Register persistent reminder
await actor.RegisterReminderAsync(
    "MyReminder",
    null,
    TimeSpan.FromMinutes(1),  // Due time
    TimeSpan.FromMinutes(5));  // Period
```

**Comparison**:

| Feature | Timer | Reminder |
|---------|-------|----------|
| Survives deactivation | No | Yes |
| Persistence | In-memory only | State store |
| Overhead | Low | Higher |
| Use case | While-active monitoring | Long-running schedules |

### 19. Actor Placement & Distribution

**Placement Service** :
- Central control plane service managing actor distribution
- Uses **Raft consensus algorithm** for state replication
- Maintains partition table mapping actor IDs to service instances
- Communicates with sidecars via mTLS

**Placement Flow** :

```
┌─────────────┐     ┌─────────────────┐     ┌─────────────┐     ┌─────────────┐
│ Sidecar A   │     │ Placement       │     │ Sidecar B   │     │ Sidecar C   │
│ (hosts      │     │ Service         │     │ (hosts      │     │ (hosts      │
│  LightActor)│     │                 │     │  LightActor)│     │  LightActor)│
└──────┬──────┘     └────────┬────────┘     └──────┬──────┘     └──────┬──────┘
       │                     │                     │                   │
       │ Register actor type │                     │                   │
       │────────────────────▶│                     │                   │
       │                     │                     │                   │
       │                     │ Register actor type │                   │
       │                     │◀────────────────────│                   │
       │                     │                     │                   │
       │                     │ Register actor type │                   │
       │                     │◀────────────────────────────────────────│
       │                     │                     │                   │
       │                     │ Compute placement   │                   │
       │                     │ using consistent    │                   │
       │                     │ hashing             │                   │
       │                     │                     │                   │
       │                     │ Push placement table│                   │
       │◀────────────────────│────────────────────▶│                   │
       │                     │                     │                   │
```

**Distribution Algorithm** :
- Placement service uses consistent hashing to distribute actors
- Actors are randomly placed across instances for uniform distribution
- Same actor ID always maps to same partition (deterministic)
- When instances are added/removed, placement tables are recomputed and pushed

---

## Workflow Building Block

### 20. Sidecar-as-Scheduler Pattern

**Purpose**: Define long-running, durable, persistent processes spanning multiple microservices .

**Core Architecture** :

```
┌─────────────────────────────────────────────────────────────────┐
│                    Dapr Workflow Architecture                    │
├─────────────────────────────────────────────────────────────────┤
│                                                                  │
│  ┌─────────────────────┐        ┌─────────────────────────────┐ │
│  │  Client Application │        │  Dapr Workflow Engine       │ │
│  │                     │        │  (in Dapr sidecar)          │ │
│  │  StartWorkflow()    │───────▶│                              │ │
│  │  QueryWorkflow()    │        │  - Manages state transitions│ │
│  │  TerminateWorkflow()│        │  - Persists history         │ │
│  └─────────────────────┘        │  - Schedules tasks          │ │
│                                 └──────────────┬──────────────┘ │
│                                                │                 │
│                                                │ gRPC            │
│                                                ▼                 │
│                                 ┌─────────────────────────────┐ │
│                                 │  Workflow Worker            │ │
│                                 │  (Application code)         │ │
│                                 │                             │ │
│                                 │  - Orchestrator logic       │ │
│                                 │  - Activity implementation  │ │
│                                 └─────────────────────────────┘ │
└─────────────────────────────────────────────────────────────────┘
```

**Key Components** :

| Component | Responsibility | Runs In |
|-----------|---------------|---------|
| **Workflow Engine** | Manages state transitions, history persistence, scheduling | Dapr sidecar |
| **Workflow Worker** | Executes user-defined orchestrator and activity logic | Application |
| **Task Hub** | Backend persistence (Dapr Actors by default) | State store |

### 21. Replay-Based Orchestration

**Purpose**: Achieve deterministic, durable execution without complex error handling .

**How Replay Works**:
1. Workflow engine persists **every event** (orchestrator start, activity completion, timer firing)
2. When recovering or continuing, engine re-reads event history
3. Orchestrator logic re-executes from beginning
4. Any non-deterministic operations (timers, activity calls) are replayed from history rather than re-executed

**Determinism Requirements** :
- Orchestrator code must be **side-effect free** except via engine-mediated effects
- No direct calls to DateTime.Now, Random, or external I/O
- All non-deterministic operations must use engine APIs (CreateTimer, CallActivity)

**Why Replay-Based?** :
- Simpler developer experience (write linear code, not state machines)
- Automatic fault tolerance (engine handles retries)
- Durable execution without complex checkpointing

**Protocol Surfaces** :

| Protocol | Purpose | Direction |
|----------|---------|-----------|
| **Management API** | Start, terminate, pause, resume, query workflows | Client → Engine |
| **Execution API (Task Hub)** | Poll for work items, report completions | Engine → Worker |

### 22. Orchestrator vs. Activity

**Orchestrator** :
- Defines the workflow's control flow (sequences, parallelism, conditions)
- Must be deterministic (replayable)
- Cannot perform I/O directly
- Schedules activities, sub-orchestrations, timers

```csharp
[Workflow]
public class OrderWorkflow : Workflow<OrderPayload, OrderResult>
{
    public override async Task<OrderResult> RunAsync(WorkflowContext context, OrderPayload payload)
    {
        // Schedule activity (non-deterministic work)
        var inventoryResult = await context.CallActivityAsync<bool>(
            nameof(CheckInventoryActivity), payload);

        if (inventoryResult)
        {
            await context.CallActivityAsync(nameof(ProcessPaymentActivity), payload);
            await context.CallActivityAsync(nameof(ShipOrderActivity), payload);
            return new OrderResult { Success = true };
        }
        
        return new OrderResult { Success = false, Reason = "Out of stock" };
    }
}
```

**Activity** :
- Performs actual work (database calls, external API calls)
- Need not be deterministic
- Executed at-least-once (design for idempotency)
- Reports results or failures back to engine

```csharp
[Activity]
public class CheckInventoryActivity : WorkflowActivity<OrderPayload, bool>
{
    public override async Task<bool> RunAsync(WorkflowContext context, OrderPayload payload)
    {
        // This can call databases, external APIs, etc.
        var stock = await _inventoryService.CheckStockAsync(payload.ProductId);
        return stock >= payload.Quantity;
    }
}
```

### 23. Workflow Execution Guarantees

**At-Least-Once Activities** :
- Activities may be delivered more than once
- **Recommendation**: Design activities to be idempotent
- Activity context provides execution ID to assist idempotency

**Exactly-Once State Commit** :
- Workflow state commits are idempotent and applied exactly once
- Ensures orchestrator state consistency despite activity retries

**Sidecar-as-Scheduler** :
- Engine persists all history BEFORE dispatching work to workers
- Workers are stateless executors from engine's perspective
- Enables workflow to survive worker crashes

---

## Placement Service & Distribution

### 24. Raft Consensus for Placement Service

**Purpose**: Provide strong consistency for actor placement information across the cluster .

**Why Raft** :
- Ensures all placement service replicas agree on partition tables
- Prevents split-brain scenarios where different nodes have different actor locations
- Provides high availability (follower can take over if leader fails)

**Raft in Dapr Placement**:
- Placement service runs with multiple replicas for high availability
- One leader, multiple followers
- All writes go through leader
- Followers replicate log and can become leader if needed

### 25. Consistent Hashing for Actor Placement

**Purpose**: Deterministically map actor IDs to service instances .

**Algorithm**:
```
partition = hash(actor_type + actor_id) % total_partitions
node = placement_table[partition]
```

**Properties**:
- Same actor ID always maps to same partition (deterministic)
- Adding/removing nodes only redistributes a subset of actors
- Supports uniform distribution across instances

---

## Additional Building Blocks

### 26. Resource Bindings

**Purpose**: Connect to external systems as either input (trigger) or output (invoke) .

**Output Binding** (sending to external system):
```http
POST http://localhost:3500/v1.0/bindings/kafka
{
  "data": { "orderId": "123" },
  "operation": "create"
}
```

**Input Binding** (receiving triggers):
```yaml
apiVersion: dapr.io/v1alpha1
kind: Component
metadata:
  name: cron-binding
spec:
  type: bindings.cron
  version: v1
  metadata:
  - name: schedule
    value: "@every 5m"
scopes:
- report-generator
```

### 27. Secrets Management

**Purpose**: Retrieve secrets from secure stores without hardcoding credentials .

```http
GET http://localhost:3500/v1.0/secrets/kubernetes/db-connection-string
```

**Supported Secret Stores**:
- Kubernetes Secrets
- HashiCorp Vault
- AWS Secrets Manager
- Azure Key Vault
- GCP Secret Manager

### 28. Distributed Lock

**Purpose**: Provide exclusive access to resources across multiple application instances .

```http
# Acquire lock
POST http://localhost:3500/v1.0-alpha1/lock/my-lock-store/mutex
{
  "ownerId": "instance-1",
  "expiryInSeconds": 60
}

# Response
{
  "success": true
}
```

### 29. Configuration API

**Purpose**: Retrieve and subscribe to application configuration items from configuration stores .

```http
# Get configuration
GET http://localhost:3500/v1.0/configuration/redis-config-store/order-settings

# Subscribe to changes
GET http://localhost:3500/v1.0/configuration/redis-config-store/order-settings/subscribe
```

### 30. Cryptography API

**Purpose**: Perform cryptographic operations without exposing keys to the application .

```http
# Encrypt
POST http://localhost:3500/v1.0-alpha1/crypto/azure-keyvault/encrypt
{
  "plaintext": "U2VjcmV0RGF0YQ==",
  "keyName": "my-encryption-key"
}
```

### 31. Jobs API

**Purpose**: Schedule jobs to run at specific times or intervals .

```http
# Schedule job
POST http://localhost:3500/v1.0-alpha1/jobs/scheduler/create-job
{
  "name": "daily-report",
  "schedule": "0 9 * * *",
  "payload": { "reportType": "sales" }
}
```

---

## Resiliency & Observability

### 32. Resiliency Policies

**Purpose**: Define fault tolerance patterns without modifying application code .

**Resiliency Specification** (YAML):
```yaml
apiVersion: dapr.io/v1alpha1
kind: Resiliency
metadata:
  name: my-resiliency
spec:
  policies:
    retries:
      my-retry:
        policy: constant
        duration: 1s
        maxRetries: 3
    timeouts:
      my-timeout:
        timeout: 5s
    circuitBreakers:
      my-cb:
        maxRequests: 1
        interval: 8s
        timeout: 6s
        trip: consecutiveFailures > 5
        
  targets:
    apps:
      order-service:
        retry: my-retry
        timeout: my-timeout
    components:
      order-pub-sub:
        outbound:
          retry: my-retry
          circuitBreaker: my-cb
```

**Supported Patterns** :
- **Timeouts**: Maximum time to wait for operation
- **Retries/Backoffs**: Automatic retry with configurable backoff
- **Circuit Breakers**: Prevent cascading failures

### 33. Observability

**Purpose**: Provide insight into application and Dapr runtime behavior .

**Three Pillars**:

| Pillar | Implementation | Use |
|--------|----------------|-----|
| **Distributed Tracing** | W3C Trace Context, OpenTelemetry | Track request flow across services |
| **Metrics** | Prometheus metrics endpoint | Monitor performance and errors |
| **Logs** | Structured logging | Debug and audit |

**Tracing Example**:
- All Dapr API calls automatically propagate trace context
- Works with Azure Monitor, Jaeger, Zipkin, New Relic, etc.
- Correlates service invocation, state operations, and pub/sub

### 34. Security: mTLS and Spiffe IDs

**Purpose**: Secure communication between Dapr sidecars and control plane .

**dapr-sentry Service** :
- Certificate authority for the Dapr control plane
- Issues mTLS certificates to sidecars
- Enables encrypted communication between sidecars

**Spiffe Identities**:
- Each Dapr sidecar gets a Spiffe ID based on its application ID
- Enables identity-based authentication and authorization

**Access Policies**:
- Fine-grained access control for service invocation and pub/sub
- Define which services can call which methods

---

## Complete System Interaction Diagram

```
┌─────────────────────────────────────────────────────────────────────────────────────┐
│                              CLIENT / USER                                           │
└────────────────────────────────────────┬────────────────────────────────────────────┘
                                         │
                                         ▼
┌─────────────────────────────────────────────────────────────────────────────────────┐
│                            CLIENT SIDECAR (daprd)                                    │
│  ┌─────────────────────────────────────────────────────────────────────────────┐    │
│  │  HTTP/gRPC API (localhost:3500)                                              │    │
│  │  ┌──────────┐ ┌──────────┐ ┌──────────┐ ┌──────────┐ ┌──────────┐          │    │
│  │  │ Service  │ │  State   │ │ Pub/Sub  │ │ Actors   │ │Workflows │          │    │
│  │  │ Invoke   │ │  Manage  │ │          │ │          │ │          │          │    │
│  │  └────┬─────┘ └────┬─────┘ └────┬─────┘ └────┬─────┘ └────┬─────┘          │    │
│  │       │            │            │            │            │                 │    │
│  │       ▼            ▼            ▼            ▼            ▼                 │    │
│  │  ┌────────────────────────────────────────────────────────────────────┐    │    │
│  │  │                        Component Resolution                         │    │    │
│  │  │  (Which component handles this building block API call?)           │    │    │
│  │  └────────────────────────────────────────────────────────────────────┘    │    │
│  └─────────────────────────────────────────────────────────────────────────────┘    │
└────────────────────────────────────────┬────────────────────────────────────────────┘
                                         │
          ┌──────────────────────────────┼──────────────────────────────┐
          │                              │                              │
          ▼                              ▼                              ▼
┌─────────────────────┐    ┌─────────────────────────┐    ┌─────────────────────────┐
│   STATE STORE       │    │   MESSAGE BROKER        │    │   SERVICE DISCOVERY     │
│   (Component)       │    │   (Component)           │    │   (Component)           │
│                     │    │                         │    │                         │
│ ┌─────────────────┐ │    │ ┌─────────────────────┐ │    │ ┌─────────────────────┐ │
│ │ Redis           │ │    │ │ Kafka/RabbitMQ      │ │    │ │ mDNS (self-hosted)  │ │
│ │ Cosmos DB       │ │    │ │ SNS/SQS/Service Bus │ │    │ │ Consul (optional)   │ │
│ │ PostgreSQL      │ │    │ │                     │ │    │ │ K8s DNS (k8s)       │ │
│ └─────────────────┘ │    │ └─────────────────────┘ │    │ └─────────────────────┘ │
└─────────────────────┘    └─────────────────────────┘    └─────────────────────────┘
                                         │
                                         │ (Service invocation path)
                                         ▼
┌─────────────────────────────────────────────────────────────────────────────────────┐
│                           TARGET SIDECAR (daprd)                                     │
│  ┌─────────────────────────────────────────────────────────────────────────────┐    │
│  │                        Building Block APIs (same as client)                   │    │
│  └─────────────────────────────────────────────────────────────────────────────┘    │
└────────────────────────────────────────┬────────────────────────────────────────────┘
                                         │
                                         │ HTTP/gRPC to application
                                         ▼
┌─────────────────────────────────────────────────────────────────────────────────────┐
│                         TARGET APPLICATION CONTAINER                                 │
│  ┌─────────────────────────────────────────────────────────────────────────────┐    │
│  │                         Business Logic                                       │    │
│  │  - Order processing                                                          │    │
│  │  - Inventory management                                                      │    │
│  │  - User authentication                                                       │    │
│  └─────────────────────────────────────────────────────────────────────────────┘    │
└─────────────────────────────────────────────────────────────────────────────────────┘

┌─────────────────────────────────────────────────────────────────────────────────────┐
│                              DAPR CONTROL PLANE                                      │
│                                                                                      │
│  ┌─────────────────┐  ┌─────────────────┐  ┌─────────────────┐  ┌─────────────────┐ │
│  │   Placement     │  │     Operator    │  │  Sidecar        │  │     Sentry      │ │
│  │   Service       │  │                 │  │  Injector       │  │                 │ │
│  │  (Raft-based    │  │ (Component      │  │ (K8s mutating   │  │ (mTLS CA,       │ │
│  │   coordination) │  │  lifecycle)     │  │  webhook)       │  │  Spiffe IDs)    │ │
│  └─────────────────┘  └─────────────────┘  └─────────────────┘  └─────────────────┘ │
│                                                                                      │
│  Observability: Prometheus metrics, OpenTelemetry traces, structured logs          │
│  Resiliency: Retry policies, circuit breakers, timeouts (configurable per target)  │
│                                                                                      │
└─────────────────────────────────────────────────────────────────────────────────────┘
```

---

## Algorithm & Pattern Summary Table

| # | Algorithm/Pattern | Primary Purpose | Dapr Component |
|---|------------------|-----------------|----------------|
| 1 | Sidecar Pattern | Decouple distributed systems concerns | daprd process |
| 2 | Building Block Abstraction | Standardized APIs for distributed capabilities | All building blocks |
| 3 | Component Portability | Infrastructure abstraction | Component specification |
| 4 | Virtual Actor Pattern | Stateful single-threaded objects | Actors building block |
| 5 | Raft Consensus | Strongly consistent actor placement | Placement service |
| 6 | Consistent Hashing | Deterministic actor distribution | Actor placement algorithm |
| 7 | Turn-Based Concurrency | Actor message processing (no locking) | Actor runtime |
| 8 | Replay-Based Orchestration | Durable workflow execution | Workflow engine |
| 9 | Sidecar-as-Scheduler | Workflow engine in sidecar | Workflow building block |
| 10 | At-Least-Once Delivery | Message reliability | Pub/Sub building block |
| 11 | Optimistic Concurrency (ETag) | Lost-update prevention | State management |
| 12 | mTLS + Spiffe IDs | Service identity and encryption | Sentry service |
| 13 | Circuit Breaker Pattern | Cascading failure prevention | Resiliency spec |
| 14 | Retry with Backoff | Transient fault handling | Resiliency spec |
| 15 | Distributed Tracing (W3C) | Request flow observability | Observability |
| 16 | Actor Reminders | Persistent scheduled callbacks | Actor timers/reminders |
| 17 | Binding Pattern | External system integration | Bindings building block |

---

## Configuration Reference

### Component Configuration (Kubernetes)

```yaml
apiVersion: dapr.io/v1alpha1
kind: Component
metadata:
  name: statestore
spec:
  type: state.redis
  version: v1
  metadata:
  - name: redisHost
    value: "redis-master:6379"
  - name: redisPassword
    secretKeyRef:
      name: redis-secret
      key: password
scopes:
- order-service
- inventory-service
```

### Subscription Configuration

```yaml
apiVersion: dapr.io/v1alpha1
kind: Subscription
metadata:
  name: order-subscription
spec:
  topic: orders
  route: /orders
  pubsubname: pubsub
  metadata:
    rawPayload: "true"
scopes:
- order-processor
```

### Resiliency Configuration

```yaml
apiVersion: dapr.io/v1alpha1
kind: Resiliency
metadata:
  name: order-resiliency
spec:
  policies:
    retries:
      standard-retry:
        policy: constant
        duration: 100ms
        maxRetries: 3
    timeouts:
      default-timeout:
        timeout: 5s
  targets:
    apps:
      "*":
        timeout: default-timeout
        retry: standard-retry
```

---

## Performance & Scale Characteristics

| Component | Characteristic | Notes |
|-----------|---------------|-------|
| Sidecar latency | ~1-5ms (local) | HTTP/gRPC overhead |
| Actor activation | ~50-200ms | Includes state load |
| Actor reminder | 1 second resolution | Configurable |
| Workflow replay | O(history events) | Linear in history size |
| Placement table sync | 1 second | For actor placement changes |
| Message TTL | Configurable | Per message/component |
| State store operations | O(1) (key-based) | Depends on underlying store |

---

## Comparison to Other Technologies

| Feature | Dapr | Service Mesh (Istio) | Spring Cloud | AWS App Mesh |
|---------|------|---------------------|--------------|--------------|
| **Primary focus** | Application building blocks | Network traffic management | Java ecosystem | AWS native |
| **Sidecar purpose** | Distributed systems capabilities | Traffic control | Not used | Traffic control |
| **Language support** | Any (HTTP/gRPC) | Any | JVM only | Any |
| **State management** | Yes | No | Limited | No |
| **Pub/Sub** | Yes | No | Limited | No |
| **Actors** | Yes | No | No | No |
| **Workflows** | Yes | No | No | No |
| **Secret management** | Yes | Limited (via CSI) | Yes | No |
| **Portability** | Multi-cloud | Multi-cloud | Java-focused | AWS-only |

---

## Source Code Reference

| Component | Repository |
|-----------|------------|
| Core daprd | `dapr/dapr` |
| .NET SDK | `dapr/dotnet-sdk` |
| Actors | `dapr/dapr/pkg/actors` |
| Placement | `dapr/dapr/pkg/placement` |
| Workflow | `dapr/dapr/pkg/workflows` |
| Components Contrib | `dapr/components-contrib` |

---

## .NET SDK Analyzers 

| Diagnostic ID | Package | Description |
|---------------|---------|-------------|
| DAPR1301 | Dapr.Workflow | Workflow type not registered with DI |
| DAPR1302 | Dapr.Workflow | Activity type not registered with DI |
| DAPR1401 | Dapr.Actors | Actor timer callback method missing |
| DAPR1402 | Dapr.Actors | Actor type not registered with DI |
| DAPR1403 | Dapr.Actors | Set UseJsonSerialization for cross-language |
| DAPR1404 | Dapr.Actors | Call app.MapActorsHandlers to map endpoints |
| DAPR1501 | Dapr.Jobs | Job handler not configured |

---

## Conclusion

Dapr's design philosophy emphasizes:

- **Application-centric**: Focus on developer productivity, not infrastructure
- **Sidecar architecture**: Zero-code changes for distributed systems capabilities
- **Portability**: Swap infrastructure without code changes
- **Building block abstraction**: Codified best practices for common patterns
- **Language agnosticism**: Any language, any framework, anywhere

Key innovations include:

- **Virtual actors with automated placement**: Durable, stateful objects without infrastructure management
- **Replay-based workflows**: Linear orchestration code with automatic durability
- **Sidecar-as-scheduler**: Workflow engine that offloads execution to worker applications
- **Component-based portability**: Same API across vastly different infrastructure
- **Resiliency as configuration**: Timeouts, retries, circuit breakers without code
- **Placement service with Raft**: Consistent actor distribution across cluster

This combination of algorithms and patterns makes Dapr suitable for:
- **Microservices applications**: Service invocation, state, pub/sub out of the box
- **Event-driven architectures**: Pub/sub with at-least-once delivery
- **Stateful workflows**: Durable, long-running processes
- **IoT and edge**: Lightweight sidecar running on resource-constrained devices
- **Legacy modernization**: Add distributed systems capabilities without rewriting
- **Multi-cloud deployments**: Same application code across AWS, Azure, GCP, on-prem

---

*Document Version: 1.0*
*Based on Dapr official documentation, technical blogs, and source code analysis*