namespace DistributedSystem.Crdt.Abstractions;

public interface IMergeable<T>
{
    /// <summary>
    /// Merge remote state into current state.
    /// Must be commutative, associative, and idempotent.
    /// </summary>
    void Merge(T other);
}
