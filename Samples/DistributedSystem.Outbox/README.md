# Transaction Outbox Pattern

A simple educational implementation of the Transaction Outbox pattern for reliable event publication.

## What this sample demonstrates

The Outbox pattern is used when a service needs to update its own database and publish an event in a reliable way. Instead of sending the event directly, the service writes the event to an outbox table inside the same local transaction as the business data.

A background process later reads the outbox messages and publishes them to a message broker.

## Core idea

This sample models the pattern with three pieces:

- OutboxMessage: a persisted message describing the event.
- OutboxStore: a simple in-memory store for pending messages.
- TransactionOutbox: a helper that records an outbox message and later publishes pending ones.

## Example flow

1. A business operation completes.
2. The service writes the business change and the outbox message in one transaction.
3. A publisher reads the pending outbox messages.
4. After a message is successfully published, it is marked as done.

## Example usage

```csharp
var store = new OutboxStore();
var outbox = new TransactionOutbox(store);

outbox.RecordEvent("OrderCreated", "order-123");
outbox.PublishPendingMessages(message =>
{
    Console.WriteLine($"Publishing: {message.EventType} -> {message.Payload}");
});
```

## Why use it

- Avoids losing events when the database write succeeds but the message publish fails.
- Keeps the business transaction and message publication consistent.
- Works well with event-driven systems and microservices.

## Trade-offs

- Requires an outbox table or equivalent storage.
- Needs a dispatcher or background worker to publish messages.
- Offers reliability, but not instant delivery by itself.
