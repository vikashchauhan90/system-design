# Three-Phase Commit (3PC)

A simplified educational implementation of the **Three-Phase Commit (3PC)** protocol for coordinating distributed transactions across multiple participants.

## Overview

In a distributed system, a single transaction often spans multiple services, databases, or resource managers. To maintain consistency, every participant must either **commit** the transaction or **abort** it—partial commits are not acceptable.

The **Three-Phase Commit (3PC)** protocol extends the traditional **Two-Phase Commit (2PC)** protocol by introducing an additional **Pre-Commit** phase. This extra phase reduces the likelihood that participants remain blocked indefinitely if the coordinator fails during the commit process.

Unlike 2PC, which may leave participants waiting indefinitely for a coordinator that has crashed, 3PC attempts to separate the "ready to commit" state from the final commit decision, allowing participants to make safer progress under certain failure conditions.

> **Note:** Although 3PC reduces blocking compared to 2PC, it does **not** provide the same guarantees as modern consensus algorithms such as Raft or Paxos in asynchronous networks with network partitions.

---

## What this sample demonstrates

This project models the core workflow of the Three-Phase Commit protocol.

The implementation demonstrates:

* Coordinating a distributed transaction.
* Collecting votes from participants.
* Entering the Pre-Commit state.
* Finalizing the transaction with Commit or Abort.
* Tracking coordinator and participant state transitions.

The goal is to illustrate the protocol rather than implement a production-ready transaction manager.

---

## How Three-Phase Commit Works

The protocol consists of three sequential phases.

### Phase 1 — CanCommit (Voting)

The coordinator asks every participant whether the transaction can be committed.

```text
Coordinator
     |
     |---- CanCommit? ----> Participant A
     |---- CanCommit? ----> Participant B
     |---- CanCommit? ----> Participant C
```

Each participant responds with one of two votes:

* **Yes** — The participant is able to commit.
* **No** — The participant cannot commit.

If any participant votes **No**, the coordinator immediately aborts the transaction.

---

### Phase 2 — PreCommit

If every participant votes **Yes**, the coordinator enters the **Pre-Commit** phase.

```text
Coordinator

↓

PreCommit
```

The coordinator sends a **PreCommit** message to every participant.

Participants:

* persist any required state,
* prepare their resources,
* acknowledge that they are ready,
* enter a "prepared" state.

At this point, every participant knows that all other participants have also agreed to commit.

---

### Phase 3 — DoCommit

After receiving acknowledgements from every participant, the coordinator issues the final commit.

```text
Coordinator

↓

Commit
```

Participants permanently apply the transaction and release any held resources.

The transaction is now complete.

---

## Protocol Flow

Successful execution looks like this:

```text
Coordinator                  Participants

CanCommit?  ------------->

           <-------------  Yes

PreCommit  ------------->

           <-------------  ACK

Commit     ------------->

           <-------------  Done
```

If any participant rejects the transaction during the voting phase:

```text
Coordinator

↓

Abort
```

Every participant rolls back its work.

---

## State Machine

### Coordinator States

```text
Initial

↓

CanCommit

↓

PreCommit

↓

Commit
```

or

```text
Initial

↓

CanCommit

↓

Abort
```

---

### Participant States

```text
Idle

↓

Voting

↓

Prepared

↓

Committed
```

or

```text
Idle

↓

Voting

↓

Aborted
```

---

## Main Components

### Coordinator

The coordinator orchestrates the transaction.

Responsibilities include:

* starting the protocol,
* collecting votes,
* entering the Pre-Commit phase,
* broadcasting the final decision,
* handling transaction completion.

---

### Participant

Each participant represents a resource manager, database, or service.

Participants can:

* vote Yes or No,
* prepare for commit,
* acknowledge readiness,
* commit,
* abort.

---

### Transaction

Represents the unit of work being coordinated across multiple participants.

A transaction may involve updates to multiple resources that must succeed or fail together.

---

### GlobalDecision

Represents the final outcome.

Possible values include:

