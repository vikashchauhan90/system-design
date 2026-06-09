using System;
using System.Collections.Generic;
using System.Text;

namespace DistributedSystem._2PC;

public enum CoordinatorState
{
    Initialized,
    WaitingForVotes,
    Committed,
    Aborted
}
