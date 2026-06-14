using DistributedSystem.Paxos.Models;

namespace DistributedSystem.Paxos.Messages;

public sealed record AcceptRequest(
    string ProposerId,
    BallotNumber Ballot,
    string Value);
