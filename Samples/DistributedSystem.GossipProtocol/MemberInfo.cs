namespace DistributedSystem.GossipProtocol;

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
