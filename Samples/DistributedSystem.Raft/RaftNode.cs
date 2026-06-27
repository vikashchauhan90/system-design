using System.Timers;
using Timer = System.Timers.Timer;

namespace DistributedSystem.Raft;

public class RaftNode : IDisposable
{
    public string Id => _id;

    private readonly string _id;
    private readonly List<string> _peers;
    private readonly IRaftNetwork? _network;
    private readonly IPersistentStorage? _storage;
    private readonly object _stateLock = new();

    public PersistentState Persistent { get; private set; } = new();
    public VolatileState Volatile { get; } = new();
    public NodeState State { get; private set; } = NodeState.Follower;

    private readonly Timer _electionTimer;
    private Timer? _heartbeatTimer;
    private bool _disposed;

    public RaftNode(string id, IEnumerable<string> peers, IRaftNetwork? network = null, IPersistentStorage? storage = null)
    {
        _id = id;
        _peers = peers.ToList();
        _network = network;
        _storage = storage;

        if (_storage != null)
        {
            var storedState = _storage.LoadAsync(_id).GetAwaiter().GetResult();
            Persistent = NormalizePersistedState(storedState);
        }

        Persistent.Log ??= new List<LogEntry>();
        Persistent.State = Persistent.State == default ? NodeState.Follower : Persistent.State;
        State = Persistent.State;

        _electionTimer = new Timer(GetRandomElectionTimeout());
        _electionTimer.Elapsed += async (s, e) => await StartElectionAsync();
        _electionTimer.AutoReset = false;
        _electionTimer.Start();
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _electionTimer.Stop();
        _electionTimer.Dispose();
        _heartbeatTimer?.Stop();
        _heartbeatTimer?.Dispose();
    }

    private static double GetRandomElectionTimeout()
    {
        var rng = Random.Shared;
        return rng.NextDouble() * 150 + 150; // 150-300ms
    }

    private void ResetElectionTimer()
    {
        if (_disposed) return;
        _electionTimer.Stop();
        _electionTimer.Interval = GetRandomElectionTimeout();
        _electionTimer.Start();
    }

    private void SavePersistent()
    {
        if (_storage is null)
        {
            return;
        }

        try
        {
            _storage.SaveAsync(_id, Persistent).GetAwaiter().GetResult();
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Failed to persist Raft state for node {_id}: {ex.Message}");
        }
    }

    private static PersistentState NormalizePersistedState(PersistentState? persistedState)
    {
        if (persistedState is null)
        {
            return new PersistentState();
        }

        persistedState.Log ??= new List<LogEntry>();
        persistedState.State = persistedState.State == default ? NodeState.Follower : persistedState.State;
        return persistedState;
    }

    private void SetState(NodeState newState)
    {
        lock (_stateLock)
        {
            State = newState;
            Persistent.State = newState;
        }

        SavePersistent();
    }

    private bool IsLogUpToDate(long lastLogIndex, long lastLogTerm, RequestVoteRequest request)
    {
        var candidateLastTerm = request.LastLogTerm;
        if (candidateLastTerm != lastLogTerm)
        {
            return candidateLastTerm > lastLogTerm;
        }

        return request.LastLogIndex >= lastLogIndex;
    }

    public async Task StartElectionAsync()
    {
        if (_disposed)
        {
            return;
        }

        lock (_stateLock)
        {
            State = NodeState.Candidate;
            Persistent.State = NodeState.Candidate;
            Persistent.CurrentTerm++;
            Persistent.VotedFor = _id;
        }
        SavePersistent();

        int votes = 1; // vote for self
        var lastLogIndex = Persistent.Log.LastOrDefault()?.Index ?? 0;
        var lastLogTerm = Persistent.Log.LastOrDefault()?.Term ?? 0;

        var tasks = _peers.Select(async peer =>
        {
            if (_network == null)
            {
                return false;
            }

            var req = new RequestVoteRequest(Persistent.CurrentTerm, _id, lastLogIndex, lastLogTerm);
            try
            {
                var res = await _network.SendRequestVoteAsync(peer, req);
                if (res.VoteGranted)
                {
                    Interlocked.Increment(ref votes);
                }

                if (res.Term > Persistent.CurrentTerm)
                {
                    lock (_stateLock)
                    {
                        Persistent.CurrentTerm = res.Term;
                        Persistent.VotedFor = null;
                        Persistent.State = NodeState.Follower;
                        State = NodeState.Follower;
                    }
                    SavePersistent();
                }
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"Vote request failed for peer {peer}: {ex.Message}");
            }

            return true;
        });

        await Task.WhenAll(tasks);

