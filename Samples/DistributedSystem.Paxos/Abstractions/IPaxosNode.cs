namespace DistributedSystem.Paxos.Abstractions;

using DistributedSystem.Paxos.Messages;

public interface IPaxosNode
{
    string NodeId { get; }

    Task<PrepareResponse> OnPrepareAsync(
        PrepareRequest request,
        CancellationToken cancellationToken);

    Task<AcceptResponse> OnAcceptAsync(
        AcceptRequest request,
        CancellationToken cancellationToken);

    Task OnHeartbeatAsync(
        Heartbeat heartbeat,
        CancellationToken cancellationToken);
}
