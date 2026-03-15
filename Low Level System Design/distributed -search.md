# Distributed Search

```C#
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

public class Node
{
    private List<string> _data;

    public Node(List<string> data)
    {
        _data = data;
    }

    public List<string> Search(string query)
    {
        return _data.Where(item => item.Contains(query)).ToList();
    }
}

public class DistributedSearchSystem
{
    private List<Node> _nodes;

    public DistributedSearchSystem(List<List<string>> dataPartitions)
    {
        _nodes = dataPartitions.Select(partition => new Node(partition)).ToList();
    }

    public async Task<List<string>> SearchAsync(string query)
    {
        var tasks = _nodes.Select(node => Task.Run(() => node.Search(query))).ToList();
        var results = await Task.WhenAll(tasks);

        return results.SelectMany(result => result).ToList();
    }
}

public class Program
{
    public static async Task Main(string[] args)
    {
        var dataPartitions = new List<List<string>>
        {
            new List<string> { "apple", "banana", "cherry" },
            new List<string> { "dog", "elephant", "fox" },
            new List<string> { "grape", "honeydew", "ice cream" },
        };

        var searchSystem = new DistributedSearchSystem(dataPartitions);

        var results = await searchSystem.SearchAsync("a");

        foreach (var result in results)
        {
            Console.WriteLine(result);
        }
    }
}

```