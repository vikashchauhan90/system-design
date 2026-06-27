# Saga Pattern

A small educational implementation of the Saga pattern for coordinating long-running distributed workflows.

## What this sample demonstrates

The Saga pattern helps manage business processes that span multiple services or databases without relying on a single distributed transaction. Instead of one big atomic transaction, the workflow is broken into steps that each succeed or fail independently.

If a later step fails, the system executes compensating actions to undo previously completed work.

## Core idea

This implementation models a simple saga as a list of steps:

1. Each step has an execute action.
2. Each step also has a compensating action.
3. If a step fails, the previously executed steps are compensated in reverse order.

## Example usage

```csharp
var saga = new Saga();
saga.AddStep(
    "Create order",
    () => true,
    () => true);
saga.AddStep(
    "Reserve inventory",
    () => true,
    () => true);
saga.AddStep(
    "Charge payment",
    () => false,
    () => true);

var success = saga.Run();
```

## When to use Saga

- Order processing
- Payment workflows
- Booking systems
- Distributed business processes that need resilience over strict atomicity

## Trade-offs

- More flexible than two-phase commit for long-running workflows.
- Requires compensating logic and careful failure handling.
- Offers eventual consistency rather than strong atomicity.
