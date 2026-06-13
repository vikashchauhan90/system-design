# Bloom Filter

A space-efficient probabilistic data structure for testing set membership with no false negatives and configurable false positives.

## Overview

A Bloom Filter is a probabilistic algorithm that answers the question "Is this element in the set?" with:
- **"Definitely NOT"** (100% certainty) — guarantees the element is not present
- **"MIGHT be"** (with false positive rate) — element might be present (could be false positive)

It uses minimal memory (~10 bytes per element) at the cost of a configurable false positive rate (~1%).

## Use Cases

### Log Compaction (Primary Use Case)
When cleaning a compacted topic (e.g., Kafka), the cleaner needs to know which keys have their latest value in which segment:

```csharp
// Instead of scanning all segments for every key:
foreach (var key in keysToClean)
{
    foreach (var segment in segments)
    {
        // Slow: scans segment metadata
        if (segment.Contains(key))
        {
            // Process key in this segment
        }
    }
}

// With Bloom Filter - 10x faster:
foreach (var segment in segments)
{
    var bloomFilter = segment.LoadBloomFilter();
    foreach (var key in keysToClean)
    {
        if (bloomFilter.MightContain(key))
        {
            // Only check this segment (false positive possible)
            val position = segment.FindOffsetByKey(key);
            if (position >= 0)
            {
                // Process key
            }
        }
        // Else: skip segment entirely (definitely not here)
    }
}
```

### Other Use Cases
- **Caching**: Avoid lookups for keys definitely not in cache
- **Database Queries**: Skip partitions that definitely don't contain a key
- **Network Routers**: Check if packet should be forwarded
- **Spell Checking**: Quick rejection of invalid words
- **Web Crawlers**: Track visited URLs efficiently
- **Deduplication**: Identify potential duplicates before expensive checks
- **CDN Content**: Determine if content is cached

## How It Works

### Algorithm Steps

1. **Hash Input**: Apply k independent hash functions to input element
2. **Set Bits**: For each hash value, set that bit position in the filter
3. **Lookup**: Check if ALL k bit positions are set
   - If ANY bit is 0 → element definitely NOT in set (no false negatives)
   - If ALL bits are 1 → element MIGHT be in set (possible false positive)

### Visual Example

```
Initial filter (all zeros):
[0, 0, 0, 0, 0, 0, 0, 0]

Add element "key1" (hash to positions 1, 3, 6):
[0, 1, 0, 1, 0, 0, 1, 0]

Add element "key2" (hash to positions 2, 4, 6):
[0, 1, 1, 1, 1, 0, 1, 0]

Check "key1" (positions 1, 3, 6):
- Position 1: 1 ✓
- Position 3: 1 ✓
- Position 6: 1 ✓
Result: MIGHT be in set ✓

Check "key3" (positions 0, 2, 5):
- Position 0: 0 ✗
Result: Definitely NOT in set ✓
```

## Implementation Details

### Basic Usage

```csharp
// Create filter with 65536 bits and 3 hash functions
var bloomFilter = new BloomFilter(65536, hashFunctions: 3);

// Add elements
bloomFilter.Add("user-123");
bloomFilter.Add("user-456");

// Check if element might be present
if (bloomFilter.MightContain("user-123"))
{
    // Proceed with expensive operation
    var user = database.GetUser("user-123");
}
else
{
    // Skip - definitely not in database
}

if (bloomFilter.MightContain("user-999"))
{
    // Might be false positive, check anyway
}
else
{
    // Definitely not in database - skip
}
```

### Optimal Creation

```csharp
// Create filter optimized for specific capacity and false positive rate
var filter = BloomFilter.CreateOptimal(
    expectedCapacity: 100000,
    falsePositiveRate: 0.01  // 1%
);

// For log compaction (1% FP rate, 10 bytes per key)
var compactionFilter = BloomFilter.CreateOptimal(
    expectedCapacity: 1000000,
    falsePositiveRate: 0.01
);
```

### Serialization

```csharp
// Save filter to disk
byte[] data = bloomFilter.ToByteArray();
File.WriteAllBytes("bloom-filter.bin", data);

// Restore filter
byte[] restored = File.ReadAllBytes("bloom-filter.bin");
var filter = BloomFilter.FromByteArray(restored);
```

### Merging Filters

```csharp
var filter1 = new BloomFilter(65536);
filter1.Add("key1");
filter1.Add("key2");

var filter2 = new BloomFilter(65536);
filter2.Add("key3");
filter2.Add("key4");

// Merge filters (logical OR)
filter1.Merge(filter2);

// filter1 now contains all keys
filter1.MightContain("key1");  // true
filter1.MightContain("key3");  // true
filter1.MightContain("key5");  // false
```

## Performance Characteristics

### Space/Accuracy Trade-off

