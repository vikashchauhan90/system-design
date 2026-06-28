using System;
using System.Collections.Generic;
using System.Text;

namespace DistributedSystem.Pages;

public sealed class Page
{
    public int PageId { get; }
    public int Capacity { get; }

    public List<Record> Records { get; } = new();

    public Page(int pageId, int capacity = 4)
    {
        PageId = pageId;
        Capacity = capacity;
    }

    public bool IsFull => Records.Count >= Capacity;

    public void Insert(Record record)
    {
        if (IsFull)
            throw new InvalidOperationException($"Page {PageId} is full");

        Records.Add(record);
        Records.Sort((a, b) => a.Key.CompareTo(b.Key));
    }
}
