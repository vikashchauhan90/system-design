using System.Diagnostics;
using DistributedSystem.BloomFilter;

namespace BloomFilterExamples;

/// <summary>
/// Examples and tests demonstrating Bloom Filter usage for log compaction and other scenarios.
/// </summary>
public static class BloomFilterExamples
{
    public static void Main(string[] args)
    {
        Console.WriteLine("=== Bloom Filter Examples ===\n");

        BasicUsageExample();
        LogCompactionExample();
        PerformanceComparison();
        SerializationExample();
        MergingExample();
        FalsePositiveRateAnalysis();
    }

    /// <summary>
    /// Basic add and contains operations
    /// </summary>
    private static void BasicUsageExample()
    {
        Console.WriteLine("1. Basic Usage Example");
        Console.WriteLine("====================");

        var filter = new BloomFilter(65536, hashFunctions: 3);

        // Add some keys
        var keys = new[] { "user-1", "user-2", "user-3", "order-100", "order-101" };
        foreach (var key in keys)
        {
            filter.Add(key);
        }

        Console.WriteLine($"Added {keys.Length} keys to filter");
        Console.WriteLine($"Memory usage: {filter.MemoryUsageBytes} bytes");
        Console.WriteLine($"Estimated false positive rate: {filter.EstimatedFalsePositiveRate:P2}");
        Console.WriteLine($"Estimated element count: {filter.EstimateElementCount()}");

        // Test lookups
        Console.WriteLine("\nLookup tests:");
        foreach (var key in keys)
        {
            var found = filter.MightContain(key);
            Console.WriteLine($"  {key}: {(found ? "might be in set ✓" : "definitely not ✗")}");
        }

        // Test non-existent key
        var nonExistent = "user-999";
        if (filter.MightContain(nonExistent))
        {
            Console.WriteLine($"  {nonExistent}: might be in set (possible false positive)");
        }
        else
        {
            Console.WriteLine($"  {nonExistent}: definitely not in set ✓");
        }

        Console.WriteLine();
    }

    /// <summary>
    /// Log compaction scenario - the primary use case
    /// </summary>
    private static void LogCompactionExample()
    {
        Console.WriteLine("2. Log Compaction Example");
        Console.WriteLine("========================");

        // Simulate Kafka segments with bloom filters
        var segment1Keys = new[] { "product-1", "product-2", "product-3", "product-5" };
        var segment2Keys = new[] { "product-2", "product-4", "product-6" };
        var segment3Keys = new[] { "product-1", "product-3", "product-7", "product-8" };

        var filter1 = BloomFilter.CreateOptimal(100000, 0.01);
        var filter2 = BloomFilter.CreateOptimal(100000, 0.01);
        var filter3 = BloomFilter.CreateOptimal(100000, 0.01);

        foreach (var key in segment1Keys) filter1.Add(key);
        foreach (var key in segment2Keys) filter2.Add(key);
        foreach (var key in segment3Keys) filter3.Add(key);

        var filters = new[] { filter1, filter2, filter3 };

        Console.WriteLine("Segment composition:");
        Console.WriteLine($"  Segment 1: {string.Join(", ", segment1Keys)}");
        Console.WriteLine($"  Segment 2: {string.Join(", ", segment2Keys)}");
        Console.WriteLine($"  Segment 3: {string.Join(", ", segment3Keys)}");
        Console.WriteLine();

        // Compaction: find which segments contain each key
        var allKeys = new[] { "product-1", "product-2", "product-3", "product-4", "product-5", "product-6", "product-7", "product-8", "product-99" };

        Console.WriteLine("Compaction scan (finding segments to check for each key):");
        foreach (var key in allKeys)
        {
            var segmentsToCheck = new List<int>();
            for (int i = 0; i < filters.Length; i++)
            {
                if (filters[i].MightContain(key))
                {
                    segmentsToCheck.Add(i + 1);
                }
            }

            if (segmentsToCheck.Count == 0)
            {
                Console.WriteLine($"  {key}: skip all segments (definitely not present)");
            }
            else
            {
                Console.WriteLine($"  {key}: check segment(s) {string.Join(", ", segmentsToCheck)}");
            }
        }

        Console.WriteLine();
    }

