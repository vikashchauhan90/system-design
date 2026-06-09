namespace DistributedSystem.Core.Raft;

public class VolatileState
{
    // Not persisted
    public long CommitIndex { get; set; }
    public long LastApplied { get; set; }

    // Only meaningful for leaders
    public Dictionary<string, long> NextIndex { get; } = new();
    public Dictionary<string, long> MatchIndex { get; } = new();
}
