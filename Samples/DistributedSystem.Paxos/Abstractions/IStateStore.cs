namespace DistributedSystem.Paxos.Abstractions;

using DistributedSystem.Paxos.Models;

public interface IStateStore
{
    Task<PaxosState> LoadAsync(
        CancellationToken cancellationToken);

    Task SaveAsync(
        PaxosState state,
        CancellationToken cancellationToken);
}
