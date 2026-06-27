namespace DistributedSystem._3PC;

public enum CoordinatorState
{
    Initialized,
    WaitingForCanCommit,
    WaitingForPreCommitAcks,
    WaitingForCommitAcks,
    Committed,
    Aborted
}
