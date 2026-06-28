# Transaction Outbox Pattern

A simplified educational implementation of the **Transaction Outbox Pattern** for reliable event publication in distributed systems.

## Overview

In an event-driven architecture, a service often needs to perform two operations as part of a single business action:

1. Update its own database.
2. Publish an event to a message broker.

At first glance, this appears straightforward, but these operations involve **two separate systems**. If one succeeds and the other fails, the application enters an inconsistent state.

This is known as the **Dual-Write Problem**.

The **Transaction Outbox Pattern** solves this problem by storing the outgoing event in the same local database transaction as the business data. A separate background process later reads the pending events and publishes them to the message broker.

This guarantees that **if the business transaction commits, the event will eventually be published**.

---

# What this sample demonstrates

This sample models a simple Transaction Outbox using three components:

* **OutboxMessage** — represents a persisted domain event.
* **OutboxStore** — stores pending messages.
* **TransactionOutbox** — records and publishes outbox events.

The implementation focuses on the core algorithm rather than production infrastructure.

---

# The Dual-Write Problem

Consider an order service.

Naively, the service performs:

```text id="dlm9zx"
1. Save Order

2. Publish OrderCreated Event
```

What happens if the application crashes?

Scenario 1

```text id="jlwmc7"
Database

✓ Saved

Broker

✗ Failed
```

The order exists.

No event is published.

Other services never learn that the order was created.

---

Scenario 2

```text id="o5yl0n"
Database

✗ Failed

Broker

✓ Published
```

Other services believe the order exists.

The database says otherwise.

The system becomes inconsistent.

---

# How Transaction Outbox Solves It

Instead of publishing immediately:

```text id="g5up7w"
BEGIN TRANSACTION

Save Order

Save Outbox Event

COMMIT
```

Both writes occur in **one local database transaction**.

After the transaction commits:

```text id="6kshv9"
Background Publisher

↓

Read Pending Events

↓

Publish Event

↓

Mark Published
```

The event may be delayed slightly, but it will not be lost.

---

# Core Idea

Each business transaction performs:

```text id="pcjlwm"
Business Data

+

Outbox Message
```

inside the **same database transaction**.

Later:

```text id="8m69pw"
Outbox

↓

Message Broker
```

The publisher runs independently of business logic.

---

# Architecture

```text id="j6xgms"
             +--------------------+
             |    Application     |
             +--------------------+
                     |
        Local Database Transaction
                     |
      +--------------+--------------+
      |                             |
Business Tables              Outbox Table
                                    |
                                    |
                          Background Publisher
                                    |
                                    |
                           Message Broker
                                    |
                                    |
                           Other Services
```

---

# Components

## OutboxMessage

Represents an event waiting to be published.

Typical fields include:

* Message Id
* Event Type
* Payload
* Created Time
* Published Status
* Retry Count

Example:

```text id="9g2g7x"
Id:
123

Event:
OrderCreated

Payload:
order-123

Published:
False
```

---

## OutboxStore

Stores pending messages.

In production this is typically:

* a SQL table,
* a NoSQL collection,
* or durable storage.

This sample uses an in-memory implementation for simplicity.

---

## TransactionOutbox

Coordinates:

* recording events,
* retrieving pending events,
* publishing messages,
* marking successful publications.

---

# Example Flow

Business transaction:

```text id="tduo65"
Create Order

↓

Insert Order

↓

Insert Outbox Message

↓

Commit
```

Later:

```text id="v4az53"
Publisher

↓

Read Outbox

↓

Publish Event

↓

Mark Published
```

---

# Example Usage

```csharp id="1g5btt"
var store = new OutboxStore();
var outbox = new TransactionOutbox(store);

outbox.RecordEvent(
    "OrderCreated",
    "order-123");

outbox.PublishPendingMessages(message =>
{
    Console.WriteLine(
        $"Publishing: {message.EventType} -> {message.Payload}");
});
```

Example output:

```text id="t96kbm"
Publishing:

OrderCreated

↓

order-123
```

---

# Publishing Strategies

There are two common ways to publish Outbox messages.

## 1. Polling Publisher

A background worker periodically scans the Outbox table.

```text id="7x5lm5"
Every 5 seconds

↓

Read Pending Messages

↓

Publish

↓

Mark Published
```

Advantages:

* Simple
* Database independent
* Easy to implement

Disadvantages:

* Slight publication delay
* Additional database queries

---

## 2. Change Data Capture (CDC)

Instead of polling, database changes are streamed automatically.

Example:

```text id="tjlwmn"
Database

↓

Transaction Log

↓

CDC Tool

↓

Message Broker
```

Common CDC technologies include:

* Debezium
* SQL Server CDC
* PostgreSQL Logical Replication
* MySQL Binlog

