namespace DistributedSystem.GossipProtocol;


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
