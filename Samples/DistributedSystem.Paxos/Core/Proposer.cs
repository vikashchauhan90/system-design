using DistributedSystem.Paxos.Abstractions;
using DistributedSystem.Paxos.Messages;
using DistributedSystem.Paxos.Models;

namespace DistributedSystem.Paxos.Core;

public sealed class Proposer
{
    private readonly IMessageBus _messageBus;
    private readonly int _replicaCount;

    public Proposer(
        IMessageBus messageBus,
        int replicaCount)
    {
        _messageBus = messageBus;
        _replicaCount = replicaCount;
    }

    public async Task<bool> ElectLeaderAsync(
        string nodeId,
        BallotNumber ballot,
        CancellationToken cancellationToken)
    {
        var prepareResponses =
            await _messageBus.SendPrepareAsync(
                new PrepareRequest(nodeId, ballot),
                cancellationToken);

        var quorum =
            Quorum.TwoThirds(_replicaCount);

        var promises =
            prepareResponses.Count(
                x => x.Promised);

        if (promises < quorum)
            return false;

        var acceptResponses =
            await _messageBus.SendAcceptAsync(
                new AcceptRequest(
                    nodeId,
                    ballot,
                    $"LEADER:{nodeId}"),
                cancellationToken);

        var accepts =
            acceptResponses.Count(
                x => x.Accepted);

        return accepts >= quorum;
    }
}