    /// <summary>
    /// Compare performance: Bloom Filter vs naive list scanning
    /// </summary>
    private static void PerformanceComparison()
    {
        Console.WriteLine("3. Performance Comparison");
        Console.WriteLine("=======================");

        const int itemCount = 100000;
        const int lookupCount = 100000;

        // Create test data
        var items = Enumerable.Range(0, itemCount).Select(i => $"key-{i}").ToList();
        var lookupsExist = items.Take(lookupCount / 2).ToList();
        var lookupsNonExist = Enumerable.Range(itemCount, lookupCount / 2).Select(i => $"key-{i}").ToList();
        var lookups = lookupsExist.Concat(lookupsNonExist).OrderBy(_ => Random.Shared.Next()).ToList();

        // Test 1: HashSet (baseline)
        var hashSet = new HashSet<string>(items);
        var sw = Stopwatch.StartNew();
        var hashSetHits = 0;
        foreach (var lookup in lookups)
        {
            if (hashSet.Contains(lookup))
                hashSetHits++;
        }
        sw.Stop();
        var hashSetTime = sw.ElapsedMilliseconds;

        // Test 2: Bloom Filter
        var bloomFilter = BloomFilter.CreateOptimal(itemCount, 0.01);
        foreach (var item in items)
        {
            bloomFilter.Add(item);
        }

        sw = Stopwatch.StartNew();
        var bloomHits = 0;
        foreach (var lookup in lookups)
        {
            if (bloomFilter.MightContain(lookup))
                bloomHits++;
        }
        sw.Stop();
        var bloomTime = sw.ElapsedMilliseconds;

        // Test 3: List.Contains (slow baseline)
        sw = Stopwatch.StartNew();
        var listHits = 0;
        foreach (var lookup in lookups)
        {
            if (items.Contains(lookup))
                listHits++;
        }
        sw.Stop();
        var listTime = sw.ElapsedMilliseconds;

        Console.WriteLine($"Items: {itemCount}, Lookups: {lookupCount}\n");
        Console.WriteLine($"HashSet:      {hashSetTime} ms ({hashSetHits} hits) - Memory: ~{hashSet.Count * 16} bytes");
        Console.WriteLine($"Bloom Filter: {bloomTime} ms ({bloomHits} hits) - Memory: {bloomFilter.MemoryUsageBytes} bytes");
        Console.WriteLine($"List.Contains: {listTime} ms ({listHits} hits) - Memory: ~{items.Count * 24} bytes");
        Console.WriteLine();
        Console.WriteLine($"Bloom Filter is {(double)hashSetTime / bloomTime:F1}x faster than HashSet");
        Console.WriteLine($"Bloom Filter is {(double)listTime / bloomTime:F0}x faster than List.Contains");
        Console.WriteLine($"Memory savings vs HashSet: {(1 - (double)bloomFilter.MemoryUsageBytes / (hashSet.Count * 16)) * 100:F1}%");
        Console.WriteLine();
    }

