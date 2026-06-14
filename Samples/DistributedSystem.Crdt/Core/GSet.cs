using DistributedSystem.Crdt.Abstractions;

public sealed class GSet<T> : IMergeable<GSet<T>>
{
    private readonly HashSet<T> _items = new();

    public IReadOnlyCollection<T> Value => _items;
    public void Add(T item)
    {
        _items.Add(item);
    }
    public void Merge(GSet<T> other)
    {
        foreach (var item in other._items)
        {
            _items.Add(item);
        }
    }
}
