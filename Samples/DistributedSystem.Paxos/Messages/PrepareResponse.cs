using DistributedSystem.Paxos.Models;

namespace DistributedSystem.Paxos.Messages;

public sealed record PrepareResponse(
    bool Promised,
    BallotNumber PromisedBallot,
    BallotNumber? AcceptedBallot,
    string? AcceptedValue);
