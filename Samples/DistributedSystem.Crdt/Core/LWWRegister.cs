using DistributedSystem.Crdt.Abstractions;
using DistributedSystem.Crdt.Models;

public sealed class LwwRegister<T> : IMergeable<LwwRegister<T>>
{
    private T _value;
    private Timestamp _timestamp;
    public void Set(T value, Timestamp timestamp)
    {
        if (timestamp > _timestamp)
        {
            _value = value;
            _timestamp = timestamp;
        }
    }
    public void Merge(LwwRegister<T> other)
    {
        if (other._timestamp > _timestamp)
        {
            _value = other._value;
            _timestamp = other._timestamp;
        }
    }

    public T Value => _value;
}
