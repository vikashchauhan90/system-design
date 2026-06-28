# Saga Pattern

A simplified educational implementation of the **Saga Pattern** for coordinating long-running distributed business transactions.

## Overview

Traditional distributed transactions such as **Two-Phase Commit (2PC)** require every participant to commit or roll back as a single atomic unit. While this provides strong consistency, it introduces blocking, reduced scalability, and tight coupling between services.

The **Saga Pattern** takes a different approach.

Instead of one large distributed transaction, a business workflow is decomposed into a sequence of **local transactions**. Each service commits its own transaction independently and publishes the result.

If a later step fails, previously completed steps are undone using **compensating transactions**.

Rather than enforcing immediate consistency, Sagas provide **eventual consistency**, making them well suited for modern microservice architectures.

---

# What this sample demonstrates

This sample implements a simple Saga workflow where each step consists of:

* an execution action,
* a compensating action,
* success/failure handling,
* rollback in reverse order when necessary.

The goal is to illustrate the Saga execution model rather than provide a production-ready workflow engine.

---

# How the Saga Pattern Works

A Saga is composed of multiple independent business transactions.

For example:

```text
Create Order

↓

Reserve Inventory

↓

Charge Payment

↓

Arrange Shipping

↓

Send Confirmation
```

Each step commits independently.

If every step succeeds:

```text
Order Completed
```

If a later step fails:

```text
Undo Shipping

↓

Refund Payment

↓

Release Inventory

↓

Cancel Order
```

Compensation executes in **reverse order** of successful execution.

---

# Core Idea

Each Saga step contains two operations.

```text
Execute()

Compensate()
```

Execution performs the business action.

Compensation reverses that action if necessary.

For example:

| Execute            | Compensation       |
| ------------------ | ------------------ |
| Create Order       | Cancel Order       |
| Reserve Inventory  | Release Inventory  |
| Charge Credit Card | Refund Payment     |
| Book Hotel         | Cancel Reservation |
| Reserve Flight     | Cancel Ticket      |

---

# Example Usage

```csharp
var saga = new Saga();

saga.AddStep(
    "Create Order",
    () => true,
    () => true);

saga.AddStep(
    "Reserve Inventory",
    () => true,
    () => true);

saga.AddStep(
    "Charge Payment",
    () => false,
    () => true);

bool success = saga.Run();
```

Execution:

```text
Create Order

✓

Reserve Inventory

✓

Charge Payment

✗

↓

Compensating...

Release Inventory

Cancel Order
```

The workflow ends in a consistent state.

---

# Saga Execution Flow

Successful execution:

```text
Order

↓

Inventory

↓

Payment

↓

Shipping

↓

Email

↓

Completed
```

Failed execution:

```text
Order

↓

Inventory

↓

Payment

✗

↓

Release Inventory

↓

Cancel Order
```

---

# Two Types of Saga

There are two primary ways to coordinate a Saga.

## 1. Orchestration-Based Saga

A central component called the **Saga Orchestrator** controls the workflow.

The orchestrator:

* executes each step,
* tracks the current state,
* decides the next action,
* invokes compensation when necessary.

Architecture:

```text
              +----------------------+
              |  Saga Orchestrator   |
              +----------------------+
                    |      |      |
                    |      |      |
          +---------+      |      +---------+
          |                |                |
     Order Service   Inventory Service  Payment Service
          |                |                |
          +----------------+----------------+
```

Example flow:

```text
Orchestrator

↓

Create Order

↓

Reserve Inventory

↓

Charge Payment

↓

Ship Order
```

If payment fails:

```text
Orchestrator

↓

Refund

↓

Release Inventory

↓

Cancel Order
```

### Advantages

* Easy to understand.
* Centralized business logic.
* Easier debugging.
* State machine is explicit.
* Good visibility into workflow progress.

### Disadvantages

* Central coordinator can become complex.
* Potential single point of failure (unless replicated).
* Services depend on the orchestrator.

---

## 2. Choreography-Based Saga

There is **no central coordinator**.

Instead, each service reacts to events published by other services.

Each service:

* listens for events,
* performs local work,
* publishes a new event.

Architecture:

```text
Order Service
      |

OrderCreated

      ↓

Inventory Service

      |

InventoryReserved

      ↓

Payment Service

      |

PaymentSucceeded

      ↓

Shipping Service
```

Failure example:

```text
PaymentFailed

↓

Inventory Service

↓

ReleaseInventory

↓

Order Service

↓

CancelOrder
```

Each service knows only the events that concern it.

---

## Orchestration vs. Choreography

