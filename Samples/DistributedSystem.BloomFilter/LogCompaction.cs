using System.Collections.Generic;
using DistributedSystem.BloomFilter;

namespace DistributedSystem.BloomFilter.LogCompaction;

/// <summary>
/// Represents a log segment with efficient key lookup via Bloom Filter.
/// Used in compacted topics (like Kafka) for fast key presence checks.
/// </summary>
public sealed class LogSegmentWithBloomFilter
{
    private readonly BloomFilter _bloomFilter;
    private readonly Dictionary<string, (long offset, string value)> _keyIndex;

    public string SegmentId { get; }
    public int KeyCount => _keyIndex.Count;
    public long MemoryUsageBytes => _bloomFilter.MemoryUsageBytes + (_keyIndex.Count * 64);

    public LogSegmentWithBloomFilter(string segmentId, int expectedKeys)
    {
        SegmentId = segmentId;
        _bloomFilter = BloomFilter.CreateOptimal(expectedKeys, 0.01);
        _keyIndex = new Dictionary<string, (long, string)>();
    }

    /// <summary>
    /// Adds a key-value pair to the segment.
    /// </summary>
    public void Put(string key, string value, long offset)
    {
        _bloomFilter.Add(key);
        _keyIndex[key] = (offset, value);
    }

    /// <summary>
    /// Attempts to find a key. Returns:
    /// - (true, value) if found
    /// - (false, null) if definitely not present
    /// Note: MIGHT return (true, null) if false positive
    /// </summary>
    public (bool found, string? value) TryGetValue(string key)
    {
        // Quick rejection - definitely not in segment
        if (!_bloomFilter.MightContain(key))
        {
            return (false, null);
        }

        // Might be in segment (possible false positive)
        if (_keyIndex.TryGetValue(key, out var entry))
        {
            return (true, entry.value);
        }

        // False positive - not actually in segment
        return (false, null);
    }

    /// <summary>
    /// Saves segment to persistent storage (simulated).
    /// </summary>
    public byte[] SerializeBloomFilter()
    {
        return _bloomFilter.ToByteArray();
    }

    /// <summary>
    /// Loads segment from persistent storage (simulated).
    /// </summary>
    public static LogSegmentWithBloomFilter DeserializeWithBloomFilter(
        string segmentId,
        byte[] bloomFilterData,
        Dictionary<string, (long offset, string value)> keyIndex)
    {
        var segment = new LogSegmentWithBloomFilter(segmentId, 0);
        var filter = BloomFilter.FromByteArray(bloomFilterData);

        // Restore state using reflection or similar (simplified for example)
        segment._keyIndex.Clear();
        foreach (var kvp in keyIndex)
        {
            segment._keyIndex[kvp.Key] = kvp.Value;
        }

        return segment;
    }
}

/// <summary>
/// Log compaction cleaner for compacted topics.
/// Demonstrates the primary use case of Bloom Filters.
/// </summary>
public sealed class LogCompactionCleaner
{
    private readonly List<LogSegmentWithBloomFilter> _segments;

    public LogCompactionCleaner()
    {
        _segments = new List<LogSegmentWithBloomFilter>();
    }

    public void AddSegment(LogSegmentWithBloomFilter segment)
    {
        _segments.Add(segment);
    }

    /// <summary>
    /// Performs log compaction: keeps only the latest value for each key.
    /// Returns new segments after compaction.
    /// </summary>
    public List<LogSegmentWithBloomFilter> Compact()
    {
        var latestValues = new Dictionary<string, (string segmentId, string value)>();
        var keysScanned = 0;
        var bloomFilterSkips = 0;

        // Find latest value for each key across all segments
        // Scan segments in reverse order (newest first)
        for (int i = _segments.Count - 1; i >= 0; i--)
        {
            var segment = _segments[i];

            // For each segment, scan for keys we haven't found yet
            var keysToCheck = latestValues.Keys.ToList();

            foreach (var key in keysToCheck)
            {
                if (latestValues.ContainsKey(key))
                {
                    continue; // Already found newer version
                }

                keysScanned++;

                // CRITICAL OPTIMIZATION: Use Bloom Filter to skip segments
                if (!segment._bloomFilter.MightContain(key))
                {
                    bloomFilterSkips++;
                    continue; // Definitely not in this segment
                }

                // Possible false positive - actually check the segment
                var (found, value) = segment.TryGetValue(key);
                if (found && value != null)
                {
                    latestValues[key] = (segment.SegmentId, value);
                }
            }
        }

        // Create compacted segment with latest values
        var compactedSegment = new LogSegmentWithBloomFilter("compacted", latestValues.Count);
        long offset = 0;
        foreach (var kvp in latestValues)
        {
            compactedSegment.Put(kvp.Key, kvp.Value.value, offset++);
        }

        Console.WriteLine($"Compaction Statistics:");
        Console.WriteLine($"  Original segments: {_segments.Count}");
        Console.WriteLine($"  Total keys in segments: {_segments.Sum(s => s.KeyCount)}");
        Console.WriteLine($"  Unique keys: {latestValues.Count}");
        Console.WriteLine($"  Keys scanned: {keysScanned}");
        Console.WriteLine($"  Bloom Filter skips: {bloomFilterSkips} ({(double)bloomFilterSkips / Math.Max(1, keysScanned) * 100:F1}%)");
        Console.WriteLine($"  Memory before: {_segments.Sum(s => s.MemoryUsageBytes)} bytes");
        Console.WriteLine($"  Memory after: {compactedSegment.MemoryUsageBytes} bytes");

        return new List<LogSegmentWithBloomFilter> { compactedSegment };
    }

