using DistributedSystem.Paxos.Models;

namespace DistributedSystem.Paxos.Messages;

public sealed record PrepareRequest(
    string ProposerId,
    BallotNumber Ballot);