Advantages:

* Near real-time publication
* Lower polling overhead

Disadvantages:

* More infrastructure
* More operational complexity

---

# Failure Recovery

Suppose publishing fails.

```text id="mtlajw"
Database

✓ Order Saved

✓ Outbox Saved

Broker

✗ Failed
```

The message remains:

```text id="tjlwmr"
Published = False
```

The next publisher execution retries.

Eventually:

```text id="jlwm8t"
Publish

↓

Success

↓

Published = True
```

No event is lost.

---

# Duplicate Messages

A publisher may crash after publishing but before marking the message as published.

Example:

```text id="z2dtbl"
Publish

✓

Crash

↓

Published Flag

Not Updated
```

On restart:

```text id="jjlwmm"
Publish Again
```

The consumer receives the same message twice.

Therefore, consumers should be **idempotent**.

---

# Idempotent Consumers

Consumers should ignore duplicate events.

Typical approaches:

* Message Id tracking
* Processed-message table
* Version numbers
* Business keys

Example:

```text id="9jlwm6"
Already Processed?

↓

Yes

↓

Ignore
```

---

# Time Complexity

Assuming **N** pending messages:

| Operation             | Complexity |
| --------------------- | ---------: |
| Record Event          |       O(1) |
| Read Pending Messages |       O(N) |
| Publish               |       O(N) |
| Mark Published        |       O(1) |

---

# Advantages

* Eliminates the dual-write problem.
* Guarantees eventual event publication.
* No distributed transaction required.
* Works with any message broker.
* High reliability.
* Supports retries.
* Compatible with microservices.
* Simple implementation.

---

# Limitations

Transaction Outbox provides **eventual consistency**, not immediate consistency.

Challenges include:

* Background publisher required.
* Additional database table.
* Duplicate message handling.
* Retry management.
* Monitoring pending messages.
* Slight publication latency.

---

# Transaction Outbox vs. Distributed Transactions

| Feature                    | Transaction Outbox | Two-Phase Commit |
| -------------------------- | ------------------ | ---------------- |
| Distributed Locking        | No                 | Yes              |
| Atomic Cross-System Commit | No                 | Yes              |
| Eventual Consistency       | Yes                | No               |
| Scalability                | High               | Moderate         |
| Availability               | High               | Lower            |
| Message Retries            | Yes                | N/A              |

---

# Transaction Outbox vs. Event Sourcing

Although they appear similar, they solve different problems.

| Transaction Outbox              | Event Sourcing                   |
| ------------------------------- | -------------------------------- |
| Stores events temporarily       | Stores events permanently        |
| Used for reliable messaging     | Used as the system of record     |
| Business data stored separately | Events are the source of truth   |
| Messages eventually deleted     | Events are retained indefinitely |

---

# Transaction Outbox vs. Inbox Pattern

The **Outbox Pattern** guarantees reliable **sending**.

The **Inbox Pattern** guarantees reliable **receiving**.

Often they are used together.

```text id="4rjlwm"
Service A

↓

Outbox

↓

Broker

↓

Inbox

↓

Service B
```

This provides:

* reliable delivery,
* retry support,
* duplicate detection,
* idempotent processing.

---

# Relationship with Saga

The Outbox Pattern is frequently used together with the Saga Pattern.

Example:

```text id="jlwmvx"
Order Created

↓

Save Order

↓

Outbox Event

↓

Broker

↓

Inventory Service

↓

Reserve Inventory

↓

Payment Service
```

Each Saga step publishes reliable events using its own Outbox.

---

# Typical Use Cases

The Transaction Outbox Pattern is commonly used for:

* Microservices
* Event-driven architectures
* Domain Events
* CQRS
* Saga orchestration
* Order processing
* Payment systems
* Inventory management
* User registration
* Notification services

---

# When to Use It

Use the Transaction Outbox Pattern when:

* Updating a database and publishing an event must succeed together.
* Distributed transactions should be avoided.
* Eventual consistency is acceptable.
* Reliable messaging is required.
* Services own independent databases.

---

# When Not to Use It

Avoid the Transaction Outbox Pattern when:

* The application is monolithic with a single database.
* No message broker is involved.
* Immediate consistency is mandatory.
* Distributed transactions are already required.

---

# Summary

The Transaction Outbox Pattern solves the **Dual-Write Problem** by recording business data and outgoing events within the same local transaction. A separate publisher later delivers pending events to a message broker, ensuring that committed business changes are eventually reflected across the system. Although it introduces a background publishing process and requires idempotent consumers, it provides a highly reliable and scalable approach to integrating databases with event-driven architectures, making it a foundational pattern for modern microservices and Saga-based workflows.
