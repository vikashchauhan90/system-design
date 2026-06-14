using DistributedSystem.Paxos.Models;
using System;
using System.Collections.Generic;
using System.Text;

namespace DistributedSystem.Paxos.Messages;

public sealed record AcceptResponse(
    bool Accepted,
    BallotNumber Ballot);
