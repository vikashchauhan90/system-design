namespace DistributedSystem._3PC;

public enum ParticipantState
{
    Initialized,
    Ready,
    PreCommitted,
    Committed,
    Aborted
}
