using System.Collections.Generic;

namespace DistributedSystem.LSMTree;

public sealed class LSMTree : IDisposable
{
    private readonly string _dataDirectory;
    private MemTable _memTable;
    private readonly List<SSTable> _sstables;
    private readonly CommitLog _commitLog;
    private int _sstableCounter;
    private readonly object _lock = new();
    private readonly long _memTableMaxSize;

    public LSMTree(string dataDirectory, long memTableMaxSize = 1024 * 1024) // 1MB
    {
        _dataDirectory = dataDirectory;
        _memTableMaxSize = memTableMaxSize;
        _memTable = new MemTable(memTableMaxSize);
        _sstables = new List<SSTable>();
        _commitLog = new CommitLog(Path.Combine(dataDirectory, "commit.log"));
        _sstableCounter = 0;

        Directory.CreateDirectory(dataDirectory);
        LoadExistingSSTables();
        ReplayCommitLog();
    }

    private void LoadExistingSSTables()
    {
        var directories = Directory.GetDirectories(_dataDirectory, "sstable_*")
            .OrderBy(path => path, StringComparer.Ordinal)
            .ToList();

        foreach (var directory in directories)
        {
            try
            {
                var sstable = new SSTable(directory);
                _sstables.Add(sstable);
                var sequenceNumber = Path.GetFileName(directory).Split('_')[1];
                _sstableCounter = Math.Max(_sstableCounter, int.Parse(sequenceNumber) + 1);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error loading SSTable '{directory}': {ex.Message}");
            }
        }
    }

    private void ReplayCommitLog()
    {
        foreach (var entry in _commitLog.ReadEntries())
        {
            _memTable.Add(entry.Key, entry.Value);
        }
    }

    public void Add(string key, byte[] value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        var normalizedValue = value ?? Array.Empty<byte>();

        lock (_lock)
        {
            _commitLog.Append(key, normalizedValue);
            _memTable.Add(key, normalizedValue);

            if (_memTable.IsFull)
            {
                FlushMemTable();
            }
        }
    }

    private void FlushMemTable()
    {
        var data = _memTable.GetAll().ToList();
        if (data.Count == 0)
        {
            return;
        }

        var sstable = new SSTable(_dataDirectory, _sstableCounter++, data);
        _sstables.Add(sstable);
        _memTable.Clear();
        _commitLog.Reset();

        Console.WriteLine($"Flushed {data.Count} entries to {sstable.DirectoryPath}");

        if (_sstables.Count > 5)
        {
            Compact();
        }
    }

    public byte[]? Get(string key)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);

        lock (_lock)
        {
            var value = _memTable.Get(key);
            if (value is { Length: > 0 })
            {
                return value;
            }

            if (value is { Length: 0 })
            {
                return null;
            }

            foreach (var sstable in _sstables.AsEnumerable().Reverse())
            {
                value = sstable.Get(key);
                if (value is { Length: > 0 })
                {
                    return value;
                }

                if (value is { Length: 0 })
                {
                    return null;
                }
            }

            return null;
        }
    }

    private void Compact()
    {
        Console.WriteLine("Starting compaction...");

        var mergeCount = Math.Min(4, _sstables.Count);
        var toMerge = _sstables.Take(mergeCount).ToList();
        if (toMerge.Count < 2)
        {
            return;
        }

        var allEntries = new SortedDictionary<string, byte[]>(StringComparer.Ordinal);
        foreach (var sstable in toMerge)
        {
            foreach (var kvp in sstable.GetAll())
            {
                allEntries[kvp.Key] = kvp.Value;
            }
        }

        var newSSTable = new SSTable(_dataDirectory, _sstableCounter++, allEntries);

        foreach (var sstable in toMerge)
        {
            _sstables.Remove(sstable);
            sstable.Dispose();
            if (Directory.Exists(sstable.DirectoryPath))
            {
                Directory.Delete(sstable.DirectoryPath, recursive: true);
            }
        }

        _sstables.Add(newSSTable);
        Console.WriteLine($"Compaction complete. Merged {mergeCount} files into {newSSTable.DirectoryPath}. Entries: {newSSTable.EntryCount}");
    }

    public void Delete(string key)
    {
        Add(key, Array.Empty<byte>());
    }

    public IEnumerable<KeyValuePair<string, byte[]>> Range(string startKey, string endKey)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(startKey);
        ArgumentException.ThrowIfNullOrWhiteSpace(endKey);

        var result = new SortedDictionary<string, byte[]>(StringComparer.Ordinal);

        foreach (var kvp in _memTable.GetAll()
                     .Where(x => string.Compare(x.Key, startKey, StringComparison.Ordinal) >= 0 &&
                                 string.Compare(x.Key, endKey, StringComparison.Ordinal) <= 0))
        {
            if (kvp.Value.Length > 0)
            {
                result[kvp.Key] = kvp.Value;
            }
        }

        foreach (var sstable in _sstables.AsEnumerable().Reverse())
        {
            foreach (var kvp in sstable.GetAll()
                         .Where(x => string.Compare(x.Key, startKey, StringComparison.Ordinal) >= 0 &&
                                     string.Compare(x.Key, endKey, StringComparison.Ordinal) <= 0))
            {
                if (kvp.Value.Length > 0)
                {
                    result[kvp.Key] = kvp.Value;
                }
            }
        }

        return result.Select(x => x);
    }

    public void PrintStats()
    {
        Console.WriteLine("=== LSM Tree Statistics ===");
        Console.WriteLine($"MemTable Entries: {_memTable.Count}");
        Console.WriteLine($"MemTable Size: {_memTable.SizeInBytes} bytes");
        Console.WriteLine($"Number of SSTables: {_sstables.Count}");

        long totalEntries = 0;
        long totalSize = 0;
        foreach (var sstable in _sstables)
        {
            totalEntries += sstable.EntryCount;
            totalSize += sstable.DataFileSizeBytes;
        }

        Console.WriteLine($"Total SSTable Entries: {totalEntries}");
        Console.WriteLine($"Total SSTable Size: {totalSize} bytes");
        Console.WriteLine("=============================");
    }

    public void Dispose()
    {
        FlushMemTable();

        foreach (var sstable in _sstables)
        {
            sstable.Dispose();
        }

        _commitLog.Dispose();
    }
}