| Feature          | Orchestration        | Choreography       |
| ---------------- | -------------------- | ------------------ |
| Coordinator      | Central Orchestrator | None               |
| Workflow Logic   | Centralized          | Distributed        |
| Communication    | Commands             | Events             |
| State Management | Central              | Distributed        |
| Coupling         | Higher               | Lower              |
| Complexity       | Easier initially     | Can grow over time |
| Debugging        | Easier               | More difficult     |

---

# State Management

A Saga can also be classified by **how workflow state is stored**.

---

## Stateful Saga

The coordinator stores workflow state.

Example:

```text
SagaId: 42

Current Step:
Payment

Status:
Running

Completed:
✓ Order
✓ Inventory
```

Benefits:

* Easy recovery.
* Resume after crashes.
* Retry failed steps.
* Progress tracking.

Frameworks such as **MassTransit**, **NServiceBus**, **Temporal**, and **Dapr Workflow** commonly use this approach.

---

## Stateless Saga

No central workflow state exists.

Instead:

* each event contains enough information,
* services react independently,
* progress emerges naturally from event flow.

Example:

```text
OrderCreated

↓

InventoryReserved

↓

PaymentSucceeded

↓

ShipmentCreated
```

No coordinator stores:

```text
Current Step
```

Instead, services derive state from received events.

Benefits:

* Highly scalable.
* Loosely coupled.
* Easier horizontal scaling.

Trade-offs:

* Harder debugging.
* Event correlation required.
* More difficult replay.

---

# Components

### Saga

Coordinates the workflow.

Depending on the implementation, it may be:

* an orchestrator,
* or simply a collection of event handlers.

---

### Saga Step

Represents one local transaction.

Each step contains:

* Execute
* Compensate

---

### Local Transaction

A transaction executed entirely within one service.

Example:

```text
Insert Order

Commit
```

No distributed lock is held.

---

### Compensation

Reverses the effect of a completed transaction.

Examples:

```text
Reserve Inventory

↓

Release Inventory
```

```text
Charge Credit Card

↓

Refund Payment
```

---

# Time Complexity

For **N** steps:

| Operation        |      Complexity |
| ---------------- | --------------: |
| Execute Workflow |            O(N) |
| Compensation     |            O(N) |
| Storage          | O(N) (stateful) |

---

# Advantages

* No distributed locking.
* High scalability.
* Works well across microservices.
* Better fault tolerance than 2PC.
* Supports long-running workflows.
* Independent local transactions.
* Eventual consistency.
* Flexible recovery through compensation.

---

# Limitations

Saga is **not** an ACID transaction.

Instead, it relies on compensation.

Challenges include:

* Designing correct compensating actions.
* Handling compensation failures.
* Managing duplicate events.
* Ensuring idempotency.
* Correlating distributed events.
* More complex testing.

Compensation itself may fail, requiring retries or manual intervention.

---

# Saga vs. Two-Phase Commit

| Feature                | Saga      | Two-Phase Commit |
| ---------------------- | --------- | ---------------- |
| Distributed Locking    | No        | Yes              |
| Atomicity              | Eventual  | Strong           |
| Consistency            | Eventual  | Immediate        |
| Long-running Workflows | Excellent | Poor             |
| Scalability            | High      | Moderate         |
| Blocking               | No        | Yes              |
| Compensation           | Required  | Not Required     |

---

# Typical Use Cases

Saga is widely used for:

* E-commerce order processing
* Payment workflows
* Travel booking systems
* Hotel reservations
* Airline ticketing
* Inventory management
* Subscription management
* Loan approval workflows
* Healthcare workflows
* Insurance claims
* Supply chain management

---

# When to Use Saga

Use Saga when:

* Business workflows span multiple services.
* Transactions are long-running.
* Eventual consistency is acceptable.
* Services own independent databases.
* High availability is important.
* Distributed locks should be avoided.

---

# When Not to Use Saga

Avoid Saga when:

* Strong ACID guarantees are mandatory.
* All updates occur within a single database.
* Compensation is impossible.
* Immediate consistency is required.
* Financial regulations require atomic commits.

In those cases, consider:

* Local database transactions
* Two-Phase Commit (2PC)
* Three-Phase Commit (3PC)
* Consensus protocols such as Raft

---

# Summary

The Saga Pattern is a distributed transaction coordination pattern that replaces one large atomic transaction with a sequence of independent local transactions and compensating actions. It enables highly scalable, loosely coupled microservice architectures by embracing **eventual consistency** instead of distributed locking.

Sagas can be implemented using **Orchestration**, where a central coordinator drives the workflow through an explicit state machine, or **Choreography**, where services react to domain events without a central controller. They can also be **stateful**, with persistent workflow state managed by the orchestrator, or **stateless**, where progress is inferred from exchanged events. Understanding these variations is essential when designing resilient, long-running business processes in distributed systems.
