using System;

namespace DistributedSystem._3PC;

public sealed class Participant
{
    public string ParticipantId { get; }
    public ParticipantState State { get; private set; }

    private readonly Func<Transaction, bool> _canCommitStrategy;
    private readonly Func<Transaction, bool> _prepareStrategy;

    public Participant(string participantId, Func<Transaction, bool>? canCommitStrategy = null, Func<Transaction, bool>? prepareStrategy = null)
    {
        ParticipantId = participantId ?? throw new ArgumentNullException(nameof(participantId));
        State = ParticipantState.Initialized;
        _canCommitStrategy = canCommitStrategy ?? DefaultCanCommitStrategy;
        _prepareStrategy = prepareStrategy ?? DefaultPrepareStrategy;
    }

    public VoteResponse CanCommit(CanCommitRequest request)
    {
        if (request is null)
        {
            throw new ArgumentNullException(nameof(request));
        }

        var transaction = new Transaction(request.TransactionId, request.Payload);
        var canCommit = _canCommitStrategy(transaction);
        State = canCommit ? ParticipantState.Ready : ParticipantState.Aborted;

        return new VoteResponse(request.TransactionId, canCommit, canCommit ? "ready" : "vote-no");
    }

    public bool PrepareCommit(PrepareRequest request)
    {
        if (request is null)
        {
            throw new ArgumentNullException(nameof(request));
        }

        if (State != ParticipantState.Ready)
        {
            State = ParticipantState.Aborted;
            return false;
        }

        var transaction = new Transaction(request.TransactionId, request.Payload);
        var prepared = _prepareStrategy(transaction);
        State = prepared ? ParticipantState.PreCommitted : ParticipantState.Aborted;
        return prepared;
    }

    public void Commit(GlobalDecision decision)
    {
        if (decision is null)
        {
            throw new ArgumentNullException(nameof(decision));
        }

        if (decision.Commit && State is ParticipantState.Ready or ParticipantState.PreCommitted)
        {
            State = ParticipantState.Committed;
        }
        else
        {
            State = ParticipantState.Aborted;
        }
    }

    public void Abort(GlobalDecision decision)
    {
        if (decision is null)
        {
            throw new ArgumentNullException(nameof(decision));
        }

        State = ParticipantState.Aborted;
    }

    private static bool DefaultCanCommitStrategy(Transaction transaction)
    {
        if (string.IsNullOrWhiteSpace(transaction.Payload))
        {
            return false;
        }

        return !transaction.Payload.Contains("abort", StringComparison.OrdinalIgnoreCase);
    }

    private static bool DefaultPrepareStrategy(Transaction transaction)
    {
        return !transaction.Payload.Contains("fail", StringComparison.OrdinalIgnoreCase);
    }
}
