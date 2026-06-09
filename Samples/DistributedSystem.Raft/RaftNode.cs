using System.Timers;
using Timer = System.Timers.Timer;

namespace DistributedSystem.Raft;

public class RaftNode
{
    public string Id => _id;

    private readonly string _id;
    private readonly List<string> _peers;
    private readonly IRaftNetwork? _network;
    private readonly IPersistentStorage? _storage;

    public PersistentState Persistent { get; private set; } = new();
    public VolatileState Volatile { get; } = new();
    public NodeState State { get; private set; } = NodeState.Follower;

    private readonly Timer _electionTimer;
    private Timer? _heartbeatTimer;

    public RaftNode(string id, IEnumerable<string> peers, IRaftNetwork? network = null, IPersistentStorage? storage = null)
    {
        _id = id;
        _peers = peers.ToList();
        _network = network;
        _storage = storage;

        // load persisted state
        if (_storage != null)
        {
            var st = _storage.LoadAsync(_id).GetAwaiter().GetResult();
            if (st != null) Persistent = st;
        }

        _electionTimer = new Timer(GetRandomElectionTimeout());
        _electionTimer.Elapsed += async (s, e) => await StartElectionAsync();
        _electionTimer.AutoReset = false;
        _electionTimer.Start();
    }

    private static double GetRandomElectionTimeout()
    {
        var rng = Random.Shared;
        return rng.NextDouble() * 150 + 150; // 150-300ms
    }

    private void ResetElectionTimer()
    {
        _electionTimer.Interval = GetRandomElectionTimeout();
        _electionTimer.Stop();
        _electionTimer.Start();
    }

    private void SavePersistent()
    {
        if (_storage != null)
            _ = _storage.SaveAsync(_id, Persistent);
    }

    public async Task StartElectionAsync()
    {
        State = NodeState.Candidate;
        Persistent.CurrentTerm++;
        Persistent.VotedFor = _id;
        SavePersistent();

        int votes = 1; // vote for self
        var lastLogIndex = Persistent.Log.LastOrDefault()?.Index ?? 0;
        var lastLogTerm = Persistent.Log.LastOrDefault()?.Term ?? 0;

        var tasks = _peers.Select(async peer =>
        {
            if (_network == null) return false;
            var req = new RequestVoteRequest(Persistent.CurrentTerm, _id, lastLogIndex, lastLogTerm);
            try
            {
                var res = await _network.SendRequestVoteAsync(peer, req);
                if (res.VoteGranted) Interlocked.Increment(ref votes);
                if (res.Term > Persistent.CurrentTerm)
                {
                    Persistent.CurrentTerm = res.Term;
                    Persistent.VotedFor = null;
                    SavePersistent();
                    State = NodeState.Follower;
                }
            }
            catch
            {
                // network error -> ignore
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
            // restart election timer
            ResetElectionTimer();
        }
    }

    private void BecomeLeader()
    {
        State = NodeState.Leader;
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
        if (State != NodeState.Leader) return;
        await SendHeartbeatsAsync();
    }

    private async Task SendHeartbeatsAsync()
    {
        if (_network == null) return;
        var tasks = _peers.Select(peer => ReplicateToPeerAsync(peer));
        await Task.WhenAll(tasks);
    }

    public async Task AppendCommandAsync(string command)
    {
        if (State != NodeState.Leader) throw new InvalidOperationException("not leader");
        var index = Persistent.Log.Count + 1;
        var entry = new LogEntry(index, Persistent.CurrentTerm, command);
        Persistent.Append(entry);
        SavePersistent();

        await SendHeartbeatsAsync();
    }

    private async Task ReplicateToPeerAsync(string peer)
    {
        if (_network == null) return;
        // determine next index
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
                // back off nextIndex and retry on next heartbeat
                if (Volatile.NextIndex.ContainsKey(peer) && Volatile.NextIndex[peer] > 1)
                    Volatile.NextIndex[peer] = Volatile.NextIndex[peer] - 1;
                else
                    Volatile.NextIndex[peer] = Math.Max(1, nextIndex - 1);
            }
        }
        catch
        {
            // network error
        }
    }

    public AppendEntriesResponse HandleAppendEntries(AppendEntriesRequest req)
    {
        if (req.Term < Persistent.CurrentTerm)
            return new AppendEntriesResponse(Persistent.CurrentTerm, false);

        // convert to follower
        State = NodeState.Follower;
        Persistent.CurrentTerm = req.Term;
        Persistent.VotedFor = null;
        SavePersistent();
        ResetElectionTimer();

        if (req.PrevLogIndex > 0)
        {
            var entry = Persistent.Log.FirstOrDefault(e => e.Index == req.PrevLogIndex);
            if (entry == null || entry.Term != req.PrevLogTerm)
                return new AppendEntriesResponse(Persistent.CurrentTerm, false);
        }

        // append new entries (simple append; conflicts not fully handled)
        foreach (var e in req.Entries)
        {
            // if an entry with same index exists, replace
            var existing = Persistent.Log.FirstOrDefault(x => x.Index == e.Index);
            if (existing != null)
            {
                if (existing.Term != e.Term)
                {
                    // delete conflicting and append
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
            Volatile.CommitIndex = Math.Min(req.LeaderCommit, Persistent.Log.Count);

        SavePersistent();
        return new AppendEntriesResponse(Persistent.CurrentTerm, true);
    }

    public RequestVoteResponse HandleRequestVote(RequestVoteRequest req)
    {
        if (req.Term < Persistent.CurrentTerm)
            return new RequestVoteResponse(Persistent.CurrentTerm, false);

        if ((Persistent.VotedFor == null || Persistent.VotedFor == req.CandidateId) /* && up-to-date check omitted */)
        {
            Persistent.VotedFor = req.CandidateId;
            Persistent.CurrentTerm = req.Term;
            SavePersistent();
            ResetElectionTimer();
            return new RequestVoteResponse(Persistent.CurrentTerm, true);
        }

        return new RequestVoteResponse(Persistent.CurrentTerm, false);
    }
}
