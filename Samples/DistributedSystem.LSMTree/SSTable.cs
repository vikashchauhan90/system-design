using System.Text;
using System.Text.Json;

namespace DistributedSystem.LSMTree;

public sealed class SSTable : IDisposable
{
    private readonly string _directoryPath;
    private readonly string _dataFilePath;
    private readonly string _indexFilePath;
    private readonly string _filterFilePath;
    private readonly string _summaryFilePath;
    private readonly string _statisticsFilePath;
    private readonly string _compressionInfoFilePath;
    private readonly Dictionary<string, long> _index;
    private readonly BloomFilter _filter;
    private readonly FileStream _dataFile;

    public string FilePath => _dataFilePath;
    public string DirectoryPath => _directoryPath;
    public int EntryCount => _index.Count;
    public long DataFileSizeBytes => _dataFile.Length;

    public SSTable(string directoryPath, int sequenceNumber, IEnumerable<KeyValuePair<string, byte[]>> data)
    {
        _directoryPath = Path.Combine(directoryPath, $"sstable_{sequenceNumber:D4}");
        Directory.CreateDirectory(_directoryPath);

        _dataFilePath = Path.Combine(_directoryPath, "data.db");
        _indexFilePath = Path.Combine(_directoryPath, "index.db");
        _filterFilePath = Path.Combine(_directoryPath, "filter.db");
        _summaryFilePath = Path.Combine(_directoryPath, "summary.db");
        _statisticsFilePath = Path.Combine(_directoryPath, "statistics.db");
        _compressionInfoFilePath = Path.Combine(_directoryPath, "compressionInfo.db");

        var sortedEntries = data.OrderBy(entry => entry.Key, StringComparer.Ordinal).ToList();
        _index = new Dictionary<string, long>(sortedEntries.Count, StringComparer.Ordinal);
        _filter = new BloomFilter(Math.Max(sortedEntries.Count, 1), 0.01);

        using (var stream = new FileStream(_dataFilePath, FileMode.Create, FileAccess.Write, FileShare.Read))
        using (var writer = new BinaryWriter(stream, Encoding.UTF8, leaveOpen: true))
        {
            foreach (var kvp in sortedEntries)
            {
                _filter.Add(kvp.Key);
                var position = stream.Position;
                _index[kvp.Key] = position;

                writer.Write(kvp.Key);
                writer.Write(kvp.Value.Length);
                writer.Write(kvp.Value);
            }
        }

        WriteIndexFile(sortedEntries);
        File.WriteAllBytes(_filterFilePath, _filter.ToByteArray());
        WriteSummaryFile(sortedEntries);
        WriteStatisticsFile(sortedEntries);
        File.WriteAllText(_compressionInfoFilePath, "compression=none");

        _dataFile = new FileStream(_dataFilePath, FileMode.Open, FileAccess.Read, FileShare.Read);
    }

    public SSTable(string directoryPath)
    {
        _directoryPath = directoryPath;
        _dataFilePath = Path.Combine(_directoryPath, "data.db");
        _indexFilePath = Path.Combine(_directoryPath, "index.db");
        _filterFilePath = Path.Combine(_directoryPath, "filter.db");
        _summaryFilePath = Path.Combine(_directoryPath, "summary.db");
        _statisticsFilePath = Path.Combine(_directoryPath, "statistics.db");
        _compressionInfoFilePath = Path.Combine(_directoryPath, "compressionInfo.db");

        _index = new Dictionary<string, long>(StringComparer.Ordinal);
        LoadIndex();
        _filter = BloomFilter.FromByteArray(File.ReadAllBytes(_filterFilePath));
        _dataFile = new FileStream(_dataFilePath, FileMode.Open, FileAccess.Read, FileShare.Read);
    }

    public byte[]? Get(string key)
    {
        if (!_index.TryGetValue(key, out var position))
        {
            return null;
        }

        if (!_filter.MightContain(key))
        {
            return null;
        }

        lock (_dataFile)
        {
            _dataFile.Seek(position, SeekOrigin.Begin);
            using var reader = new BinaryReader(_dataFile, Encoding.UTF8, true);

            var recordKey = reader.ReadString();
            if (!string.Equals(recordKey, key, StringComparison.Ordinal))
            {
                return null;
            }

            var valueLength = reader.ReadInt32();
            return reader.ReadBytes(valueLength);
        }
    }

    public IEnumerable<KeyValuePair<string, byte[]>> GetAll()
    {
        lock (_dataFile)
        {
            _dataFile.Seek(0, SeekOrigin.Begin);
            using var reader = new BinaryReader(_dataFile, Encoding.UTF8, true);

            while (_dataFile.Position < _dataFile.Length)
            {
                var key = reader.ReadString();
                var valueLength = reader.ReadInt32();
                var value = reader.ReadBytes(valueLength);
                yield return new KeyValuePair<string, byte[]>(key, value);
            }
        }
    }

    public bool MightContain(string key) => _filter.MightContain(key);

    public void Dispose()
    {
        _dataFile.Dispose();
    }

    private void WriteIndexFile(IEnumerable<KeyValuePair<string, byte[]>> sortedEntries)
    {
        using var stream = new FileStream(_indexFilePath, FileMode.Create, FileAccess.Write, FileShare.Read);
        using var writer = new BinaryWriter(stream, Encoding.UTF8, leaveOpen: true);

        foreach (var kvp in sortedEntries)
        {
            writer.Write(kvp.Key);
            writer.Write(_index[kvp.Key]);
        }
    }

    private void WriteSummaryFile(IReadOnlyList<KeyValuePair<string, byte[]>> sortedEntries)
    {
        var interval = Math.Max(1, sortedEntries.Count / 8);
        using var stream = new FileStream(_summaryFilePath, FileMode.Create, FileAccess.Write, FileShare.Read);
        using var writer = new BinaryWriter(stream, Encoding.UTF8, leaveOpen: true);

        for (var i = 0; i < sortedEntries.Count; i += interval)
        {
            var entry = sortedEntries[i];
            writer.Write(entry.Key);
            writer.Write(_index[entry.Key]);
        }
    }

    private void WriteStatisticsFile(IReadOnlyList<KeyValuePair<string, byte[]>> sortedEntries)
    {
        var stats = new SSTableMetadata
        {
            EntryCount = sortedEntries.Count,
            CreatedAtUtc = DateTimeOffset.UtcNow,
            MinKey = sortedEntries.FirstOrDefault().Key,
            MaxKey = sortedEntries.LastOrDefault().Key,
            DataFileSizeBytes = new FileInfo(_dataFilePath).Length
        };

        File.WriteAllText(_statisticsFilePath, JsonSerializer.Serialize(stats, new JsonSerializerOptions { WriteIndented = true }));
    }

    private void LoadIndex()
    {
        if (!File.Exists(_indexFilePath))
        {
            throw new InvalidOperationException($"Index file is missing for SSTable '{_directoryPath}'.");
        }

        using var stream = new FileStream(_indexFilePath, FileMode.Open, FileAccess.Read, FileShare.Read);
        using var reader = new BinaryReader(stream, Encoding.UTF8, leaveOpen: true);

        while (stream.Position < stream.Length)
        {
            var key = reader.ReadString();
            var offset = reader.ReadInt64();
            _index[key] = offset;
        }
    }

    private sealed class SSTableMetadata
    {
        public int EntryCount { get; set; }
        public DateTimeOffset CreatedAtUtc { get; set; }
        public string? MinKey { get; set; }
        public string? MaxKey { get; set; }
        public long DataFileSizeBytes { get; set; }
    }
}
