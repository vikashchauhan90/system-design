# Three-Phase Commit (3PC)

A simplified educational implementation of the Three-Phase Commit protocol for distributed transactions.

## What this sample demonstrates

This project models the core flow of 3PC:

1. The coordinator asks each participant whether it can commit.
2. If all participants agree, the coordinator moves to the pre-commit phase.
3. Each participant confirms that it is ready to commit.
4. If all acknowledgements arrive, the coordinator issues the final commit.

This sample is intentionally small and readable so it can be used as a teaching aid for distributed transaction protocols.

## Main components

- Coordinator: orchestrates the transaction across participants.
- Participant: simulates a resource manager that can vote, prepare, and commit.
- Transaction: the unit of work being coordinated.
- GlobalDecision: the final commit or abort signal.

## Demo usage

Run the project:

```bash
dotnet run --project Samples/DistributedSystem.3PC/DistributedSystem.3PC.csproj
```

The code will print:

- the transaction outcome,
- the coordinator state,
- the final state of each participant.

## Notes

Compared with 2PC, 3PC adds a pre-commit phase to reduce the chance that participants remain blocked when the coordinator fails. The implementation here keeps the behavior simple and focuses on the protocol flow rather than production-grade failure recovery.
