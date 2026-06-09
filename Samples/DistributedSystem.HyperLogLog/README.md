# HyperLogLog

A probabilistic data structure for estimating cardinality (distinct count) of very large datasets with minimal memory overhead.

## Overview

HyperLogLog is a space-efficient probabilistic algorithm for approximating the number of distinct elements in a dataset. It uses only ~1.5 kilobytes of memory per HyperLogLog instance to estimate cardinalities up to billions of distinct elements with ~2% error.

## Use Cases

- **Unique Visitor Counting**: Website analytics (unique users per day/month)
- **Data Deduplication**: Detecting duplicate records in streams
- **Network Monitoring**: Counting unique source IPs, ports, or flows
- **Search Engines**: Estimating unique query volume
- **Database Query Optimization**: Join selectivity estimation
- **Stream Processing**: Cardinality estimation on data streams
- **Redis HyperLogLog**: Built-in data type for counting distinct elements
- **Real-time Analytics**: Memory-efficient cardinality tracking

## How It Works

### Algorithm Phases

1. **Hash Input**: Hash incoming element to 64-bit value using FNV-1a
2. **Extract Bucket Index**: Use first `p` bits as index into registers (0 to 2^p - 1)
3. **Count Leading Zeros**: On remaining bits, count leading zero bits + 1 (rho value)
4. **Update Register**: Store maximum rho value for that bucket
5. **Estimate Cardinality**: Use harmonic mean of registers with bias correction

### Precision Parameter

- **`p` (precision)**: Number of bits for register indexing (typically 4-16)
- **Registers**: 2^p bytes required
- **Memory**: ~1.5 KB for p=14 (2^14 = 16,384 registers)
- **Accuracy**: Standard error ≈ 1.04 / √m (m = 2^p)
  - p=10: ~3.3% error, 1 KB
  - p=12: ~1.6% error, 4 KB
  - p=14: ~0.8% error, 16 KB
  - p=16: ~0.4% error, 64 KB

### Cardinality Estimation

The algorithm estimates cardinality using:

1. **Raw Estimate**: `E = α × m² / Σ(2^(-M[i]))`
   - α = calibration constant based on m
   - m = number of registers
   - M[i] = value in register i

2. **Small Range Correction**: If estimate ≤ 2.5m and empty registers exist:
   - `E = m × ln(m / V)` where V = number of zero registers

3. **Large Range Correction**: If estimate > 2^32 / 30:
   - `E = -2^32 × ln(1 - E / 2^32)`

## Implementation Details

```csharp
// Create HyperLogLog with precision (4-16)
var hll = new HyperLogLog(precision: 14);

// Add elements
hll.Add("user-123");
hll.Add("user-456");
hll.Add("user-123");  // Duplicate - counted once

// Estimate cardinality
var count = hll.Count();  // ≈ 2

// Merge two HyperLogLog instances
var hll2 = new HyperLogLog(14);
hll2.Add("user-789");
hll.Merge(hll2);

// Serialize for storage/transmission
var bytes = hll.ToByteArray();
var restored = HyperLogLog.FromByteArray(bytes);
```

## Key Features

- **Ultra-Low Memory**: ~1.5 KB to ~64 KB depending on precision
- **Fast Operations**: O(1) add, O(1) count
- **Mergeable**: Combine HyperLogLog instances for union cardinality
- **Serializable**: Save and restore state
- **Provably Accurate**: Mathematical error bounds
- **Parallel-Friendly**: Register updates are independent

## Accuracy Characteristics

| Precision | Memory  | Std Error | Use Case                |
|-----------|---------|-----------|------------------------|
| 4         | 16 B    | 26%       | Coarse estimates        |
| 8         | 256 B   | 8.1%      | Basic counting          |
| 10        | 1 KB    | 3.3%      | Moderate accuracy       |
| 12        | 4 KB    | 1.6%      | Good accuracy           |
| 14        | 16 KB   | 0.81%     | High accuracy (default) |
| 16        | 64 KB   | 0.41%     | Very high accuracy      |

## Limitations

- **Approximate**: Not exact count, trades accuracy for memory
- **One-way**: Cannot retrieve individual elements
- **Removal**: Cannot remove elements (add-only)
- **Small Cardinalities**: Less beneficial for small datasets (use exact sets instead)

## Improvements & Alternatives

- **T-Digest**: Better tail accuracy for percentile estimation
- **Count-Min Sketch**: For frequency estimation (top-k elements)
- **Probabilistic Counting**: Earlier algorithm, requires more memory
- **Exact Set**: Use HashSet for small cardinalities where memory isn't critical

## Demo Usage

The implementation includes efficient hashing, register management, and cardinality estimation. Example usage:

```csharp
var hll = new HyperLogLog(14);
for (int i = 0; i < 100000; i++)
{
    hll.Add($"element-{i}");
}

var estimate = hll.Count();
// Output: ~100000 with ~0.81% error
```

## Implementation Notes

- **Hash Function**: FNV-1a 64-bit for good distribution
- **Leading Zero Count**: Uses `BitOperations.LeadingZeroCount`
- **Alpha Calibration**: Dynamic alpha based on register count
- **Register Width**: Single byte per register (values 0-64)

## References

- [HyperLogLog Paper](http://algo.inria.fr/flajolet/Publications/FlFuGaMe07.pdf) - Flajolet et al.
- [Redis PFADD/PFCOUNT](https://redis.io/commands/pfadd/)
- [Google BigQuery Approximate COUNT(DISTINCT)](https://cloud.google.com/bigquery/docs/reference/standard-sql/approximate_aggregate_functions)
- [Probabilistic Data Structures](https://blog.turi.com/state-of-the-art-data-structures/)
