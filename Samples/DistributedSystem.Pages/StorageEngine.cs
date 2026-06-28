using System;
using System.Collections.Generic;
using System.Text;

namespace DistributedSystem.Pages;

public sealed class StorageEngine
{
    private readonly Dictionary<int, Page> _disk = new();
    private readonly BufferPool _buffer = new();
    private readonly WriteAheadLog _wal = new();

    private int _nextPageId = 0;
    private readonly int _pageSize;

    public StorageEngine(int pageSize = 4)
    {
        _pageSize = pageSize;
    }

    private Page CreatePage()
    {
        var page = new Page(_nextPageId++, _pageSize);
        _disk[page.PageId] = page;
        return page;
    }

    public void Insert(int key, string value)
    {
        var record = new Record(key, value);

        // 1. Write-Ahead Log first (durability)
        _wal.Append(record);

        // 2. Find a page with space
        foreach (var page in _disk.Values)
        {
            if (!page.IsFull)
            {
                page.Insert(record);
                _buffer.GetOrAdd(page);
                return;
            }
        }

        // 3. Create new page if needed
        var newPage = CreatePage();
        newPage.Insert(record);
        _buffer.GetOrAdd(newPage);
    }

    public Record? Search(int key)
    {
        // check buffer first
        foreach (var page in _buffer.GetAllPages())
        {
            var found = page.Records.FirstOrDefault(r => r.Key == key);
            if (found != null)
                return found;
        }

        // fallback to disk
        foreach (var page in _disk.Values)
        {
            var found = page.Records.FirstOrDefault(r => r.Key == key);
            if (found != null)
            {
                _buffer.GetOrAdd(page);
                return found;
            }
        }

        return null;
    }

    public void Flush()
    {
        Console.WriteLine("\n--- FLUSHING PAGES TO DISK ---");

        foreach (var page in _disk.Values)
        {
            Console.WriteLine($"Page {page.PageId}:");

            foreach (var r in page.Records)
                Console.WriteLine($"   {r.Key} -> {r.Value}");
        }
    }

    public void Recover()
    {
        Console.WriteLine("\n--- RECOVERY FROM WAL ---");

        foreach (var record in _wal.Replay())
        {
            Console.WriteLine($"Replaying: {record.Key} -> {record.Value}");

            // re-insert (simplified recovery)
            Insert(record.Key, record.Value);
        }
    }
}
