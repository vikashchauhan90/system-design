# Akka.NET: Complete Actor Model & Distributed Systems Reference

## Document Overview

This document provides a comprehensive analysis of Akka.NET's architectural patterns, concurrency models, and distributed systems algorithms. Akka.NET is a toolkit and runtime for building highly concurrent, distributed, and resilient message-driven applications on .NET. It implements the **Actor Model**, which provides a higher level of abstraction for writing concurrent and distributed systems by treating everything as actors that communicate via asynchronous message passing . This document covers the core actor system architecture, persistence mechanisms, cluster management, stream processing, and advanced coordination patterns.

---

## Table of Contents

1. [Core Actor Model Architecture](#core-actor-model-architecture)
2. [Actor Lifecycle & Supervision](#actor-lifecycle--supervision)
3. [Akka.Persistence (Event Sourcing)](#akkapersistence-event-sourcing)
4. [Cluster Management](#cluster-management)
5. [Cluster Sharding](#cluster-sharding)
6. [Akka.Streams Implementation](#akkastreams-implementation)
7. [Other Cluster Tools](#other-cluster-tools)
8. [Serialization & Message Contracts](#serialization--message-contracts)
9. [Complete System Interaction Diagram](#complete-system-interaction-diagram)

---

## Core Actor Model Architecture

### 1. Actor Model Fundamentals

**Purpose**: Provide a high-level abstraction for concurrent and distributed computation without low-level threading primitives.

**Core Principles**:

| Principle | Description |
|-----------|-------------|
| **Message-Driven** | All communication is via asynchronous messages |
| **Isolation** | Actors do not share mutable state |
| **Location Transparency** | Actors can be local or remote, code is the same |
| **Fail-fast** | Let actors fail, don't try to fix corrupted state |

**The Actor Hierarchy**:

```
ActorSystem (root)
    │
    ├── /user (user guardian - parent of all user actors)
    │       │
    │       ├── /greeter (user-created actor)
    │       │       └── child actors...
    │       └── /orderProcessor
    │
    ├── /system (system guardian - internal actors)
    └── /deadLetters (dead letter office - undeliverable messages)
```

**Basic Actor Definition**:
```csharp
// Create an immutable message type
public class Greet
{
    public Greet(string who) { Who = who; }
    public string Who { get; }
}

// Define actor behavior using ReceiveActor 
public class GreetingActor : ReceiveActor
{
    public GreetingActor()
    {
        Receive<Greet>(greet => 
            Console.WriteLine("Hello {0}", greet.Who));
    }
}

// Usage
var system = ActorSystem.Create("MySystem");
var greeter = system.ActorOf(Props.Create<GreetingActor>(), "greeter");
greeter.Tell(new Greet("Akka.NET"));
```

### 2. Actor References (ActorRef)

**Purpose**: Provide a location-transparent proxy to an actor instance.

**Characteristics**:

| Property | Description |
|----------|-------------|
| **Immutable** | Can be safely shared across threads |
| **Serializable** | Can be sent in messages across network |
| **Location Transparent** | Same API for local and remote actors |

**Actor Path Structure**:
```
akka://MySystem/user/greeter/child1#-1234567890
  │       │     │      │          │
  │       │     │      │          └─ UID (unique across restarts)
  │       │     │      └─ Actor Name
  │       │     └─ Guardian
  │       └─ ActorSystem Name
  └─ Protocol (akka.tcp for remoting)
```

**Why ActorRef not direct reference**:
- Allows actor to be restarted (old instance replaced)
- Enables remote actors (network transparency)
- Provides location transparency (actor can move in cluster)
- Prevents direct method calls (enforces message passing)

### 3. ReceiveActor Pattern

**Purpose**: Type-safe message handling using partial functions.

**Basic Pattern**:
```csharp
public class MyActor : ReceiveActor
{
    public MyActor()
    {
        // Message handlers
        Receive<string>(message => Console.WriteLine("Got string: " + message));
        Receive<int>(i => Console.WriteLine("Got integer: " + i));
        Receive<MyMessage>(msg => HandleMyMessage(msg));
        
        // Predicate-based receive
        Receive<int>(i => i > 0, i => Console.WriteLine("Positive: " + i));
        
        // Async handlers
        ReceiveAsync<MyCommand>(async cmd => await HandleCommand(cmd));
    }
    
    private void HandleMyMessage(MyMessage msg) { ... }
    private async Task HandleCommand(MyCommand cmd) { ... }
}
```

**Handler Execution Order**: First matching handler executes (evaluation order as registered).

---

## Actor Lifecycle & Supervision

### 4. Actor Lifecycle

**Purpose**: Manage actor creation, restart, and termination.

**Lifecycle Hooks**:

| Hook | Called When | Typical Use |
|------|-------------|--------------|
| `PreStart()` | After actor created, before processing messages | Initialize resources |
| `PostStop()` | After actor stopped, no more messages processed | Cleanup resources |
| `PreRestart()` | Before restart (after failure) | Cleanup before restart |
| `PostRestart()` | After restart (after `PreRestart`) | Re-initialize after recovery |

**Actor Creation**:
```csharp
// Using Props factory
var actor = system.ActorOf(Props.Create<MyActor>(), "myActor");
var actorWithArgs = system.ActorOf(Props.Create(() => new MyActor("arg")), "named");

// Dependency injection via Props
var props = Props.Create(() => new DependentActor(dependency));
var actor = system.ActorOf(props, "dependentActor");
```

### 5. Supervision Strategies

**Purpose**: Define how parent actors respond to child actor failures.

**Supervision Directives**:

| Directive | Behavior |
|-----------|----------|
| `Resume` | Resume child, keep its internal state |
| `Restart` | Stop child, create new instance (discards state) |
| `Stop` | Permanently stop child |
| `Escalate` | Escalate failure to parent |

**One-For-One Strategy** (default):
```csharp
public class SupervisingActor : ReceiveActor
{
    public SupervisingActor()
    {
        var child = Context.ActorOf(Props.Create<ChildActor>(), "child");
        
        // Configure supervision
        Context.Parent = Sender;
    }
    
    protected override SupervisorStrategy Strategy =>
        new OneForOneStrategy(
            maxNrOfRetries: 10,
            withinTimeRange: TimeSpan.FromMinutes(1),
            localOnlyDecider: ex =>
            {
                if (ex is ArithmeticException)
                    return Directive.Resume;
                if (ex is NullReferenceException)
                    return Directive.Restart;
                return Directive.Stop;
            });
}
```

**One-For-All Strategy**:
- When one child fails, all children are restarted/stopped
- Used when children have strong dependencies

---

## Akka.Persistence (Event Sourcing)

### 6. Event Sourcing Core

**Purpose**: Persist state changes as events rather than mutable state, enabling recovery and auditability .

**Architecture Overview**:
```
Command → PersistentActor → Event → Journal (append-only)
                ↓
            Update State
                ↓
           (may also publish event bus)
```

**Key Components** :

| Component | Purpose |
|-----------|---------|
| `PersistentActor` | Stateful actor that persists events |
| `Journal` | Append-only storage of events |
| `Snapshot Store` | Periodic state snapshots for faster recovery |
| `AtLeastOnceDelivery` | Reliable message delivery with retries |
| `PersistentView` (deprecated) | Read-side projection (use PersistenceQuery) |

**Basic PersistentActor**:
```csharp
public class OrderActor : ReceivePersistentActor
{
    public override string PersistenceId => "order-" + OrderId;
    
    private OrderState _state = new OrderState();
    
    public OrderActor(string orderId)
    {
        OrderId = orderId;
        
        // Command handlers (validate, then persist)
        Command<CreateOrder>(cmd => 
        {
            var evt = new OrderCreated(cmd.OrderId, cmd.Items);
            Persist(evt, ApplyEvent);
        });
        
        Command<AddItem>(cmd =>
        {
            if (_state.IsOpen)
            {
                var evt = new ItemAdded(cmd.OrderId, cmd.Item);
                Persist(evt, ApplyEvent);
            }
            else Sender.Tell(new OrderNotOpen());
        });
    }
    
    // Recovery handlers (replay events)
    protected override bool ReceiveRecover(object message)
    {
        switch (message)
        {
            case OrderCreated evt: ApplyEvent(evt); return true;
            case ItemAdded evt: ApplyEvent(evt); return true;
            case SnapshotOffer offer: 
                _state = (OrderState)offer.Snapshot; 
                return true;
            default: return false;
        }
    }
    
    private void ApplyEvent(object evt)
    {
        _state = _state.Update(evt);
    }
}
```

### 7. Persistence Guarantees

**Key Properties** :

| Property | Description |
|----------|-------------|
| **Append-Only** | Events never mutated, only appended |
| **At-Least-Once Delivery** | Guaranteed delivery to journal (idempotent handling required) |
| **Recovery via Replay** | State rebuilt by replaying events in order |
| **Snapshot Optimization** | Snapshots reduce replay time |

**Stashing During Persist**:
During `Persist()` call, incoming messages are **stashed** (queued) until event handler completes. This ensures event ordering and prevents command interleaving . If persistence fails, `OnPersistFailure` is invoked and actor stops.

### 8. Serialization of ActorRefs in Events

**Critical Consideration**:
When persisting messages containing `IActorRef`, the serializer stores the actor's **path** (e.g., `akka://system/user/$b`), not the actor's identity . Upon replay, this path is resolved to whatever actor currently exists at that path, leading to potentially incorrect references .

**Recommendation**: Store business identifiers instead of `IActorRef` in events, then resolve via `Context.ActorSelection(path)` or lookup mechanisms.

### 9. Snapshots

**Purpose**: Optimize recovery time by storing full state periodically.

**Configuration**:
```hocon
akka.persistence {
  snapshot-store {
    plugin = "akka.persistence.snapshot-store.sql-server"
    sql-server {
      connection-string = "..."
      table-name = Snapshot
    }
  }
}
```

**Usage**:
```csharp
public class MyActor : ReceivePersistentActor
{
    private int _eventCount = 0;
    
    private void AfterEventPersisted(object evt)
    {
        _eventCount++;
        if (_eventCount % 1000 == 0)
        {
            SaveSnapshot(CreateSnapshot());
        }
    }
    
    protected override bool ReceiveRecover(object message)
    {
        switch (message)
        {
            case SnapshotOffer offer:
                RestoreFromSnapshot(offer.Snapshot);
                return true;
            // ... replay events after snapshot
        }
    }
}
```

### 10. AtLeastOnceDelivery

**Purpose**: Guarantee message delivery even across sender/receiver restarts .

**Components**:
- Extend `AtLeastOnceDeliveryReceiveActor` (or `AtLeastOnceDeliveryReceivePersistentActor`)
- Use `Deliver()` to send with delivery semantics
- Track unconfirmed messages for redelivery

**Constraints**: Does not guarantee exactly-once delivery (recipient must handle duplicates idempotently).

---

## Cluster Management

### 11. Gossip Protocol & Failure Detection

**Purpose**: Decentralized cluster membership and failure detection .

**How It Works**:
- Nodes exchange gossip about cluster state periodically
- Each gossip contains node heartbeats and cluster membership
- Information spreads exponentially (O(log N) convergence)

**Membership States** :

| State | Description |
|-------|-------------|
| `Joining` | Node attempting to join cluster |
| `Up` | Node fully operational in cluster |
| `Leaving` | Node gracefully leaving cluster |
| `Exiting` | Node transitioning out (after leaving) |
| `Down` | Node marked as down (failed) |
| `Removed` | Node fully removed from cluster |

**Phi Accrual Failure Detection**:
- Adaptive threshold based on heartbeat inter-arrival times
- φ value represents suspicion level (φ=1 = 10% failure probability) 
- No manual timeout tuning needed

### 12. Cluster Formation

**Seed Nodes**:
Nodes designated for initial cluster discovery . Not special beyond initial contact - regular cluster nodes after joining.

**Configuration**:
```hocon
akka.cluster {
  seed-nodes = [
    "akka.tcp://ClusterSystem@host1:2552",
    "akka.tcp://ClusterSystem@host2:2552"
  ]
  # Minimum number of members before cluster usable
  min-nr-of-members = 3
  
  # Failure detection
  failure-detector {
    threshold = 8.0
    acceptable-heartbeat-pause = 3s
  }
}
```

**Auto-Downing**: Not recommended for production - leads to split-brain. Use Split Brain Resolver (SBR) or manual downing instead .

### 13. Cluster Singleton

**Purpose**: Ensure exactly one instance of an actor exists across the cluster .

**Characteristics**:
- One singleton actor active at any time
- Automatically migrates when node fails
- Old instance stopped before new instance starts

**Configuration**:
```csharp
var singleton = system.ActorOf(
    ClusterSingletonManager.Props(
        singletonProps: Props.Create<MySingleton>(),
        terminationMessage: PoisonPill.Instance,
        settings: ClusterSingletonSettings.Create(system)),
    "mySingleton");

// Proxy for clients to find singleton
var proxy = system.ActorOf(
    ClusterSingletonProxy.Props(
        singletonPath: "/user/mySingleton",
        settings: ClusterSingletonProxySettings.Create(system)),
    "mySingletonProxy");
```

**Lease Requirement**: Starting with Akka.NET 1.5, cluster singleton relies on a **distributed lease** (e.g., via Kubernetes API or ZooKeeper) to prevent split-brain singletons .

---

## Cluster Sharding

### 14. Sharding Architecture

**Purpose**: Distribute many actors (entities) across cluster nodes with guaranteed at-most-one instance per entity .

**Core Components**:

| Component | Purpose |
|-----------|---------|
| **ShardRegion** | Entry point for messages to sharded entities |
| **Shard** | Parent actor for entities in a shard |
| **Entity** | Individual actor (e.g., per customer, per order) |
| **ShardCoordinator** | Manages shard-to-node mapping (runs as singleton) |

**Shard Calculation**:
```
EntityId → ShardId = Hash(EntityId) % NumberOfShards
ShardId → Node = ShardCoordinator lookup
```

### 15. Message Extractor

**Purpose**: Extract entity ID and shard ID from messages .

```csharp
public sealed class OrderShardMsgRouter : HashCodeMessageExtractor
{
    public const int DefaultShardCount = 100;
    
    public OrderShardMsgRouter() : base(DefaultShardCount) { }
    
    // Extract which entity should receive this message
    public override string EntityId(object message)
    {
        switch (message)
        {
            case IWithOrderId orderMsg:
                return orderMsg.OrderId;
            case ConfirmableMessageEnvelope<IWithOrderId> envelope:
                return envelope.Message.OrderId;
            default:
                return null;
        }
    }
}
```

**How Sharding Routes Messages** :
1. Message arrives at `ShardRegion`
2. `IMessageExtractor.EntityId()` determines target entity
3. `IMessageExtractor.ShardId(entityId)` determines target shard
4. If entity not already hosted on this node, `ShardRegion` forwards message to the `ShardCoordinator`
5. `ShardCoordinator` maps shard to owning node
6. Message forwarded to correct node, entity created on-demand if needed

### 16. Sharding Guarantees

| Guarantee | Description |
|-----------|-------------|
| **At-Most-Once Entity** | Maximum one instance of each entity exists |
| **Message Buffering** | Messages buffered during entity migration |
| **Even Distribution** | Shards distributed across cluster nodes |
| **Dynamic Rebalancing** | Shards moved when nodes added/removed |

**Entity Passivation**:
Actors should be passivated (stopped) when idle to free resources:
```csharp
Context.SetReceiveTimeout(TimeSpan.FromMinutes(5)); // Auto-passivate
// Or explicit passivation:
Context.Parent.Tell(Passivate.Stop.Instance);
```

---

## Akka.Streams Implementation

### 17. Reactive Streams Implementation

**Purpose**: Asynchronous stream processing with automatic backpressure .

**Core Abstractions**:

| Abstraction | Description | Parameterization |
|-------------|-------------|------------------|
| `Source<TOut, TMat>` | Produces elements | `TOut`=element type, `TMat`=materialized value |
| `Flow<TIn, TOut, TMat>` | Transforms elements | `TIn`→`TOut` |
| `Sink<TIn, TMat>` | Consumes elements | `TIn` |
| `RunnableGraph<TMat>` | Executable stream blueprint | materialized value |

**Materialization Process**:
The stream blueprint (graph) is "materialized" into running actors when executed :

```csharp
var source = Source.From(Enumerable.Range(1, 100));
var flow = Flow.Create<int>().Select(x => x * 2);
var sink = Sink.ForEach<int>(Console.WriteLine);

var runnable = source.Via(flow).To(sink);
var materializer = system.Materializer();
var task = runnable.Run(materializer); // Materialization happens here
```

### 18. Backpressure Protocol

**Purpose**: Prevent fast producers from overwhelming slow consumers .

**Push/Pull Mechanics**:
- Downstream must call `Pull()` before upstream can `Push()`
- Demand propagates upstream when consumers are ready
- Bounded buffers prevent unbounded memory growth

**GraphInterpreter Execution**:
```
State per Connection:
- Empty: No element, no demand
- PushPending: Element ready, not yet pulled
- PullPending: Demand registered, awaiting element
- Closed: Connection terminated

The interpreter processes one stage at a time until no more progress possible .
```

### 19. Fusing Optimization

**Purpose**: Combine adjacent stream stages into same actor to reduce overhead .

**How Fusing Works** :
- Without fusing: Each stage runs in its own actor → message per element
- With fusing: Adjacent stages share actor → synchronous calls between stages
- Fusion happens automatically during materialization

**Controlling Fusing**:
```csharp
// Force async boundary (prevents fusing)
source.Select(x => HeavyCompute(x))
      .Async()  // Creates boundary
      .Select(x => x * 2)
      .To(sink);

// With custom attributes
source.AddAttributes(ActorAttributes.CreateDispatcher("my-dispatcher"));
```

**Trade-offs** :

| Fused | Not Fused |
|-------|-----------|
| Lower latency (no message passing) | Higher latency |
| Better throughput | More parallelism possible |
| Single thread for all stages | Stages can run concurrently |
| One slow stage blocks all | Independent execution |

### 20. GraphStage Custom Operator

**Purpose**: Implement custom processing stages with complete control over backpressure and lifecycle .

```csharp
public class SelectStage<TIn, TOut> : GraphStage<FlowShape<TIn, TOut>>
{
    private readonly Func<TIn, TOut> _mapper;
    
    public SelectStage(Func<TIn, TOut> mapper)
    {
        _mapper = mapper;
        Shape = new FlowShape<TIn, TOut>(
            Inlet<TIn>.Create("Input"),
            Outlet<TOut>.Create("Output"));
    }
    
    protected override GraphStageLogic CreateLogic(Attributes inheritedAttributes)
    {
        return new Logic(this);
    }
    
    private class Logic : InAndOutGraphStageLogic
    {
        private readonly SelectStage<TIn, TOut> _stage;
        
        public Logic(SelectStage<TIn, TOut> stage) : base(stage.Shape)
        {
            _stage = stage;
            
            // Register handlers
            SetHandler(stage.Shape.Inlet, onPush: () =>
            {
                var element = Grab(stage.Shape.Inlet);
                var result = _stage._mapper(element);
                Push(stage.Shape.Outlet, result);
            });
            
            SetHandler(stage.Shape.Outlet, onPull: () =>
            {
                Pull(stage.Shape.Inlet);
            });
        }
    }
}
```

---

## Other Cluster Tools

### 21. Distributed Publish-Subscribe

**Purpose**: Publish messages to all nodes in cluster (or actors subscribed to specific topics) .

```csharp
// Subscribe to topic
var mediator = DistributedPubSub.Get(system).Mediator;
mediator.Tell(new Subscribe("orders", Context.Self));

// Publish to all subscribers
mediator.Tell(new Publish("orders", new OrderCreated(orderId)));

// Send to only one subscriber (efficient routing)
mediator.Tell(new SendToAll("/user/processor", new ProcessOrder(orderId)));
```

**Delivery Guarantee**: At-most-once delivery .

### 22. Cluster Client

**Purpose**: Allow external (non-cluster) systems to communicate with a cluster .

**Architecture**:
```
External Client → Cluster Client (lightweight) → Receptionist (inside cluster)
                                                          ↓
                                                 Any cluster node
```

**Use Case**: Legacy systems, external services, mobile apps that can't join the cluster.

### 23. Distributed Data (CRDTs)

**Purpose**: Conflict-Free Replicated Data Types for eventual consistency across cluster .

**Supported CRDTs**:
| Type | Description |
|------|-------------|
| `GCounter` | Grow-only counter |
| `PNCounter` | Positive/Negative counter (increment/decrement) |
| `GSet` | Grow-only set |
| `ORSet` | Observed-Remove set (add/remove) |
| `LWWRegister` | Last-write-wins register |
| `ORMap` | Map with ORSet keys |

**Use Case**: Cluster-wide configuration, counters, presence tracking where strong consistency not required.

---

## Serialization & Message Contracts

### 24. Message Immutability

**Principle**: All messages should be immutable to ensure safe sharing across actors and threads.

```csharp
// Good - immutable
public sealed class OrderCreated
{
    public OrderCreated(string orderId, decimal amount)
    {
        OrderId = orderId;
        Amount = amount;
    }
    
    public string OrderId { get; }
    public decimal Amount { get; }
}

// Bad - mutable (data corruption possible)
public class OrderCreated
{
    public string OrderId { get; set; }  // Mutable!
    public decimal Amount { get; set; }
}
```

### 25. Serialization Options

| Serializer | Characteristics | Use Case |
|------------|-----------------|----------|
| **Hyperion** (default 1.5+) | High performance, handles object graphs | General purpose |
| **Newtonsoft.Json** (legacy) | Widely compatible, slower | Legacy migration |
| **Protobuf** | Version tolerant, compact | Cross-platform |
| **Custom** | Full control | Special formats |

**ActorRef Serialization Caution**: `IActorRef` serializes to path string; replay may resolve to wrong actor .

---

## Complete System Interaction Diagram

```
┌─────────────────────────────────────────────────────────────────────────────┐
│                          EXTERNAL CLIENTS                                    │
│  ┌──────────────┐  ┌──────────────┐  ┌──────────────┐                       │
│  │  HTTP API    │  │  TCP Socket  │  │  Message     │                       │
│  │  Gateway     │  │  Connection  │  │  Queue       │                       │
│  └──────┬───────┘  └──────┬───────┘  └──────┬───────┘                       │
│         │                 │                 │                               │
│         └─────────────────┼─────────────────┘                               │
│                           │ (External clients use ClusterClient)            │
└───────────────────────────┼─────────────────────────────────────────────────┘
                            │
                            ▼
┌─────────────────────────────────────────────────────────────────────────────┐
│                           AKKA.NET CLUSTER                                   │
│                                                                              │
│  ┌─────────────────────────────────────────────────────────────────────┐    │
│  │                         Node A                                        │    │
│  │  ┌─────────────────────────────────────────────────────────────┐    │    │
│  │  │                    ActorSystem                                │    │    │
│  │  │  ┌─────────┐  ┌─────────┐  ┌─────────┐  ┌─────────┐         │    │    │
│  │  │  │ Cluster │  │ Cluster │  │ Shard   │  │ Dist    │         │    │    │
│  │  │  │ Daemon  │  │ Client  │  │ Region  │  │ PubSub  │         │    │    │
│  │  │  │         │  │Reception│  │         │  │ Mediator│         │    │    │
│  │  │  └─────────┘  └─────────┘  └────┬────┘  └─────────┘         │    │    │
│  │  │                                 │                             │    │    │
│  │  │  ┌─────────┐  ┌─────────┐  ┌────┴────┐  ┌─────────┐         │    │    │
│  │  │  │Singl-   │  │Persistent│  │OrderBook│  │Snapshot │         │    │    │
│  │  │  │eton     │  │ Actor   │  │ Shard   │  │ Store   │         │    │    │
│  │  │  │Manager  │  │(Event    │  │(contains│  │(Local   │         │    │    │
│  │  │  │         │  │Sourced)  │  │ entities│  │  FS)    │         │    │    │
│  │  │  └─────────┘  └────┬─────┘  └────┬────┘  └─────────┘         │    │    │
│  │  │                    │            │                             │    │    │
│  │  │              Journal (SQLite)   │                             │    │    │
│  │  └─────────────────────────────────────────────────────────────┘    │    │
│  └─────────────────────────────────────────────────────────────────────┘    │
│                                                                              │
│  ┌─────────────────────────────────────────────────────────────────────┐    │
│  │                         Node B                                        │    │
│  │  (Similar structure - shares cluster state via Gossip)               │    │
│  └─────────────────────────────────────────────────────────────────────┘    │
│                                                                              │
│  ┌─────────────────────────────────────────────────────────────────────┐    │
│  │                         Node C                                        │    │
│  │  (Similar structure)                                                 │    │
│  └─────────────────────────────────────────────────────────────────────┘    │
│                                                                              │
│  Gossip Protocol: Node A ←→ Node B ←→ Node C (eventual convergence)        │
│  Failure Detection: Phi Accrual (adaptive thresholds)           │
│                                                                              │
└─────────────────────────────────────────────────────────────────────────────┘
                            │
                            ▼
┌─────────────────────────────────────────────────────────────────────────────┐
│                         PERSISTENCE LAYER                                    │
│                                                                              │
│  ┌─────────────────────────────────────────────────────────────────────┐    │
│  │                    Event Journal (Append-Only)                       │    │
│  │  ┌─────────────────────────────────────────────────────────────┐    │    │
│  │  │ PersistenceId │ SequenceNr │        Event Payload           │    │    │
│  │  ├─────────────────────────────────────────────────────────────┤    │    │
│  │  │ "order-123"   │     1      │ OrderCreated { orderId:123 }   │    │    │
│  │  │ "order-123"   │     2      │ ItemAdded { sku:"A123" }       │    │    │
│  │  │ "order-123"   │     3      │ ItemAdded { sku:"B456" }       │    │    │
│  │  │ "order-456"   │     1      │ OrderCreated { orderId:456 }   │    │    │
│  │  └─────────────────────────────────────────────────────────────┘    │    │
│  └─────────────────────────────────────────────────────────────────────┘    │
│                                                                              │
│  ┌─────────────────────────────────────────────────────────────────────┐    │
│  │                    Snapshot Store (Periodic State)                   │    │
│  │  ┌─────────────────────────────────────────────────────────────┐    │    │
│  │  │ PersistenceId │ SequenceNr │        State Snapshot           │    │    │
│  │  ├─────────────────────────────────────────────────────────────┤    │    │
│  │  │ "order-123"   │    100     │ OrderState {items: List...}    │    │    │
│  │  │ "order-456"   │    50      │ OrderState {items: List...}    │    │    │
│  │  └─────────────────────────────────────────────────────────────┘    │    │
│  └─────────────────────────────────────────────────────────────────────┘    │
│                                                                              │
└─────────────────────────────────────────────────────────────────────────────┘
                            │
                            ▼
┌─────────────────────────────────────────────────────────────────────────────┐
│                       STREAM PROCESSING (Akka.Streams)                       │
│                                                                              │
│  ┌─────────────────────────────────────────────────────────────────────┐    │
│  │                    Stream Materialization                            │    │
│  │                                                                       │    │
│  │  Blueprint (IModule tree) → Fusing (FusedModule) → Actor Creation    │    │
│  │                                                                       │    │
│  │  Source<int> → Flow<int,string> → Sink<string>                      │    │
│  │       │              │                │                              │    │
│  │       ▼              ▼                ▼                              │    │
│  │  [Fused Group A] → [Fused Group B]  [Fused Group C]                 │    │
│  │  (Async boundary if Async() called)                                 │    │
│  └─────────────────────────────────────────────────────────────────────┘    │
│                                                                              │
│  Backpressure Protocol: Demand propagated upstream, bounded buffers         │
│                                                                              │
└─────────────────────────────────────────────────────────────────────────────┘
```

---

## Algorithm & Pattern Summary Table

| # | Algorithm/Pattern | Primary Purpose | Akka.NET Component |
|---|------------------|-----------------|---------------------|
| 1 | Actor Model | Concurrent computation without shared state | ActorSystem, ActorRef |
| 2 | ReceiveActor | Type-safe message handling | `ReceiveActor` |
| 3 | Supervision Hierarchy | Fault tolerance via parent monitoring | `SupervisorStrategy` |
| 4 | One-For-One Strategy | Independent child restart | `OneForOneStrategy` |
| 5 | Event Sourcing | State persistence via immutable events | `PersistentActor` |
| 6 | Append-Only Journal | Durable event storage | Journal plugin |
| 7 | Snapshots | Recovery optimization | SnapshotStore |
| 8 | AtLeastOnceDelivery | Reliable cross-actor messaging | `AtLeastOnceDeliveryActor` |
| 9 | Gossip Protocol | Decentralized cluster membership | Cluster.Gossip |
| 10 | Phi Accrual Failure Detection | Adaptive failure detection | Cluster.FailureDetector |
| 11 | Seed Nodes | Cluster bootstrap mechanism | `akka.cluster.seed-nodes` |
| 12 | Cluster Singleton | Single active instance across cluster | `ClusterSingletonManager` |
| 13 | Cluster Sharding | Distributed entity management | `ClusterSharding` |
| 14 | Consistent Hashing (Sharding) | Entity-to-shard mapping | `HashCodeMessageExtractor` |
| 15 | Shard Rebalancing | Dynamic load distribution | `ShardCoordinator` |
| 16 | Reactive Streams | Async streams with backpressure | Akka.Streams |
| 17 | Fusing Optimization | Reduce actor overhead in streams | `Fusing.Aggressive` |
| 18 | Backpressure Protocol | Flow control (Push/Pull) | `GraphInterpreter` |
| 19 | Materialization | Blueprint → running stream | `ActorMaterializer` |
| 20 | Distributed Pub/Sub | Cluster-wide message distribution | `DistributedPubSub` |
| 21 | CRDTs (Distributed Data) | Conflict-free eventual consistency | `DistributedData` |
| 22 | Cluster Client | External system access | `ClusterClient` |
| 23 | Location Transparency | Local/remote actor transparency | `ActorRef`, `ActorPath` |

---

## Configuration Reference

### Core Configuration (HOCON)
```hocon
akka {
  actor {
    provider = "Akka.Cluster.ClusterActorRefProvider, Akka.Cluster"
    default-dispatcher {
      type = "Akka.Dispatch.MessageDispatcherConfigurator"
      throughput = 5
      throughput-deadline-time = 0ms
    }
    serializer = hyperion
  }
  
  remote {
    dot-netty.tcp {
      port = 2552
      hostname = "0.0.0.0"
      public-hostname = "my-host.domain.com"
    }
  }
  
  cluster {
    seed-nodes = [
      "akka.tcp://MyCluster@node1:2552",
      "akka.tcp://MyCluster@node2:2552"
    ]
    min-nr-of-members = 3
    roles = ["frontend", "backend"]
    
    failure-detector {
      threshold = 8.0
      acceptable-heartbeat-pause = 3s
    }
  }
  
  persistence {
    journal {
      plugin = "akka.persistence.journal.sql-server"
    }
    snapshot-store {
      plugin = "akka.persistence.snapshot-store.sql-server"
    }
  }
}
```

### Persistence with SQL Server
```hocon
akka.persistence.journal.sql-server {
  class = "Akka.Persistence.SqlServer.Journal.SqlServerJournal, Akka.Persistence.SqlServer"
  connection-string = "Server=localhost;Database=AkkaJournal;Trusted_Connection=true"
  schema-name = dbo
  table-name = Journal
  auto-initialize = on
}
```

### Cluster Sharding Settings
```hocon
akka.cluster.sharding {
  # Number of shards per region (across cluster)
  number-of-shards = 100
  
  # Passivation settings
  passivate-idle-entity-after = 5m
  
  # Rebalance threshold
  rebalance-threshold = 10
  
  # Lease settings for coordinator singleton
  lease {
    type = "lease"
    lease-implementation = "akka.coordination.lease.kubernetes"
  }
}
```

### Stream Materializer Settings
```csharp
var settings = ActorMaterializerSettings.Create(system)
    .WithInputBuffer(initialSize: 16, maxSize: 32)
    .WithDispatcher("akka.actor.default-dispatcher")
    .WithSupervisionStrategy(Deciders.ResumingDecider);

var materializer = system.Materializer(settings);
```

---

## Performance & Complexity Reference

| Operation | Complexity | Notes |
|-----------|------------|-------|
| Message send (local) | O(1) | ~100-200 ns overhead |
| Message send (remote) | Network + serialization | ~1-5ms typical |
| Actor creation | O(1) | ~1-10μs (depends on constructor) |
| PersistentActor persist | O(1) + I/O | Async, non-blocking |
| Recovery (replay) | O(N events) | N = events since last snapshot |
| Stream fusing | O(stages) | Linear pass during materialization |
| Stream element processing | O(1) per element | In-memory within fused group |
| Shard resolution | O(1) cache + O(1) network | First message may require coordinator lookup |
| Gossip propagation | O(log N) rounds | Each round ~1 second |

---

## Source Code Reference

| Component | Location (Akka.NET GitHub) |
|-----------|---------------------------|
| Core Actor | `src/core/Akka/Actor/` |
| Persistence | `src/contrib/persistence/Akka.Persistence/` |
| Cluster | `src/core/Akka.Cluster/` |
| Cluster.Sharding | `src/contrib/cluster/Akka.Cluster.Sharding/` |
| Streams | `src/core/Akka.Streams/` |
| Streams Implementation | `src/core/Akka.Streams/Implementation/Fusing/` |
| Distributed Data | `src/contrib/cluster/Akka.DistributedData/` |

---

## Key NuGet Packages

| Package | Purpose |
|---------|---------|
| `Akka` | Core actor library |
| `Akka.Remote` | Remote actor communication |
| `Akka.Cluster` | Cluster membership management |
| `Akka.Cluster.Tools` | Cluster singleton, pub/sub, client |
| `Akka.Cluster.Sharding` | Distributed entity sharding |
| `Akka.Persistence` | Event sourcing and snapshots |
| `Akka.Persistence.SqlServer` | SQL Server journal plugin |
| `Akka.Streams` | Reactive Streams implementation |
| `Akka.DistributedData` | CRDT-based eventual consistency |
| `Akka.Serialization.Hyperion` | Default high-performance serializer |

---

## Comparison to Other Frameworks

| Feature | Akka.NET | Orleans | Proto.Actor | Microsoft TPL Dataflow |
|---------|----------|---------|-------------|----------------------|
| Actor Model | Full | Virtual Actors | Full | No |
| Cluster Support | Yes | Yes (Azure) | Yes | No |
| Event Sourcing | Built-in | Via extensions | Built-in | No |
| Streams with Backpressure | Yes | Limited | Basic | Yes (blocks) |
| Supervision | Hierarchy | Parent implicit | Yes | N/A |
| At-Least-Once Delivery | Yes | No | Yes | No |
| .NET Integration | Full | Full (ASP.NET) | Full | Full |
| Learning Curve | Steep | Moderate | Moderate | Low |

---

## Conclusion

Akka.NET's design philosophy emphasizes:

- **Location transparency**: Same code works locally or across network
- **Let it crash**: Fail-fast and automatic supervision
- **Event sourcing**: Append-only persistence for auditability and recovery
- **Elastic scaling**: Cluster sharding distributes actors across nodes
- **Backpressure by design**: Reactive Streams for flow control

Key innovations include:
- **ReceiveActor pattern**: Type-safe message handling without `UntypedActor` 
- **Phi Accrual failure detection**: Adaptive, no magic timeouts 
- **PersistentActor**: Event sourcing integrated directly into actor lifecycle 
- **Cluster sharding**: Guaranteed at-most-one entity per identifier 
- **GraphStage fusing**: Optimize stream performance by reducing actor overhead 
- **Message extractor pattern**: Clean separation of routing logic from business logic 

This combination of algorithms and patterns makes Akka.NET suitable for:
- **Real-time systems**: Financial trading, IoT data processing
- **Microservices**: Actor-per-service with cluster deployment
- **Event-driven architectures**: Event sourcing and CQRS
- **Stream processing**: Backpressured pipelines with Akka.Streams
- **Distributed coordination**: Singleton, sharding, and distributed data across clusters

---

*Document Version: 1.0*
*Based on Akka.NET source code, official documentation, and community resources*