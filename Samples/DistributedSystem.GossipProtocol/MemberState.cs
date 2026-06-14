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
