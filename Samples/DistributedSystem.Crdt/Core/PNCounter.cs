using DistributedSystem.Crdt.Abstractions;
using DistributedSystem.Crdt.Core;

public sealed class PNCounter: IMergeable<PNCounter>
{
    private readonly GCounter _inc = new();
    private readonly GCounter _dec = new();

    public long Value =>
        _inc.Value - _dec.Value;
    public void Increment(string nodeId, long value = 1)
    => _inc.Increment(nodeId, value);

    public void Decrement(string nodeId, long value = 1)
        => _dec.Increment(nodeId, value);
    public void Merge(PNCounter other)
    {
        _inc.Merge(other._inc);
        _dec.Merge(other._dec);
    }
}
