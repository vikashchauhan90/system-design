namespace DistributedSystem.Core.Raft;

public record RequestVoteRequest(long Term, string CandidateId, long LastLogIndex, long LastLogTerm);
public record RequestVoteResponse(long Term, bool VoteGranted);

public record AppendEntriesRequest(long Term, string LeaderId, long PrevLogIndex, long PrevLogTerm, List<LogEntry> Entries, long LeaderCommit);
public record AppendEntriesResponse(long Term, bool Success);
