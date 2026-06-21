# Count-Min Sketch

A probabilistic data structure for estimating frequency counts of items in massive data streams with fixed memory usage.

**"How many times have I seen this item?"**

## Overview

Count-Min Sketch (CMS) is a space-efficient probabilistic algorithm for approximating the frequency of elements in a data stream. It can estimate counts for billions of events using only a few kilobytes of memory, making it ideal for high-throughput analytics systems.

Unlike a HashMap or Dictionary, Count-Min Sketch does not store individual keys. Instead, it stores compact frequency estimates and guarantees that estimated counts are never less than the true count.

## Use Cases

* **Top-K / Heavy Hitters Detection**: Find most frequent search queries, products, or events
* **Network Monitoring**: Track most active IP addresses or connections
* **Fraud Detection**: Detect unusual activity patterns
* **Rate Limiting**: Approximate request counts per user
* **Telemetry Systems**: Count event frequencies at scale
* **Log Analytics**: Estimate occurrence of log messages
* **Streaming Platforms**: Track trending content
* **Database Systems**: Query optimization and frequency statistics

## How It Works

### Algorithm Phases

1. **Hash Input**: Generate multiple independent hashes for the item
2. **Locate Counters**: Use each hash to select one counter per row
3. **Increment Counters**: Increase all selected counters
4. **Estimate Frequency**: Return the minimum counter value across all rows

### Data Structure Layout

The sketch consists of a matrix:

```text
Depth (d)
 ┌───────────────────────────┐
 │ 0 0 1 0 2 1 0 0 0 ...     │
 │ 0 1 0 3 1 0 2 0 0 ...     │
 │ 0 0 2 1 0 1 0 0 0 ...     │
 │ 1 0 0 2 4 0 1 0 0 ...     │
 └───────────────────────────┘
            Width (w)
```

Each row uses a different hash function.

When an element is added:

* One counter is incremented in every row.
* Frequency estimate = minimum counter value across rows.

### Why Minimum?

Hash collisions can only increase counts.

Therefore:

```text
Estimated Count ≥ Actual Count
```

Taking the minimum across rows minimizes collision error.

## Error Guarantees

Count-Min Sketch provides probabilistic guarantees:

### Width

```text
w ≈ e / ε
```

Where:

* ε = acceptable error rate
* Larger width reduces collision error

### Depth

```text
d ≈ ln(1 / δ)
```

Where:

* δ = probability of exceeding error bounds
* Larger depth increases confidence

### Error Bound

Estimated count:

```text
f̂(x) ≤ f(x) + εN
```

Where:

* f(x) = true frequency
* N = total events processed
* ε = configured error factor

Probability of exceeding this bound:

```text
≤ δ
```

## Implementation Details

```csharp
// Create Count-Min Sketch
var cms = new CountMinSketch(
    width: 2048,
    depth: 5);

// Add events
cms.Add("apple");
cms.Add("apple");
cms.Add("banana");

// Estimate frequency
var appleCount = cms.EstimateCount("apple");
var bananaCount = cms.EstimateCount("banana");

// Merge sketches
var cms2 = new CountMinSketch(2048, 5);
cms2.Add("orange");

cms.Merge(cms2);

// Serialize
var bytes = cms.ToByteArray();
var restored = CountMinSketch.FromByteArray(bytes);
```

## Key Features

* **Fixed Memory Usage**: Memory independent of number of unique keys
* **Fast Operations**: O(depth) insertion and lookup
* **Mergeable**: Combine sketches from multiple nodes
* **Serializable**: Save and restore state
* **Streaming Friendly**: Works on unbounded data streams
* **Never Underestimates**: Estimated count is always ≥ actual count
* **Distributed Analytics Ready**: Easy aggregation across services

## Complexity

| Operation     | Time Complexity  |
| ------------- | ---------------- |
| Add           | O(depth)         |
| EstimateCount | O(depth)         |
| Merge         | O(width × depth) |
| Serialize     | O(width × depth) |

## Memory Usage

Memory consumption:

```text
width × depth × sizeof(counter)
```

Examples using 32-bit counters:

| Width | Depth | Memory |
| ----- | ----- | ------ |
| 512   | 4     | 8 KB   |
| 1024  | 4     | 16 KB  |
| 2048  | 5     | 40 KB  |
| 4096  | 5     | 80 KB  |
| 8192  | 7     | 224 KB |

Even with billions of events, memory remains constant.

## Accuracy Characteristics

| Width | Depth | Approx Error | Confidence |
| ----- | ----- | ------------ | ---------- |
| 512   | 4     | ~0.5%        | 98%        |
| 1024  | 4     | ~0.25%       | 98%        |
| 2048  | 5     | ~0.13%       | 99.3%      |
| 4096  | 5     | ~0.07%       | 99.3%      |
| 8192  | 7     | ~0.03%       | 99.9%      |

Actual accuracy depends on key distribution and collision rates.

## Limitations

* **Approximate**: Results are estimates
* **Overestimation Only**: Collisions can inflate counts
* **No Exact Key Storage**: Cannot enumerate stored items
* **Deletion Support**: Standard CMS cannot decrement safely
* **Hash Quality Matters**: Poor hashes increase error

## Comparison with Other Probabilistic Structures

| Structure        | Question Answered                     |
| ---------------- | ------------------------------------- |
| Bloom Filter     | Have I seen this item?                |
| HyperLogLog      | How many unique items have I seen?    |
| Count-Min Sketch | How many times have I seen this item? |
| MinHash          | How similar are two datasets?         |
| Cuckoo Filter    | Is this item probably present?        |

## When to Use Count-Min Sketch

Use Count-Min Sketch when:

* You need approximate frequencies
* Memory must remain bounded
* The stream is extremely large
* Exact counts are unnecessary
* Aggregation across nodes is required

Use a Dictionary/HashMap when:

* Exact counts are required
* Dataset fits comfortably in memory
* Key enumeration is needed

## Demo Usage

```csharp
var cms = new CountMinSketch();

for (int i = 0; i < 1000000; i++)
{
    cms.Add("page-view");
}

var estimate = cms.EstimateCount("page-view");

// Output: approximately 1,000,000
```

## Distributed Analytics Example

```csharp
var server1 = new CountMinSketch();
var server2 = new CountMinSketch();

server1.Add("login");
server1.Add("login");

server2.Add("login");

server1.Merge(server2);

var total = server1.EstimateCount("login");

// ≈ 3
```

## Implementation Notes

* **Hashing**: FNV-1a with independent seeds
* **Counter Type**: 32-bit unsigned integers
* **Merge Strategy**: Counter-wise addition
* **Collision Handling**: Minimum across rows
* **Serialization**: Width, depth, then counter matrix

## Future Improvements

* Conservative Update Count-Min Sketch
* Count-Min-Mean Sketch
* Heavy Hitters Tracking
* Top-K Extraction
* Sliding Window Count-Min Sketch
* Time-Decayed Count-Min Sketch
* xxHash3-based Double Hashing
* Sparse Count-Min Sketch

## References

* Count-Min Sketch: An Improved Data Stream Summary (Cormode & Muthukrishnan)
* Data Stream Algorithms and Applications
* Streaming Algorithms for Massive Datasets
* Probabilistic Data Structures for Web Analytics
