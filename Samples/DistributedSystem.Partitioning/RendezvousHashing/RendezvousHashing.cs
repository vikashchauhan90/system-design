using System.Text;
using DistributedSystem.Partitioning.Abstractions;

namespace DistributedSystem.Partitioning.RendezvousHashing;

public sealed class RendezvousHashing
    : IRendezvousHashing
{
    private readonly HashSet<string> _nodes =
        new();

    private readonly IHashFunction _hashFunction;

    public RendezvousHashing(
        IHashFunction hashFunction)
    {
        _hashFunction = hashFunction;
    }

    public void AddNode(string nodeId)
    {
        _nodes.Add(nodeId);
    }

    public void RemoveNode(string nodeId)
    {
        _nodes.Remove(nodeId);
    }

    public string GetNode(string key)
    {
        string? winner = null;
        ulong maxScore = 0;

        foreach (var node in _nodes)
        {
            var score =
                ComputeScore(key, node);

            if (winner == null ||
                score > maxScore)
            {
                winner = node;
                maxScore = score;
            }
        }

        return winner ??
               throw new InvalidOperationException(
                   "No nodes available.");
    }

    private ulong ComputeScore(
        string key,
        string node)
    {
        var bytes =
            Encoding.UTF8.GetBytes(
                $"{key}:{node}");

        return _hashFunction.ComputeHash(bytes);
    }
}