| Expected Items | FP Rate | Bits Needed | Hash Functions | Memory | Use Case |
|---|---|---|---|---|---|
| 10,000 | 5% | 47,193 | 4 | 6 KB | Lenient filtering |
| 10,000 | 1% | 95,850 | 7 | 12 KB | Standard (log compaction) |
| 100,000 | 1% | 958,505 | 7 | 120 KB | Large datasets |
| 1,000,000 | 1% | 9,585,058 | 7 | 1.2 MB | Enterprise scale |

### Time Complexity

| Operation | Complexity | Notes |
|---|---|---|
| Add | O(k) | k = number of hash functions |
| MightContain | O(k) | k = number of hash functions |
| Merge | O(m) | m = number of bits |
| Serialize | O(m) | m = number of bits |
| Clear | O(m) | m = number of bits |

### Space Complexity

- **Memory**: `(bits / 8)` bytes for bit array + overhead
- **For 1M items, 1% FP**: ~1.2 MB
- **For 1M items, 0.1% FP**: ~1.8 MB
- **No growth with usage**: Fixed size from creation

## Key Features

- **Guaranteed No False Negatives**: If `MightContain()` returns false, element is definitely not in set
- **Configurable False Positive Rate**: Trade memory vs accuracy
- **Ultra-Low Memory**: ~1.2 bytes per item for 1% FP rate
- **Fast Operations**: O(k) where k typically 3-7
- **Mergeable**: Combine filters from different segments
- **Serializable**: Save/restore for persistence
- **Thread-Safe for Reads**: Once built, safe for concurrent reading (no synchronization needed)

## Limitations

- **No Removal**: Cannot efficiently remove elements (use Counting Bloom Filter for removal)
- **False Positives**: Will have false positives at configured rate
- **Fixed Size**: Cannot dynamically grow (need to rebuild with larger size)
- **Hash Function Dependent**: Quality depends on hash function uniformity
- **No Count**: Cannot determine how many elements were added

## Comparison with Alternatives

| Feature | Bloom Filter | Hash Set | Bit Vector | Counting BF |
|---|---|---|---|---|
| False Positives | Yes | No | N/A | Yes |
| Memory (1M items, 1% FP) | 1.2 MB | 20+ MB | 125 KB (dense only) | 4.8 MB |
| Add | O(k) | O(1) avg | O(1) | O(k) |
| Contains | O(k) | O(1) avg | O(1) | O(k) |
| Remove | N/A | O(1) avg | N/A | O(k) |
| Merge | Yes | Complex | Bitwise OR | Yes |

## Log Compaction Deep Dive

### Problem Statement
In a Kafka-like system with compacted topics, segments contain key-value pairs. During compaction:
1. Scan each key
2. Find latest value across all segments
3. Keep only latest, discard older versions

**Naive Approach**: For each segment, scan ALL keys to check if any match:
- O(keys × segments) checks
- Slow for large topics

### Bloom Filter Solution
For each segment, maintain a Bloom Filter of its keys:

```
Segment 1: [keys: 1, 5, 9]           Bloom Filter 1: [Set bits for 1, 5, 9]
Segment 2: [keys: 2, 5, 10]          Bloom Filter 2: [Set bits for 2, 5, 10]
Segment 3: [keys: 3, 6, 9]           Bloom Filter 3: [Set bits for 3, 6, 9]

Compaction scan:
- Check key 1:
  BF1.MightContain(1) → true, check segment 1 → found
  BF2.MightContain(1) → false, skip segment 2
  BF3.MightContain(1) → false, skip segment 3
  
- Check key 2:
  BF1.MightContain(2) → false, skip segment 1
  BF2.MightContain(2) → true, check segment 2 → found
  BF3.MightContain(2) → false, skip segment 3
```

**Performance Gain**:
- Without BF: Check 3 × 3 = 9 segments
- With BF: Definitely skip 4 segments, only check 5
- As scale increases (more segments): Skips dominate, near-linear instead of quadratic

## Best Practices

1. **Right-Size the Filter**: Use `CreateOptimal()` with expected capacity
2. **Choose FP Rate Wisely**:
   - 1% = Standard (good balance)
   - 0.1% = High accuracy needed
   - 5% = Memory-constrained
3. **Serialize with Headers**: Include precision metadata in serialized format
4. **Immutable After Build**: Once created, treat as immutable (thread-safe for reads)
5. **Monitor FP Rate**: Track actual false positives vs expected
6. **Merge Carefully**: Only merge filters with same parameters

## Implementation Notes

- **Double Hashing**: Uses two independent hash functions with arithmetic to generate k values
- **FNV-1a + MurmurHash**: Combination of fast FNV and good distribution from Murmur
- **Bit Packing**: Efficient byte-level bit storage and operations
- **Power-of-2 Size**: Simplifies bit index calculation with bitwise operations
