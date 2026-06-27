using System;
using System.Collections.Generic;
using System.Text;

namespace DistributedSystem.LSMTree;

public class LSMTree : IDisposable
{
    private readonly string _dataDirectory;
    private MemTable _memTable;
    private readonly List<SSTable> _sstables;
    private int _sstableCounter;
    private readonly object _lock = new object();
    private readonly long _memTableMaxSize;

    public LSMTree(string dataDirectory, long memTableMaxSize = 1024 * 1024) // 1MB
    {
        _dataDirectory = dataDirectory;
        _memTableMaxSize = memTableMaxSize;
        _memTable = new MemTable(memTableMaxSize);
        _sstables = new List<SSTable>();
        _sstableCounter = 0;

        // Create directory if not exists
        Directory.CreateDirectory(dataDirectory);

        // Load existing SSTables
        LoadExistingSSTables();
    }

    private void LoadExistingSSTables()
    {
        var files = Directory.GetFiles(_dataDirectory, "sstable_*.dat")
                             .OrderBy(f => f)
                             .ToList();

        foreach (var file in files)
        {
            try
            {
                var sstable = new SSTable(file);
                _sstables.Add(sstable);
                _sstableCounter = Math.Max(_sstableCounter,
                    int.Parse(Path.GetFileNameWithoutExtension(file).Split('_')[1]) + 1);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error loading {file}: {ex.Message}");
            }
        }
    }

    public void Add(string key, byte[] value)
    {
        lock (_lock)
        {
            // Add to memtable
            _memTable.Add(key, value);

            // If memtable is full, flush to SSTable
            if (_memTable.IsFull)
            {
                FlushMemTable();
            }
        }
    }

    private void FlushMemTable()
    {
        // Get all data from memtable
        var data = _memTable.GetAll().ToList();

        if (data.Count == 0)
            return;

        // Create new SSTable
        var sstable = new SSTable(_dataDirectory, _sstableCounter++, data);
        _sstables.Add(sstable);

        // Clear memtable
        _memTable.Clear();

        Console.WriteLine($"Flushed {data.Count} entries to {sstable.FilePath}");

        // Optional: Trigger compaction if too many SSTables
        if (_sstables.Count > 5)
        {
            Compact();
        }
    }

    public byte[]? Get(string key)
    {
        lock (_lock)
        {
            // 1. Check memtable (latest data)
            var value = _memTable.Get(key);
            if (value != null)
                return value;

            // 2. Check SSTables (newest first)
            foreach (var sstable in _sstables.AsEnumerable().Reverse())
            {
                value = sstable.Get(key);
                if (value != null)
                    return value;
            }

            return null; // Key not found
        }
    }

    private void Compact()
    {
        Console.WriteLine("Starting compaction...");

        // Take oldest SSTables to merge
        int mergeCount = Math.Min(4, _sstables.Count);
        var toMerge = _sstables.Take(mergeCount).ToList();

        if (toMerge.Count < 2)
            return;

        // Collect all entries from SSTables being merged
        var allEntries = new SortedDictionary<string, byte[]>();
        foreach (var sstable in toMerge)
        {
            foreach (var kvp in sstable.GetAll())
            {
                // Latest write wins
                allEntries[kvp.Key] = kvp.Value;
            }
        }

        // Create new merged SSTable
        var newSSTable = new SSTable(_dataDirectory, _sstableCounter++, allEntries);

        // Remove old SSTables
        foreach (var sstable in toMerge)
        {
            _sstables.Remove(sstable);
            sstable.Dispose();
            File.Delete(sstable.FilePath);
        }

        // Add new merged SSTable
        _sstables.Insert(0, newSSTable); // Insert at beginning (oldest)

        Console.WriteLine($"Compaction complete. Merged {mergeCount} files into {newSSTable.FilePath}. Entries: {newSSTable.EntryCount}");
    }

    public void Delete(string key)
    {
        // Use a special "null" value to mark deletion (tombstone)
        Add(key, Array.Empty<byte>());
    }


    public IEnumerable<KeyValuePair<string, byte[]>> Range(string startKey, string endKey)
    {
        var result = new Dictionary<string, byte[]>();

        // Get from memtable
        foreach (var kvp in _memTable.GetAll()
            .Where(x => string.Compare(x.Key, startKey) >= 0 &&
                       string.Compare(x.Key, endKey) <= 0))
        {
            result[kvp.Key] = kvp.Value;
        }

        // Get from SSTables (newest first, overwriting older values)
        foreach (var sstable in _sstables.AsEnumerable().Reverse())
        {
            foreach (var kvp in sstable.GetAll()
                .Where(x => string.Compare(x.Key, startKey) >= 0 &&
                           string.Compare(x.Key, endKey) <= 0))
            {
                result[kvp.Key] = kvp.Value;
            }
        }
        // Return sorted by key, excluding tombstones (empty values)
        return result
            .Where(x => x.Value.Length > 0)
            .OrderBy(x => x.Key)
            .Select(x => x);
    }

    public void PrintStats()
    {
        Console.WriteLine($"=== LSM Tree Statistics ===");
        Console.WriteLine($"MemTable Entries: {_memTable.Count}");
        Console.WriteLine($"MemTable Size: {_memTable.SizeInBytes} bytes");
        Console.WriteLine($"Number of SSTables: {_sstables.Count}");

        long totalEntries = 0;
        long totalSize = 0;
        foreach (var sstable in _sstables)
        {
            totalEntries += sstable.EntryCount;
            totalSize += new FileInfo(sstable.FilePath).Length;
        }
        Console.WriteLine($"Total SSTable Entries: {totalEntries}");
        Console.WriteLine($"Total SSTable Size: {totalSize} bytes");
        Console.WriteLine($"=============================");
    }

    public void Dispose()
    {
        // Flush any remaining data
        FlushMemTable();

        // Dispose all SSTables
        foreach (var sstable in _sstables)
        {
            sstable.Dispose();
        }
    }
}
