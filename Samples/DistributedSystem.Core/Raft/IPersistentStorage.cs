namespace DistributedSystem.Core.Raft;

public interface IPersistentStorage
{
    Task SaveAsync(string nodeId, PersistentState state);
    Task<PersistentState?> LoadAsync(string nodeId);
}
