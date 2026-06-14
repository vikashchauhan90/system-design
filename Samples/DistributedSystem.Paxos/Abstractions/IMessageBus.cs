
using DistributedSystem.Paxos.Messages;

namespace DistributedSystem.Paxos.Abstractions;


public interface IMessageBus
{
    Task<IReadOnlyCollection<PrepareResponse>>
        SendPrepareAsync(
            PrepareRequest request,
            CancellationToken cancellationToken);

    Task<IReadOnlyCollection<AcceptResponse>>
        SendAcceptAsync(
            AcceptRequest request,
            CancellationToken cancellationToken);

    Task BroadcastHeartbeatAsync(
        Heartbeat heartbeat,
        CancellationToken cancellationToken);
}
