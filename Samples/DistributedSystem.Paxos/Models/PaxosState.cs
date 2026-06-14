namespace DistributedSystem.Paxos.Models;

public sealed class PaxosState
{
    public BallotNumber PromisedBallot { get; set; }

    public BallotNumber? AcceptedBallot { get; set; }

    public string? AcceptedValue { get; set; }
}
