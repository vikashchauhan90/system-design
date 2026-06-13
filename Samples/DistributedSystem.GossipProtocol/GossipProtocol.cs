using System.Collections.Concurrent;

namespace DistributedSystem.GossipProtocol;

/// <summary>
/// Represents the state of a cluster member (node/broker)
/// </summary>
public enum MemberState
{
    Alive,
    Suspected,
    Dead
}

/// <summary>
/// Information about a cluster member that gets gossiped
/// </summary>
public sealed class MemberInfo : ICloneable
{
    public string NodeId { get; set; }
    public string Hostname { get; set; }
    public int Port { get; set; }
    public long Timestamp { get; set; } // Last update timestamp
    public MemberState State { get; set; }
    public int GenerationId { get; set; } // Incremented when state changes
    public Dictionary<string, object> Metadata { get; set; }

    public MemberInfo(string nodeId, string hostname, int port)
    {
        NodeId = nodeId;
        Hostname = hostname;
        Port = port;
        Timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        State = MemberState.Alive;
        GenerationId = 0;
        Metadata = new Dictionary<string, object>();
    }

    public object Clone()
    {
        return new MemberInfo(NodeId, Hostname, Port)
        {
            Timestamp = Timestamp,
            State = State,
            GenerationId = GenerationId,
            Metadata = new Dictionary<string, object>(Metadata)
        };
    }

    public override string ToString()
    {
        return $"{NodeId} ({Hostname}:{Port}) - {State} [Gen:{GenerationId}]";
    }
}

/// <summary>
/// Gossip message payload
/// </summary>
public sealed class GossipMessage
{
    public string SenderId { get; set; }
    public long Timestamp { get; set; }
    public Dictionary<string, MemberInfo> MemberStates { get; set; }
    public List<string> AlivePeers { get; set; }
    public List<string> SuspectedPeers { get; set; }
    public List<string> DeadPeers { get; set; }

    public GossipMessage(string senderId)
    {
        SenderId = senderId;
        Timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        MemberStates = new Dictionary<string, MemberInfo>();
        AlivePeers = new List<string>();
        SuspectedPeers = new List<string>();
        DeadPeers = new List<string>();
    }
}

/// <summary>
/// Core Gossip Protocol implementation for cluster membership and state dissemination
/// </summary>
public sealed class GossipProtocol
{
    private readonly string _nodeId;
    private readonly ConcurrentDictionary<string, MemberInfo> _memberStates;
    private readonly Random _random;
    private readonly int _fanout; // Number of peers to gossip with each round
    private readonly long _suspicionTimeout; // Time before suspected becomes dead
    private readonly long _deadTimeout; // Time to remove dead member
    private int _generationId;

    public string NodeId => _nodeId;
    public int MemberCount => _memberStates.Count;
    public IEnumerable<MemberInfo> Members => _memberStates.Values;
    public IEnumerable<MemberInfo> AlivePeers => _memberStates.Values.Where(m => m.State == MemberState.Alive && m.NodeId != _nodeId);
    public IEnumerable<MemberInfo> SuspectedPeers => _memberStates.Values.Where(m => m.State == MemberState.Suspected);
    public IEnumerable<MemberInfo> DeadPeers => _memberStates.Values.Where(m => m.State == MemberState.Dead);

    /// <summary>
    /// Creates a gossip protocol instance
    /// </summary>
    /// <param name="nodeId">Unique identifier for this node</param>
    /// <param name="hostname">Hostname/IP address</param>
    /// <param name="port">Port number</param>
    /// <param name="fanout">Number of peers to gossip with each round (default: 3)</param>
    /// <param name="suspicionTimeout">Time in ms before suspected becomes dead (default: 5000ms)</param>
    /// <param name="deadTimeout">Time in ms to remove dead member (default: 60000ms)</param>
    public GossipProtocol(
        string nodeId,
        string hostname,
        int port,
        int fanout = 3,
        long suspicionTimeout = 5000,
        long deadTimeout = 60000)
    {
        if (string.IsNullOrWhiteSpace(nodeId))
            throw new ArgumentException("NodeId cannot be null or empty", nameof(nodeId));
        if (fanout <= 0)
            throw new ArgumentException("Fanout must be positive", nameof(fanout));

        _nodeId = nodeId;
        _fanout = fanout;
        _suspicionTimeout = suspicionTimeout;
        _deadTimeout = deadTimeout;
        _random = new Random();
        _memberStates = new ConcurrentDictionary<string, MemberInfo>();
        _generationId = 0;

        // Add self to cluster
        var selfInfo = new MemberInfo(nodeId, hostname, port);
        _memberStates.TryAdd(nodeId, selfInfo);
    }

