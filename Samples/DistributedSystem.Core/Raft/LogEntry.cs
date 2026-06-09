namespace DistributedSystem.Core.Raft;

public record LogEntry(long Index, long Term, string Command);