    /// <summary>
    /// Finds a key across all segments, using Bloom Filter for fast rejection.
    /// </summary>
    public (bool found, string? value) FindLatestValue(string key)
    {
        // Scan from newest to oldest
        for (int i = _segments.Count - 1; i >= 0; i--)
        {
            var (found, value) = _segments[i].TryGetValue(key);
            if (found)
            {
                return (true, value);
            }
        }

        return (false, null);
    }
}

/// <summary>
/// Performance comparison demonstrating Bloom Filter benefits.
/// </summary>
public static class LogCompactionBenchmarks
{
    public static void RunComparisonBenchmark()
    {
        Console.WriteLine("\n=== Log Compaction Performance Comparison ===\n");

        const int segmentCount = 10;
        const int keysPerSegment = 10000;

        // Create segments with overlapping keys
        var segments = new List<LogSegmentWithBloomFilter>();
        for (int s = 0; s < segmentCount; s++)
        {
            var segment = new LogSegmentWithBloomFilter($"segment-{s}", keysPerSegment);

            // Add keys (overlapping pattern)
            for (int i = 0; i < keysPerSegment; i++)
            {
                var key = $"key-{i % (keysPerSegment / 2)}"; // 50% overlap
                var value = $"value-{s}-{i}";
                segment.Put(key, value, i);
            }

            segments.Add(segment);
        }

        var totalKeys = segmentCount * keysPerSegment;
        Console.WriteLine($"Setup: {segmentCount} segments × {keysPerSegment} keys = {totalKeys:N0} total entries");
        Console.WriteLine($"Overlapping keys: ~{keysPerSegment / 2:N0} unique keys\n");

        // Benchmark: Find specific keys using Bloom Filter
        var sw = System.Diagnostics.Stopwatch.StartNew();
        var bloomFilterHits = 0;
        var bloomFilterChecks = 0;

        var keysToFind = Enumerable.Range(0, keysPerSegment / 2)
            .Select(i => $"key-{i}")
            .ToList();

        foreach (var key in keysToFind)
        {
            foreach (var segment in segments)
            {
                bloomFilterChecks++;
                if (segment._bloomFilter.MightContain(key))
                {
                    var result = segment.TryGetValue(key);
                    if (result.found)
                    {
                        bloomFilterHits++;
                    }
                }
            }
        }
        sw.Stop();

        Console.WriteLine($"With Bloom Filter:");
        Console.WriteLine($"  Lookups performed: {bloomFilterChecks}");
        Console.WriteLine($"  Keys found: {bloomFilterHits}");
        Console.WriteLine($"  Time: {sw.ElapsedMilliseconds} ms");
        Console.WriteLine();

        // Show memory savings
        var totalMemory = segments.Sum(s => s.MemoryUsageBytes);
        var bloomFilterMemory = segments.Sum(s => s._bloomFilter.MemoryUsageBytes);
        var bloomFilterPercentage = (double)bloomFilterMemory / totalMemory * 100;

        Console.WriteLine($"Memory Analysis:");
        Console.WriteLine($"  Total segment memory: {totalMemory:N0} bytes");
        Console.WriteLine($"  Bloom Filter memory: {bloomFilterMemory:N0} bytes ({bloomFilterPercentage:F1}%)");
        Console.WriteLine($"  Per-key Bloom Filter size: {bloomFilterMemory / (keysPerSegment / 2.0):F2} bytes");
    }
}