        if (State == NodeState.Candidate && votes > (_peers.Count + 1) / 2)
        {
            BecomeLeader();
        }
        else
        {
            ResetElectionTimer();
        }
    }

    private void BecomeLeader()
    {
        lock (_stateLock)
        {
            State = NodeState.Leader;
            Persistent.State = NodeState.Leader;
        }
        SavePersistent();

        foreach (var p in _peers)
        {
            Volatile.NextIndex[p] = Persistent.Log.Count + 1;
            Volatile.MatchIndex[p] = 0;
        }

        _heartbeatTimer ??= new Timer(50);
        _heartbeatTimer.Elapsed -= HeartbeatElapsed;
        _heartbeatTimer.Elapsed += HeartbeatElapsed;
        _heartbeatTimer.AutoReset = true;
        _heartbeatTimer.Start();
    }

    private async void HeartbeatElapsed(object? s, ElapsedEventArgs e)
    {
        if (State != NodeState.Leader)
        {
            return;
        }

        await SendHeartbeatsAsync();
    }

    private async Task SendHeartbeatsAsync()
    {
        if (_network == null)
        {
            return;
        }

        var tasks = _peers.Select(peer => ReplicateToPeerAsync(peer));
        await Task.WhenAll(tasks);
    }

    public async Task AppendCommandAsync(string command)
    {
        if (State != NodeState.Leader)
        {
            throw new InvalidOperationException("not leader");
        }

        var index = Persistent.Log.Count + 1;
        var entry = new LogEntry(index, Persistent.CurrentTerm, command);
        Persistent.Append(entry);
        SavePersistent();

        await SendHeartbeatsAsync();
    }

    private async Task ReplicateToPeerAsync(string peer)
    {
        if (_network == null)
        {
            return;
        }

        var nextIndex = Volatile.NextIndex.ContainsKey(peer) ? Volatile.NextIndex[peer] : 1;
        var prevLogIndex = nextIndex - 1;
        var prevLogTerm = 0L;
        if (prevLogIndex > 0)
        {
            var prev = Persistent.Log.FirstOrDefault(e => e.Index == prevLogIndex);
            prevLogTerm = prev?.Term ?? 0;
        }

        var entries = Persistent.Log.Where(e => e.Index >= nextIndex).ToList();
        var req = new AppendEntriesRequest(Persistent.CurrentTerm, _id, prevLogIndex, prevLogTerm, entries, Volatile.CommitIndex);
        try
        {
            var res = await _network.SendAppendEntriesAsync(peer, req);
            if (res.Success)
            {
                if (entries.Any())
                {
                    var last = entries.Last();
                    Volatile.MatchIndex[peer] = last.Index;
                    Volatile.NextIndex[peer] = last.Index + 1;
                }
            }
            else
            {
                if (Volatile.NextIndex.ContainsKey(peer) && Volatile.NextIndex[peer] > 1)
                {
                    Volatile.NextIndex[peer] = Volatile.NextIndex[peer] - 1;
                }
                else
                {
                    Volatile.NextIndex[peer] = Math.Max(1, nextIndex - 1);
                }
            }
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"AppendEntries failed for peer {peer}: {ex.Message}");
        }
    }

    public AppendEntriesResponse HandleAppendEntries(AppendEntriesRequest req)
    {
        if (req.Term < Persistent.CurrentTerm)
        {
            return new AppendEntriesResponse(Persistent.CurrentTerm, false);
        }

        lock (_stateLock)
        {
            State = NodeState.Follower;
            Persistent.State = NodeState.Follower;
            Persistent.CurrentTerm = req.Term;
            Persistent.VotedFor = null;
        }
        SavePersistent();
        ResetElectionTimer();

        if (req.PrevLogIndex > 0)
        {
            var entry = Persistent.Log.FirstOrDefault(e => e.Index == req.PrevLogIndex);
            if (entry == null || entry.Term != req.PrevLogTerm)
            {
                return new AppendEntriesResponse(Persistent.CurrentTerm, false);
            }
        }

        foreach (var e in req.Entries)
        {
            var existing = Persistent.Log.FirstOrDefault(x => x.Index == e.Index);
            if (existing != null)
            {
                if (existing.Term != e.Term)
                {
                    Persistent.Log.RemoveAll(x => x.Index >= e.Index);
                    Persistent.Append(e);
                }
            }
            else
            {
                Persistent.Append(e);
            }
        }

        if (req.LeaderCommit > Volatile.CommitIndex)
        {
            Volatile.CommitIndex = Math.Min(req.LeaderCommit, Persistent.Log.Count);
        }

        SavePersistent();
        return new AppendEntriesResponse(Persistent.CurrentTerm, true);
    }

    public RequestVoteResponse HandleRequestVote(RequestVoteRequest req)
    {
        if (req.Term < Persistent.CurrentTerm)
        {
            return new RequestVoteResponse(Persistent.CurrentTerm, false);
        }

        var lastLogIndex = Persistent.Log.LastOrDefault()?.Index ?? 0;
        var lastLogTerm = Persistent.Log.LastOrDefault()?.Term ?? 0;

        if (Persistent.VotedFor is not null && Persistent.VotedFor != req.CandidateId)
        {
            return new RequestVoteResponse(Persistent.CurrentTerm, false);
        }

        if (!IsLogUpToDate(lastLogIndex, lastLogTerm, req))
        {
            return new RequestVoteResponse(Persistent.CurrentTerm, false);
        }

        lock (_stateLock)
        {
            Persistent.VotedFor = req.CandidateId;
            Persistent.CurrentTerm = req.Term;
            Persistent.State = NodeState.Follower;
            State = NodeState.Follower;
        }
        SavePersistent();
        ResetElectionTimer();
        return new RequestVoteResponse(Persistent.CurrentTerm, true);
    }
}