    /// <summary>
    /// Joins the cluster with known members
    /// </summary>
    public void JoinCluster(IEnumerable<MemberInfo> seedMembers)
    {
        foreach (var member in seedMembers)
        {
            if (member.NodeId != _nodeId)
            {
                _memberStates.TryAdd(member.NodeId, (MemberInfo)member.Clone());
            }
        }
    }

    /// <summary>
    /// Broadcasts a member state to all known peers
    /// Simulates the gossip round - each node sends to F random peers
    /// </summary>
    public GossipMessage? GossipRound()
    {
        // Clean up old dead members
        CleanupDeadMembers();

        // Mark old suspected as dead
        PromoteSuspectedToDead();

        // Create gossip message with current state
        var message = new GossipMessage(_nodeId);

        foreach (var kvp in _memberStates)
        {
            message.MemberStates[kvp.Key] = (MemberInfo)kvp.Value.Clone();

            if (kvp.Value.State == MemberState.Alive)
                message.AlivePeers.Add(kvp.Key);
            else if (kvp.Value.State == MemberState.Suspected)
                message.SuspectedPeers.Add(kvp.Key);
            else if (kvp.Value.State == MemberState.Dead)
                message.DeadPeers.Add(kvp.Key);
        }

        return message;
    }

    /// <summary>
    /// Process a received gossip message from a peer
    /// </summary>
    public void ProcessGossipMessage(GossipMessage message)
    {
        if (message?.MemberStates == null || message.MemberStates.Count == 0)
            return;

        foreach (var (nodeId, memberInfo) in message.MemberStates)
        {
            if (nodeId == _nodeId)
            {
                // Update generation for self if peer has newer info
                if (memberInfo.GenerationId > _generationId)
                {
                    _generationId = memberInfo.GenerationId + 1;
                    UpdateSelfState();
                }
                continue;
            }

            // Get or create member info
            if (!_memberStates.TryGetValue(nodeId, out var currentInfo))
            {
                _memberStates.TryAdd(nodeId, (MemberInfo)memberInfo.Clone());
                continue;
            }

            // Update if received info is newer (generation-based)
            if (memberInfo.GenerationId > currentInfo.GenerationId)
            {
                var updated = (MemberInfo)memberInfo.Clone();
                updated.Timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
                _memberStates[nodeId] = updated;
            }
            else if (memberInfo.GenerationId == currentInfo.GenerationId)
            {
                // Same generation - update timestamp to indicate freshness
                if (memberInfo.Timestamp > currentInfo.Timestamp)
                {
                    currentInfo.Timestamp = memberInfo.Timestamp;
                }
            }
        }
    }

    /// <summary>
    /// Reports a peer as suspected
    /// </summary>
    public void SuspectPeer(string peerId)
    {
        if (_memberStates.TryGetValue(peerId, out var member))
        {
            if (member.State == MemberState.Alive)
            {
                member.State = MemberState.Suspected;
                member.GenerationId++;
                member.Timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            }
        }
    }

    /// <summary>
    /// Reports a peer as dead
    /// </summary>
    public void ConfirmDead(string peerId)
    {
        if (_memberStates.TryGetValue(peerId, out var member))
        {
            if (member.State != MemberState.Dead)
            {
                member.State = MemberState.Dead;
                member.GenerationId++;
                member.Timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            }
        }
    }

    /// <summary>
    /// Resurrects a peer (marks as alive after being dead)
    /// Typically used when peer reappears/recovers
    /// </summary>
    public void ResurrectPeer(string peerId)
    {
        if (_memberStates.TryGetValue(peerId, out var member))
        {
            if (member.State == MemberState.Dead || member.State == MemberState.Suspected)
            {
                member.State = MemberState.Alive;
                member.GenerationId++;
                member.Timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            }
        }
    }

