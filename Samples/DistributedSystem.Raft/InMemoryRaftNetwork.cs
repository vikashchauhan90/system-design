namespace DistributedSystem.Raft;

public class InMemoryRaftNetwork : IRaftNetwork
{
    private readonly Dictionary<string, RaftNode> _nodes = new();
    private readonly int _latencyMs;

    public InMemoryRaftNetwork(int latencyMs = 5)
    {
        _latencyMs = latencyMs;
    }

    public void Register(RaftNode node)
    {
        _nodes[node.Id] = node;
    }

    public async Task<RequestVoteResponse> SendRequestVoteAsync(string peerId, RequestVoteRequest request)
    {
        if (!_nodes.TryGetValue(peerId, out var node)) throw new InvalidOperationException("peer not found");
        if (_latencyMs > 0) await Task.Delay(_latencyMs);
        return node.HandleRequestVote(request);
    }

    public async Task<AppendEntriesResponse> SendAppendEntriesAsync(string peerId, AppendEntriesRequest request)
    {
        if (!_nodes.TryGetValue(peerId, out var node)) throw new InvalidOperationException("peer not found");
        if (_latencyMs > 0) await Task.Delay(_latencyMs);
        return node.HandleAppendEntries(request);
    }
}
