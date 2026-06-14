using DistributedSystem.Paxos.Abstractions;
using DistributedSystem.Paxos.Core;
using DistributedSystem.Paxos.Messages;

public sealed class PaxosNode : IPaxosNode
{
    private readonly Acceptor _acceptor;

    public string NodeId { get; }

    public PaxosNode(string nodeId)
    {
        NodeId = nodeId;
        _acceptor = new Acceptor();
    }

    public Task<PrepareResponse> OnPrepareAsync(
        PrepareRequest request,
        CancellationToken cancellationToken)
    {
        return Task.FromResult(
            _acceptor.HandlePrepare(request));
    }

    public Task<AcceptResponse> OnAcceptAsync(
        AcceptRequest request,
        CancellationToken cancellationToken)
    {
        return Task.FromResult(
            _acceptor.HandleAccept(request));
    }

    public Task OnHeartbeatAsync(
        Heartbeat heartbeat,
        CancellationToken cancellationToken)
    {
        return Task.CompletedTask;
    }
}
