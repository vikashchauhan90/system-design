using System;
using System.Collections.Generic;

namespace DistributedSystem._3PC;

public sealed class Coordinator
{
    public CoordinatorState State { get; private set; }
    public IReadOnlyList<Participant> Participants { get; }

    public Coordinator(IEnumerable<Participant> participants)
    {
        Participants = participants?.ToArray() ?? throw new ArgumentNullException(nameof(participants));
        if (!Participants.Any())
        {
            throw new ArgumentException("At least one participant is required.", nameof(participants));
        }

        State = CoordinatorState.Initialized;
    }

    public bool ExecuteTransaction(Transaction transaction)
    {
        if (transaction is null)
        {
            throw new ArgumentNullException(nameof(transaction));
        }

        State = CoordinatorState.WaitingForCanCommit;
        var canCommitResponses = new List<VoteResponse>(Participants.Count);

        foreach (var participant in Participants)
        {
            var response = participant.CanCommit(new CanCommitRequest(transaction.TransactionId, transaction.Payload));
            canCommitResponses.Add(response);
        }

        if (!canCommitResponses.All(response => response.CanCommit))
        {
            foreach (var participant in Participants)
            {
                participant.Abort(new GlobalDecision(transaction.TransactionId, false));
            }

            State = CoordinatorState.Aborted;
            return false;
        }

        State = CoordinatorState.WaitingForPreCommitAcks;
        var preCommitResponses = new List<bool>(Participants.Count);

        foreach (var participant in Participants)
        {
            var ack = participant.PrepareCommit(new PrepareRequest(transaction.TransactionId, transaction.Payload));
            preCommitResponses.Add(ack);
        }

        if (!preCommitResponses.All(ack => ack))
        {
            foreach (var participant in Participants)
            {
                participant.Abort(new GlobalDecision(transaction.TransactionId, false));
            }

            State = CoordinatorState.Aborted;
            return false;
        }

        State = CoordinatorState.WaitingForCommitAcks;
        foreach (var participant in Participants)
        {
            participant.Commit(new GlobalDecision(transaction.TransactionId, true));
        }

        State = CoordinatorState.Committed;
        return true;
    }
}
