using DistributedSystem.Paxos.Messages;
using DistributedSystem.Paxos.Models;

namespace DistributedSystem.Paxos.Core;


public sealed class Acceptor
{
    private readonly object _sync = new();

    private BallotNumber _promisedBallot;
    private BallotNumber? _acceptedBallot;
    private string? _acceptedValue;

    public PrepareResponse HandlePrepare(
        PrepareRequest request)
    {
        lock (_sync)
        {
            if (request.Ballot < _promisedBallot)
            {
                return new PrepareResponse(
                    false,
                    _promisedBallot,
                    _acceptedBallot,
                    _acceptedValue);
            }

            _promisedBallot = request.Ballot;

            return new PrepareResponse(
                true,
                _promisedBallot,
                _acceptedBallot,
                _acceptedValue);
        }
    }

    public AcceptResponse HandleAccept(
        AcceptRequest request)
    {
        lock (_sync)
        {
            if (request.Ballot < _promisedBallot)
            {
                return new AcceptResponse(
                    false,
                    _promisedBallot);
            }

            _promisedBallot = request.Ballot;
            _acceptedBallot = request.Ballot;
            _acceptedValue = request.Value;

            return new AcceptResponse(
                true,
                request.Ballot);
        }
    }
}
