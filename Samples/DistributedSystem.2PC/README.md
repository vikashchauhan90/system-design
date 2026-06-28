# Two-Phase Commit (2PC)

A simplified educational implementation of the **Two-Phase Commit (2PC)** protocol for coordinating atomic transactions across multiple participants in a distributed system.

## Overview

In distributed systems, a single business transaction often involves multiple databases, services, or resource managers. If one participant commits while another fails, the system becomes inconsistent.

The **Two-Phase Commit (2PC)** protocol ensures **atomicity** by coordinating all participants so that they either:

* **all commit**, or
* **all abort (rollback)**.

This guarantees that no participant permanently applies a transaction unless every participant is prepared to do so.

2PC is one of the fundamental distributed transaction protocols and forms the basis of many transaction managers, XA-compliant systems, and distributed databases.

---

## What this sample demonstrates

This sample provides a lightweight implementation of the Two-Phase Commit protocol.

It demonstrates:

* Coordinator-driven transaction management.
* Voting among multiple participants.
* Global commit and abort decisions.
* Participant state transitions.
* Transaction rollback when any participant rejects the transaction.

The implementation is intentionally simplified to make the protocol easy to understand and experiment with.

---

## How Two-Phase Commit Works

The protocol consists of two sequential phases.

### Phase 1 — Prepare (Voting Phase)

The coordinator asks every participant whether the transaction can be committed.

```text
Coordinator
      |
      |---- Prepare ----> Participant A
      |---- Prepare ----> Participant B
      |---- Prepare ----> Participant C
```

Each participant:

1. Executes the local transaction.
2. Locks any required resources.
3. Writes recovery information to durable storage.
4. Responds with either:

```text
YES
```

or

```text
NO
```

If **any participant votes NO**, the entire transaction is aborted.

---

### Phase 2 — Commit / Abort (Decision Phase)

If every participant votes **YES**, the coordinator broadcasts:

```text
COMMIT
```

Each participant:

* commits its local transaction,
* releases locks,
* acknowledges completion.

If any participant voted **NO**, the coordinator broadcasts:

```text
ABORT
```

Each participant:

* rolls back local changes,
* releases locks,
* returns to its idle state.

---

## Successful Transaction Flow

```text
Coordinator                  Participants

Prepare ------------->

         <----------- YES

Prepare ------------->

         <----------- YES

Prepare ------------->

         <----------- YES

Commit ------------->

         <----------- ACK
```

Result:

```text
Transaction Committed
```

---

## Failed Transaction Flow

If one participant cannot commit:

```text
Coordinator                  Participants

Prepare ------------->

         <----------- YES

Prepare ------------->

         <----------- NO

Abort --------------->

         <----------- ACK
```

Result:

```text
Transaction Aborted
```

No participant commits the transaction.

---

## Architecture

### Coordinator

The coordinator manages the distributed transaction.

Responsibilities include:

* initiating transactions,
* sending prepare requests,
* collecting votes,
* deciding commit or abort,
* broadcasting the final decision.

---

### Participant

A participant represents a resource manager such as:

* a database,
* a service,
* a message broker,
* or another transactional component.

Each participant:

* executes local work,
* votes on the transaction,
* commits or rolls back based on the coordinator's decision.

---

### Transaction

Represents the unit of work coordinated across multiple participants.

Typical examples include:

* money transfers,
* order processing,
* inventory updates,
* distributed writes.

---

### VoteRequest

Sent by the coordinator during Phase 1.

Contains:

* transaction identifier,
* transaction payload,
* prepare request.

---

### VoteResponse

Returned by participants.

Possible responses:

```text
Commit

or

Abort
```

---

### GlobalDecision

Broadcast after voting completes.

Possible values:

```text
Commit

Abort
```

Every participant eventually receives the same decision.

---

## State Machines

### Participant States

```text
Initialized

↓

Prepared

↓

Committed
```

or

```text
Initialized

↓

Prepared

↓

Aborted
```

Participants remain in the **Prepared** state while waiting for the coordinator's decision.

---

### Coordinator States

```text
Initialized

↓

WaitingForVotes

↓

Committed
```

or

```text
Initialized

↓

WaitingForVotes

↓

Aborted
```

---

## Implementation Example