    /// <summary>
    /// Gets a list of random peers to send gossip to (fanout)
    /// </summary>
    public List<MemberInfo> SelectRandomPeers()
    {
        var alivePeersList = AlivePeers.ToList();

        if (alivePeersList.Count == 0)
            return new List<MemberInfo>();

        var peerCount = Math.Min(_fanout, alivePeersList.Count);
        var selected = new List<MemberInfo>();

        for (int i = 0; i < peerCount; i++)
        {
            var randomIndex = _random.Next(alivePeersList.Count);
            selected.Add(alivePeersList[randomIndex]);
        }

        return selected;
    }

    /// <summary>
    /// Gets member info by node ID
    /// </summary>
    public MemberInfo? GetMemberInfo(string nodeId)
    {
        _memberStates.TryGetValue(nodeId, out var memberInfo);
        return memberInfo;
    }

    /// <summary>
    /// Updates metadata for this node
    /// </summary>
    public void UpdateMetadata(string key, object value)
    {
        if (_memberStates.TryGetValue(_nodeId, out var selfInfo))
        {
            selfInfo.Metadata[key] = value;
            selfInfo.Timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        }
    }

    /// <summary>
    /// Gets metadata value for a node
    /// </summary>
    public object? GetMetadata(string nodeId, string key)
    {
        if (_memberStates.TryGetValue(nodeId, out var memberInfo))
        {
            memberInfo.Metadata.TryGetValue(key, out var value);
            return value;
        }
        return null;
    }

    /// <summary>
    /// Checks if the protocol has converged (all members learned all state)
    /// </summary>
    public bool HasConverged()
    {
        // Simple heuristic: all members have same generation count within 1
        var members = _memberStates.Values.ToList();
        if (members.Count < 2)
            return true;

        var maxGen = members.Max(m => m.GenerationId);
        var minGen = members.Min(m => m.GenerationId);

        return maxGen - minGen <= 1;
    }

    /// <summary>
    /// Gets convergence status as percentage
    /// </summary>
    public double GetConvergencePercentage()
    {
        var members = _memberStates.Values.ToList();
        if (members.Count < 2)
            return 100.0;

        var maxGen = members.Max(m => m.GenerationId);
        var convergedCount = members.Count(m => m.GenerationId >= maxGen - 1);

        return (double)convergedCount / members.Count * 100;
    }

    /// <summary>
    /// Gets dissemination progress (how many nodes have learned about latest state)
    /// </summary>
    public int GetDisseminationDepth()
    {
        var maxGen = _memberStates.Values.Max(m => m.GenerationId);
        return _memberStates.Values.Count(m => m.GenerationId == maxGen);
    }

    public override string ToString()
    {
        var alive = AlivePeers.Count();
        var suspected = SuspectedPeers.Count();
        var dead = DeadPeers.Count();

        return $"Node: {_nodeId} | Alive: {alive} | Suspected: {suspected} | Dead: {dead}";
    }

    // Private helpers

    private void CleanupDeadMembers()
    {
        var now = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

        var toRemove = _memberStates
            .Where(kvp => kvp.Value.State == MemberState.Dead && 
                         (now - kvp.Value.Timestamp) > _deadTimeout &&
                         kvp.Key != _nodeId)
            .Select(kvp => kvp.Key)
            .ToList();

        foreach (var nodeId in toRemove)
        {
            _memberStates.TryRemove(nodeId, out _);
        }
    }

    private void PromoteSuspectedToDead()
    {
        var now = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

        foreach (var member in _memberStates.Values)
        {
            if (member.State == MemberState.Suspected &&
                (now - member.Timestamp) > _suspicionTimeout)
            {
                member.State = MemberState.Dead;
                member.GenerationId++;
                member.Timestamp = now;
            }
        }
    }

    private void UpdateSelfState()
    {
        if (_memberStates.TryGetValue(_nodeId, out var selfInfo))
        {
            selfInfo.GenerationId = _generationId;
            selfInfo.Timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        }
    }
}