```text
Commit

or

Abort
```

All participants eventually receive the same decision.

---

## Demo Usage

Run the sample:

```bash
dotnet run --project Samples/DistributedSystem.3PC/DistributedSystem.3PC.csproj
```

Example output:

```text
Transaction: Commit

Coordinator:
Committed

Participants

Participant A
Committed

Participant B
Committed

Participant C
Committed
```

If a participant rejects the transaction:

```text
Transaction: Abort

Coordinator:
Aborted

Participants

Participant A
Aborted

Participant B
Aborted

Participant C
Aborted
```

---

## Time Complexity

Assuming **N** participants:

| Operation | Complexity |
| --------- | ---------: |
| Voting    |       O(N) |
| PreCommit |       O(N) |
| Commit    |       O(N) |

Communication complexity:

```text
3 rounds

CanCommit
PreCommit
Commit
```

This requires more messages than Two-Phase Commit but provides additional coordination before the final commit.

---

## Advantages

* Reduces blocking compared to Two-Phase Commit.
* Separates the prepare and commit decisions.
* Participants gain more information before the final commit.
* Easier to recover from certain coordinator failures.
* Demonstrates distributed transaction coordination clearly.

---

## Limitations

Three-Phase Commit is **not** a perfect solution.

It assumes a system with bounded communication delays and reliable failure detection. In fully asynchronous distributed systems, these assumptions may not hold.

Limitations include:

* More communication rounds than 2PC.
* Increased protocol complexity.
* Higher latency before commit.
* Does not tolerate arbitrary network partitions.
* Rarely used in modern distributed databases.

Modern distributed systems often prefer consensus algorithms that provide stronger safety guarantees.

---

## Three-Phase Commit vs. Two-Phase Commit

| Feature              | Two-Phase Commit | Three-Phase Commit |
| -------------------- | ---------------- | ------------------ |
| Phases               | 2                | 3                  |
| Prepare step         | Yes              | Yes                |
| Pre-Commit phase     | No               | Yes                |
| Blocking risk        | Higher           | Lower              |
| Communication rounds | 2                | 3                  |
| Protocol complexity  | Lower            | Higher             |

The additional **Pre-Commit** phase gives participants greater confidence that the transaction is likely to complete, reducing certain blocking scenarios.

---

## Three-Phase Commit vs. Consensus

Although 3PC coordinates distributed commits, it is **not** a consensus algorithm.

| Protocol           | Primary Goal                                        |
| ------------------ | --------------------------------------------------- |
| Two-Phase Commit   | Atomic distributed transactions                     |
| Three-Phase Commit | Atomic transactions with reduced blocking           |
| Raft               | Distributed consensus and replicated state machines |
| Paxos              | Fault-tolerant distributed consensus                |

Consensus protocols are generally preferred for building highly available distributed systems because they continue to provide strong safety guarantees even in the presence of failures and network partitions.

---

## When to Use Three-Phase Commit

Three-Phase Commit is useful for:

* Learning distributed transaction protocols.
* Understanding the evolution of commit protocols.
* Educational simulations.
* Comparing transaction coordination strategies.
* Demonstrating coordinator–participant interactions.

---

## When Not to Use It

Avoid using 3PC when:

* Strong fault tolerance is required.
* Network partitions are expected.
* Consensus across replicas is needed.
* High availability is a priority.
* Building production-grade distributed databases.

Modern alternatives include:

* Raft
* Paxos
* Multi-Paxos
* Viewstamped Replication
* Saga Pattern (for long-running business transactions)
* Transactional Outbox (for event-driven systems)

---

## Summary

Three-Phase Commit extends Two-Phase Commit by introducing a **Pre-Commit** phase between voting and the final commit decision. This additional coordination step reduces certain blocking scenarios by ensuring that all participants are prepared before committing. While it improves upon 2PC in some failure cases, it relies on stronger timing assumptions and is rarely used in production distributed systems today. Nevertheless, it remains an important protocol for understanding the design and evolution of distributed transaction coordination.
