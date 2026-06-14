using DistributedSystem.Paxos.Abstractions;
using DistributedSystem.Paxos.Models;

namespace DistributedSystem.Paxos.Storage;

public sealed class InMemoryStateStore : IStateStore
{
    private readonly Lock _sync = new();

    private PaxosState _state = new PaxosState();

    public Task<PaxosState> LoadAsync(
        CancellationToken cancellationToken)
    {
        lock (_sync)
        {
            return Task.FromResult(_state);
        }
    }

    public Task SaveAsync(
        PaxosState state,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(state);

        lock (_sync)
        {
            _state = state;
        }

        return Task.CompletedTask;
    }
}
