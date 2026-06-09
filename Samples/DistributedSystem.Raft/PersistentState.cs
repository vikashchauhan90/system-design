using System.Collections.Concurrent;

namespace DistributedSystem.Raft;

public class PersistentState
{
    // persisted to stable storage
    public long CurrentTerm { get; set; }
    public string? VotedFor { get; set; }
    public List<LogEntry> Log { get; set; } = new();

    public void Append(LogEntry entry) => Log.Add(entry);
}
