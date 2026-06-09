using System;
using System.Collections.Generic;
using System.Text;

namespace DistributedSystem._2PC;

public sealed class Participant
{
    public string ParticipantId { get; }
    public ParticipantState State { get; private set; }

    private readonly Func<Transaction, VoteDecision> _voteStrategy;

    public Participant(string participantId, Func<Transaction, VoteDecision>? voteStrategy = null)
    {
        ParticipantId = participantId ?? throw new ArgumentNullException(nameof(participantId));
        State = ParticipantState.Initialized;
        _voteStrategy = voteStrategy ?? DefaultVotingStrategy;
    }

    public VoteResponse Prepare(VoteRequest request)
    {
        if (request is null)
        {
            throw new ArgumentNullException(nameof(request));
        }

        var transaction = new Transaction(request.TransactionId, request.Payload);
        var decision = _voteStrategy(transaction);
        State = decision == VoteDecision.Commit ? ParticipantState.Prepared : ParticipantState.Aborted;

        return new VoteResponse(request.TransactionId, decision);
    }

    public void Commit(GlobalDecision decision)
    {
        if (decision is null)
        {
            throw new ArgumentNullException(nameof(decision));
        }

        if (decision.TransactionId is null)
        {
            throw new ArgumentException("TransactionId is required.", nameof(decision));
        }

        if (decision.Commit)
        {
            State = ParticipantState.Committed;
        }
        else
        {
            State = ParticipantState.Aborted;
        }
    }

    private static VoteDecision DefaultVotingStrategy(Transaction transaction)
    {
        if (string.IsNullOrWhiteSpace(transaction.Payload) || transaction.Payload.Contains("abort", StringComparison.OrdinalIgnoreCase))
        {
            return VoteDecision.Abort;
        }

        return VoteDecision.Commit;
    }
}
