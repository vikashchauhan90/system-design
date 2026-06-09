namespace DistributedSystem.Raft;

// Abstract network interface so the algorithm can be tested locally
public interface IRaftNetwork
{
    Task<RequestVoteResponse> SendRequestVoteAsync(string peerId, RequestVoteRequest request);
    Task<AppendEntriesResponse> SendAppendEntriesAsync(string peerId, AppendEntriesRequest request);
}