```csharp
// Create participants
var participants = new List<Participant>
{
    new("participant-1"),
    new("participant-2"),
    new("participant-3", transaction =>
        transaction.Payload.Length % 2 == 0
            ? VoteDecision.Commit
            : VoteDecision.Abort)
};

// Create coordinator
var coordinator = new Coordinator(participants);

// Execute transaction
var transaction = new Transaction(
    "tx-123",
    "important-message");

bool committed =
    coordinator.ExecuteTransaction(transaction);
```

---

## Demo Usage

Run the sample:

```bash
dotnet run --project Samples/DistributedSystem.2PC/DistributedSystem.2PC.csproj
```

or invoke the demo directly:

```csharp
DistributedSystem._2PC.TwoPhaseCommitDemo.RunDemo();
```

Example successful output:

```text
Transaction:
Committed

Coordinator:
Committed

Participant 1:
Committed

Participant 2:
Committed

Participant 3:
Committed
```

Example aborted transaction:

```text
Transaction:
Aborted

Coordinator:
Aborted

Participant 1:
Aborted

Participant 2:
Aborted

Participant 3:
Aborted
```

---

## Time Complexity

Assuming **N** participants:

| Operation        | Complexity |
| ---------------- | ---------: |
| Voting           |       O(N) |
| Commit Broadcast |       O(N) |
| Abort Broadcast  |       O(N) |

Communication requires **two network rounds**:

1. Prepare
2. Commit (or Abort)

---

## Advantages

* Guarantees atomic transactions.
* Ensures all participants reach the same outcome.
* Maintains ACID consistency across distributed systems.
* Relatively simple to understand and implement.
* Widely supported by transaction managers and XA-compliant systems.

---

## Limitations

Although 2PC guarantees atomicity, it has several well-known drawbacks.

### Blocking Protocol

Participants hold locks after voting **YES** until the coordinator responds.

This reduces concurrency and may delay other transactions.

---

### Coordinator Failure

If the coordinator crashes after collecting votes but before sending the final decision:

```text
Participants

↓

Prepared

↓

Waiting...
```

Participants cannot safely commit or abort on their own.

This is the primary weakness of Two-Phase Commit.

---

### Performance

Every distributed transaction requires:

* two communication rounds,
* durable logging,
* coordination between all participants.

This introduces higher latency than local transactions.

---

### Scalability

As the number of participants increases:

* network traffic grows,
* transaction latency increases,
* failures become more likely.

Large distributed systems often avoid long-running 2PC transactions.

---

## Two-Phase Commit vs. Three-Phase Commit

| Feature              |    2PC |    3PC |
| -------------------- | -----: | -----: |
| Phases               |      2 |      3 |
| Communication Rounds |      2 |      3 |
| Blocking             | Higher |  Lower |
| Pre-Commit Phase     |     No |    Yes |
| Complexity           |  Lower | Higher |

The additional **Pre-Commit** phase in 3PC helps reduce certain blocking scenarios by ensuring participants know that every node is prepared before the final commit decision.

---

## 2PC vs. Consensus

Two-Phase Commit is **not** a consensus protocol.

| Protocol           | Purpose                                             |
| ------------------ | --------------------------------------------------- |
| Two-Phase Commit   | Atomic distributed transactions                     |
| Three-Phase Commit | Reduced-blocking transaction coordination           |
| Raft               | Distributed consensus and replicated state machines |
| Paxos              | Fault-tolerant distributed consensus                |

Consensus algorithms are generally preferred for replicated databases because they continue to operate safely under network partitions and leader failures.

---

## Typical Use Cases

Two-Phase Commit is commonly used for:

* Distributed database transactions
* XA transaction managers
* Banking and financial systems
* Order processing systems
* Inventory management
* Multi-database updates
* Enterprise middleware
* Message-oriented middleware

---

## When Not to Use It

Avoid Two-Phase Commit when:

* High availability is more important than strict consistency.
* Network partitions are common.
* Long-running business workflows are involved.
* Horizontal scalability is a primary goal.
* Eventual consistency is acceptable.

Alternatives include:

* Three-Phase Commit (3PC)
* Saga Pattern
* Transactional Outbox
* Event Sourcing
* CRDTs
* Raft
* Paxos

---

## Summary

Two-Phase Commit is the classic protocol for coordinating atomic transactions across multiple distributed participants. It guarantees that every participant reaches the same outcome—either all commit or all abort—making it a cornerstone of distributed transaction processing. While simple and reliable, its blocking behavior and dependence on a central coordinator limit its scalability and fault tolerance. Modern distributed systems often replace or complement 2PC with consensus algorithms or compensation-based approaches, but understanding 2PC remains essential for learning distributed systems and transaction coordination.
