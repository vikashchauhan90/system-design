using DistributedSystem.Crdt.Abstractions;

namespace DistributedSystem.Crdt.Core;

public sealed class GCounter : IMergeable<GCounter>
{
    private readonly Dictionary<string, long> _counts = new();

    public long Value => _counts.Values.Sum();

    public void Increment(string nodeId, long value = 1)
    {
        if (!_counts.ContainsKey(nodeId))
            _counts[nodeId] = 0;

        _counts[nodeId] += value;
    }

    public void Merge(GCounter other)
    {
        foreach (var kv in other._counts)
        {
            if (!_counts.ContainsKey(kv.Key))
                _counts[kv.Key] = kv.Value;
            else
                _counts[kv.Key] =
                    Math.Max(_counts[kv.Key], kv.Value);
        }
    }
}