    /// <summary>
    /// Serialization and deserialization
    /// </summary>
    private static void SerializationExample()
    {
        Console.WriteLine("4. Serialization Example");
        Console.WriteLine("=======================");

        // Create and populate filter
        var filter = BloomFilter.CreateOptimal(10000, 0.01);
        var testKeys = new[] { "key-1", "key-2", "key-3", "key-100", "key-200" };
        foreach (var key in testKeys)
        {
            filter.Add(key);
        }

        Console.WriteLine($"Original filter:");
        Console.WriteLine($"  Size: {filter.Size} bits");
        Console.WriteLine($"  Hash functions: {filter.HashFunctions}");
        Console.WriteLine($"  Memory: {filter.MemoryUsageBytes} bytes");

        // Serialize
        var serialized = filter.ToByteArray();
        Console.WriteLine($"\nSerialized to {serialized.Length} bytes");

        // Simulate save/load
        var loadedData = serialized; // In real scenario: read from file

        // Deserialize
        var restoredFilter = BloomFilter.FromByteArray(loadedData);
        Console.WriteLine($"\nRestored filter:");
        Console.WriteLine($"  Size: {restoredFilter.Size} bits");
        Console.WriteLine($"  Hash functions: {restoredFilter.HashFunctions}");
        Console.WriteLine($"  Memory: {restoredFilter.MemoryUsageBytes} bytes");

        // Verify restored filter works
        Console.WriteLine("\nVerification (checking original keys in restored filter):");
        foreach (var key in testKeys)
        {
            var found = restoredFilter.MightContain(key);
            Console.WriteLine($"  {key}: {(found ? "found ✓" : "not found ✗")}");
        }

        Console.WriteLine();
    }

    /// <summary>
    /// Merging filters from different segments
    /// </summary>
    private static void MergingExample()
    {
        Console.WriteLine("5. Merging Example (Union of Filters)");
        Console.WriteLine("====================================");

        var filter1 = new BloomFilter(16384);
        var filter2 = new BloomFilter(16384);

        // Add to filter1
        filter1.Add("user-1");
        filter1.Add("user-2");
        filter1.Add("user-3");

        // Add to filter2
        filter2.Add("user-4");
        filter2.Add("user-5");

        Console.WriteLine("Filter 1 contains: user-1, user-2, user-3");
        Console.WriteLine("Filter 2 contains: user-4, user-5");

        // Verify before merge
        Console.WriteLine("\nBefore merge:");
        Console.WriteLine($"  Filter1.MightContain('user-4'): {filter1.MightContain("user-4")}");
        Console.WriteLine($"  Filter2.MightContain('user-1'): {filter2.MightContain("user-1")}");

        // Merge
        filter1.Merge(filter2);

        Console.WriteLine("\nAfter merge (filter1 merged with filter2):");
        Console.WriteLine($"  Filter1.MightContain('user-1'): {filter1.MightContain("user-1")}");
        Console.WriteLine($"  Filter1.MightContain('user-4'): {filter1.MightContain("user-4")}");
        Console.WriteLine($"  Filter1.MightContain('user-6'): {filter1.MightContain("user-6")}");

        Console.WriteLine();
    }

    /// <summary>
    /// Analyze false positive rates with different configurations
    /// </summary>
    private static void FalsePositiveRateAnalysis()
    {
        Console.WriteLine("6. False Positive Rate Analysis");
        Console.WriteLine("==============================");

        const int itemCount = 10000;
        const int testCount = 100000;

        Console.WriteLine($"Test: Add {itemCount} items, test {testCount} random keys\n");

        var configurations = new[]
        {
            (size: 32768, hf: 3),
            (size: 65536, hf: 3),
            (size: 65536, hf: 5),
            (size: 131072, hf: 5),
            (size: 131072, hf: 7),
        };

        foreach (var (size, hf) in configurations)
        {
            var filter = new BloomFilter(size, hf);

            // Add items
            for (int i = 0; i < itemCount; i++)
            {
                filter.Add($"item-{i}");
            }

            // Test false positives
            var falsePositives = 0;
            for (int i = itemCount; i < itemCount + testCount; i++)
            {
                if (filter.MightContain($"item-{i}"))
                {
                    falsePositives++;
                }
            }

            var actualFPRate = (double)falsePositives / testCount;
            var memKb = filter.MemoryUsageBytes / 1024.0;

            Console.WriteLine($"Size: {size} bits, Hash Functions: {hf}");
            Console.WriteLine($"  Memory: {memKb:F2} KB");
            Console.WriteLine($"  Theoretical FP rate: {filter.EstimatedFalsePositiveRate:P3}");
            Console.WriteLine($"  Measured FP rate: {actualFPRate:P3} ({falsePositives} / {testCount})");
            Console.WriteLine();
        }
    }
}
