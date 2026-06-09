namespace DistributedSystem.Core.AppendLog;

public sealed class OffsetIndexEntry
{
    public long Offset { get; init; }
    public long Position { get; init; }

    public OffsetIndexEntry(long offset, long position)
    {
        Offset = offset;
        Position = position;
    }
}

public sealed class TimeIndexEntry
{
    public DateTime Timestamp { get; init; }
    public long Offset { get; init; }

    public TimeIndexEntry(DateTime timestamp, long offset)
    {
        Timestamp = timestamp;
        Offset = offset;
    }
}

public sealed class TransactionIndexEntry
{
    public long TransactionId { get; init; }
    public long StartOffset { get; init; }
    public TransactionState State { get; init; }

    public TransactionIndexEntry(long transactionId, long startOffset, TransactionState state)
    {
        TransactionId = transactionId;
        StartOffset = startOffset;
        State = state;
    }
}

public enum TransactionState
{
    Ongoing,
    Committed,
    Aborted
}
