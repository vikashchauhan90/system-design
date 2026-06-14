namespace DistributedSystem.Crdt.Abstractions;

public interface ICrdt<T>
{
    T Value { get; }

    void Merge(T other);
}
