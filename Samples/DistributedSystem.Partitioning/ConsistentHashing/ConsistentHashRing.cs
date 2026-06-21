using System.Text;
using DistributedSystem.Partitioning.Abstractions;

namespace DistributedSystem.Partitioning.ConsistentHashing;

public sealed class ConsistentHashRing
    : IConsistentHashRing
{
    private readonly SortedDictionary<ulong, string>
        _ring = new();

    private readonly IHashFunction _hashFunction;

    private readonly int _virtualNodes;

    public ConsistentHashRing(
        IHashFunction hashFunction,
        int virtualNodes = 100)
    {
        _hashFunction = hashFunction;
        _virtualNodes = virtualNodes;
    }

    public void AddNode(string nodeId)
    {
        for (var i = 0; i < _virtualNodes; i++)
        {
            var hash =
                Hash($"{nodeId}:{i}");

            _ring[hash] = nodeId;
        }
    }

    public void RemoveNode(string nodeId)
    {
        for (var i = 0; i < _virtualNodes; i++)
        {
            var hash =
                Hash($"{nodeId}:{i}");

            _ring.Remove(hash);
        }
    }

    public string GetNode(string key)
    {
        var hash = Hash(key);

        foreach (var entry in _ring)
        {
            if (entry.Key >= hash)
            {
                return entry.Value;
            }
        }

        return _ring.First().Value;
    }

    private ulong Hash(string value)
    {
        return _hashFunction.ComputeHash(
            Encoding.UTF8.GetBytes(value));
    }
}
