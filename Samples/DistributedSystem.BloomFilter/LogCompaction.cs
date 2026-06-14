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
