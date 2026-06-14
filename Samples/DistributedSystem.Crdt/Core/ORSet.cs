using DistributedSystem.Crdt.Abstractions;

namespace DistributedSystem.Crdt.Core;

public sealed class ORSet<T> : IMergeable<ORSet<T>>
{
    private readonly Dictionary<T, HashSet<Guid>> _adds = new();
    private readonly HashSet<Guid> _removed = new();
    public Guid Add(T item)
    {
        var id = Guid.NewGuid();

        if (!_adds.ContainsKey(item))
            _adds[item] = new HashSet<Guid>();

        _adds[item].Add(id);

        return id;
    }
    public void Remove(T item)
    {
        if (_adds.TryGetValue(item, out var ids))
        {
            foreach (var id in ids)
                _removed.Add(id);
        }
    }
    public IReadOnlyCollection<T> Value =>
    _adds
        .Where(kv => kv.Value.Any(id => !_removed.Contains(id)))
        .Select(kv => kv.Key)
        .ToList();
    public void Merge(ORSet<T> other)
    {
        foreach (var kv in other._adds)
        {
            if (!_adds.ContainsKey(kv.Key))
                _adds[kv.Key] = new HashSet<Guid>();

            foreach (var id in kv.Value)
                _adds[kv.Key].Add(id);
        }

        foreach (var id in other._removed)
            _removed.Add(id);
    }
}
