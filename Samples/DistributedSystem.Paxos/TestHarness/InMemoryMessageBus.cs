using DistributedSystem.Paxos.Abstractions;
using DistributedSystem.Paxos.Messages;

namespace DistributedSystem.Paxos.TestHarness;

public sealed class InMemoryMessageBus : IMessageBus
{
    private readonly IReadOnlyCollection<IPaxosNode> _nodes;

    public InMemoryMessageBus(
        IReadOnlyCollection<IPaxosNode> nodes)
    {
        _nodes = nodes;
    }

    public async Task<IReadOnlyCollection<PrepareResponse>>
        SendPrepareAsync(
            PrepareRequest request,
            CancellationToken cancellationToken)
    {
        var responses = new List<PrepareResponse>();

        foreach (var node in _nodes)
        {
            responses.Add(
                await node.OnPrepareAsync(
                    request,
                    cancellationToken));
        }

        return responses;
    }

    public async Task<IReadOnlyCollection<AcceptResponse>>
        SendAcceptAsync(
            AcceptRequest request,
            CancellationToken cancellationToken)
    {
        var responses = new List<AcceptResponse>();

        foreach (var node in _nodes)
        {
            responses.Add(
                await node.OnAcceptAsync(
                    request,
                    cancellationToken));
        }

        return responses;
    }

    public async Task BroadcastHeartbeatAsync(
        Heartbeat heartbeat,
        CancellationToken cancellationToken)
    {
        foreach (var node in _nodes)
        {
            await node.OnHeartbeatAsync(
                heartbeat,
                cancellationToken);
        }
    }
}
