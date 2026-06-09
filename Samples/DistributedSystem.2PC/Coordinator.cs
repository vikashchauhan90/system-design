using System;
using System.Collections.Generic;
using System.Text;

namespace DistributedSystem._2PC;

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

        State = CoordinatorState.WaitingForVotes;
        var voteRequest = new VoteRequest(transaction.TransactionId, transaction.Payload);
        var responses = new List<VoteResponse>(Participants.Count);

        foreach (var participant in Participants)
        {
            var response = participant.Prepare(voteRequest);
            responses.Add(response);
        }

        var shouldCommit = responses.All(response => response.Decision == VoteDecision.Commit);
        var globalDecision = new GlobalDecision(transaction.TransactionId, shouldCommit);

        foreach (var participant in Participants)
        {
            participant.Commit(globalDecision);
        }

        State = shouldCommit ? CoordinatorState.Committed : CoordinatorState.Aborted;
        return shouldCommit;
    }
}
