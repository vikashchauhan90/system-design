# Two-Phase Commit (2PC)

A distributed transaction protocol that ensures atomicity across multiple participants in a distributed system.

## Overview

Two-Phase Commit (2PC) is a coordination protocol used to decide whether to commit or abort (rollback) a distributed transaction across multiple databases or systems. It guarantees that either all participants commit the transaction or all abort it, maintaining atomicity in a distributed environment.

## Use Cases

- **Distributed Transactions**: Ensuring consistency across multiple databases
- **Microservices**: Coordinating transactions across service boundaries
- **Message Brokers**: Confirming message delivery to all cluster nodes (e.g., Kafka)
- **Financial Systems**: Maintaining ACID properties in cross-system transfers
- **Distributed Databases**: Coordinating writes across replicas

## How It Works

### Phase 1: Prepare (Voting Phase)
1. Coordinator sends a prepare request to all participants
2. Each participant:
   - Executes the transaction up to the point of commit
   - Locks required resources
   - Votes "Yes" (commit) or "No" (abort)
   - Waits for the coordinator's global decision

### Phase 2: Commit/Abort (Decision Phase)
1. Coordinator collects all votes
2. If all participants vote "Yes":
   - Coordinator sends a global COMMIT decision
   - Participants commit the transaction and release locks
3. If any participant votes "No":
   - Coordinator sends a global ABORT decision
   - Participants rollback the transaction and release locks

## Architecture

### Components

- **Coordinator**: Orchestrates the transaction across participants
- **Participant**: Executes local operations and votes on commit
- **Transaction**: The unit of work being coordinated
- **VoteRequest**: Request sent to participants in phase 1
- **VoteResponse**: Participant's vote decision
- **GlobalDecision**: Final commit/abort decision broadcast in phase 2

### States

**Participant States**:
- `Initialized`: Initial state, no transaction in progress
- `Prepared`: Voted and awaiting global decision
- `Committed`: Transaction committed successfully
- `Aborted`: Transaction rolled back

**Coordinator States**:
- `Initialized`: Initial state
- `WaitingForVotes`: Collecting participant votes
- `Committed`: All participants committed
- `Aborted`: Transaction aborted

## Implementation Details

```csharp
// Create participants with voting strategies
var participants = new List<Participant>
{
    new("participant-1"),  // Default strategy: always commit
    new("participant-2"),  // Default strategy: always commit
    new("participant-3", transaction => 
        transaction.Payload.Length % 2 == 0 
            ? VoteDecision.Commit 
            : VoteDecision.Abort)  // Custom voting logic
};

// Create coordinator
var coordinator = new Coordinator(participants);

// Execute a transaction
var transaction = new Transaction("tx-123", "important-message");
var committed = coordinator.ExecuteTransaction(transaction);
```

## Key Features

- **Atomic Commits**: All-or-nothing transaction semantics
- **Configurable Voting**: Participants can implement custom voting strategies
- **State Tracking**: Both coordinator and participants track transaction state
- **Transactional Safety**: Ensures consistency across distributed systems

## Limitations

- **Blocking Protocol**: Participants hold locks during voting, reducing concurrency
- **Coordinator Failure**: If the coordinator fails after phase 1, participants remain blocked
- **Performance**: Higher latency compared to single-system transactions
- **Scalability**: Becomes slower as the number of participants increases

## Improvements & Alternatives

- **Three-Phase Commit (3PC)**: Adds pre-commit phase to handle coordinator failures
- **Sagas**: Compensating transactions for long-running distributed workflows
- **Event Sourcing**: Eventual consistency with compensating events
- **Distributed Consensus**: Raft/Paxos for leader election and replication

## Demo Usage

Run the demo to see 2PC in action:

```csharp
DistributedSystem._2PC.TwoPhaseCommitDemo.RunDemo();
```

Output shows:
- Transaction execution
- Participant voting
- Global decision (COMMIT or ABORT)
- Final state of all participants

## References

- [X/Open XA Specification](https://en.wikipedia.org/wiki/X/Open_XA)
- [Two-Phase Commit Protocol](https://en.wikipedia.org/wiki/Two-phase_commit)
- [Distributed Systems Patterns](https://martinfowler.com/articles/patterns-of-distributed-systems/)
