using System.Diagnostics;
using DistributedSystem.BloomFilter;

namespace DistributedSystem.HyperLogLog;

/// <summary>
/// HyperLogLog examples demonstrating cardinality estimation for distributed systems.
/// Used for: unique visitor counting, deduplication, stream processing, etc.
/// </summary>
public static class HyperLogLogExamples
{
    public static void Main(string[] args)
    {
        Console.WriteLine("=== HyperLogLog Examples ===\n");

        BasicUsageExample();
        UniqueVisitorCountingExample();
        ComparisonWithHashSet();
        MergingAndDistributedProcessing();
        SerializationExample();
        AccuracyAnalysis();
        BloomFilterVsHyperLogLogComparison();
    }

    /// <summary>
    /// Basic add and cardinality counting
    /// </summary>
    private static void BasicUsageExample()
    {
        Console.WriteLine("1. Basic Usage Example");
        Console.WriteLine("====================");

        // Create HyperLogLog with precision 14 (default)
        var hll = new HyperLogLog(precision: 14);

        Console.WriteLine($"Created HyperLogLog with precision: {hll.Precision}");
        Console.WriteLine($"Register count: {hll.RegisterCount}");
        Console.WriteLine($"Memory usage: {hll.ToByteArray().Length} bytes\n");

        // Add some elements (including duplicates)
        var elements = new[] 
        { 
            "user-1", "user-2", "user-3", "user-1", "user-4", 
            "user-2", "user-5", "user-1", "user-6" 
        };

        foreach (var element in elements)
        {
            hll.Add(element);
        }

        Console.WriteLine($"Added {elements.Length} elements (7 unique)");
        Console.WriteLine($"Estimated cardinality: {hll.Count():F0}");
        Console.WriteLine($"Actual unique elements: 7");
        Console.WriteLine();
    }

    /// <summary>
    /// Unique visitor counting - core use case
    /// </summary>
    private static void UniqueVisitorCountingExample()
    {
        Console.WriteLine("2. Unique Visitor Counting (Website Analytics)");
        Console.WriteLine("=============================================");

        // Simulate daily traffic
        var days = new[] { "Monday", "Tuesday", "Wednesday" };
        var dailyHyperLogLogs = new Dictionary<string, HyperLogLog>();

        // Simulate visitors on each day
        var allVisitors = new List<string>();
        for (int day = 0; day < days.Length; day++)
        {
            var hll = new HyperLogLog(precision: 14);
            var dayName = days[day];

            // Generate visitor sessions (with realistic repeats)
            var visitorsToday = new HashSet<string>();
            for (int i = 0; i < 5000; i++)
            {
                var visitorId = $"visitor-{i % 3000}"; // 3000 unique, some repeats daily
                hll.Add(visitorId);
                visitorsToday.Add(visitorId);
            }

            dailyHyperLogLogs[dayName] = hll;
            allVisitors.AddRange(visitorsToday);

            Console.WriteLine($"{dayName}:");
            Console.WriteLine($"  Sessions: 5000");
            Console.WriteLine($"  HyperLogLog estimate: {hll.Count():F0}");
            Console.WriteLine($"  Actual unique: {visitorsToday.Count}");
            Console.WriteLine($"  Error: {Math.Abs(hll.Count() - visitorsToday.Count) / visitorsToday.Count * 100:F2}%");
        }

        Console.WriteLine();

        // Merge for weekly total
        var weeklyHll = new HyperLogLog(14);
        foreach (var hll in dailyHyperLogLogs.Values)
        {
            weeklyHll.Merge(hll);
        }

        var uniqueWeekly = new HashSet<string>(allVisitors).Count;
        Console.WriteLine($"Weekly Summary:");
        Console.WriteLine($"  Unique visitors estimate: {weeklyHll.Count():F0}");
        Console.WriteLine($"  Actual unique: {uniqueWeekly}");
        Console.WriteLine($"  Error: {Math.Abs(weeklyHll.Count() - uniqueWeekly) / uniqueWeekly * 100:F2}%");
        Console.WriteLine();
    }

    /// <summary>
    /// Compare HyperLogLog memory vs HashSet
    /// </summary>
    private static void ComparisonWithHashSet()
    {
        Console.WriteLine("3. Memory Efficiency Comparison");
        Console.WriteLine("==============================");

        const int itemCount = 1000000; // 1 million items
        const int uniqueCount = 100000; // 100k unique

        // Create HyperLogLog
        var hll = new HyperLogLog(precision: 14);
        for (int i = 0; i < itemCount; i++)
        {
            hll.Add($"item-{i % uniqueCount}");
        }

        var hllData = hll.ToByteArray();

        // Create HashSet for comparison
        var hashSet = new HashSet<string>();
        for (int i = 0; i < itemCount; i++)
        {
            hashSet.Add($"item-{i % uniqueCount}");
        }

        Console.WriteLine($"Tracking {itemCount:N0} items with {uniqueCount:N0} unique values:\n");
        Console.WriteLine($"HyperLogLog:");
        Console.WriteLine($"  Memory: {hllData.Length:N0} bytes");
        Console.WriteLine($"  Estimate: {hll.Count():F0}");
        Console.WriteLine($"  Error: ~{1.04 / Math.Sqrt(hll.RegisterCount) * 100:F2}%");
        Console.WriteLine();

        Console.WriteLine($"HashSet:");
        // Rough estimation: 16 bytes per reference + string overhead
        var hashSetMemory = hashSet.Count * 50; // Approximate
        Console.WriteLine($"  Memory: ~{hashSetMemory:N0} bytes");
        Console.WriteLine($"  Exact count: {hashSet.Count}");
        Console.WriteLine($"  Error: 0%");
        Console.WriteLine();

        Console.WriteLine($"Memory savings: {(1 - (double)hllData.Length / hashSetMemory) * 100:F1}%");
        Console.WriteLine();
    }

    /// <summary>
    /// Distributed processing with merging
    /// </summary>
    private static void MergingAndDistributedProcessing()
    {
        Console.WriteLine("4. Distributed Processing (Multi-Node Cardinality)");
        Console.WriteLine("=================================================");

        // Simulate data from multiple sources/nodes
        var nodes = new[] { "Node-A", "Node-B", "Node-C" };
        var nodeCounters = new Dictionary<string, HyperLogLog>();

        Console.WriteLine("Local cardinality estimates from each node:");

        var allItems = new HashSet<string>();

        foreach (var node in nodes)
        {
            var hll = new HyperLogLog(14);

            // Each node sees different subset of data
            for (int i = 0; i < 10000; i++)
            {
                var itemId = $"item-{i % 8000}"; // 8000 unique globally
                hll.Add(itemId);
                allItems.Add(itemId);
            }

            nodeCounters[node] = hll;
            Console.WriteLine($"  {node}: {hll.Count():F0} unique (10000 items processed)");
        }

        Console.WriteLine();

        // Combine estimates from all nodes
        var globalHll = new HyperLogLog(14);
        foreach (var hll in nodeCounters.Values)
        {
            globalHll.Merge(hll);
        }

        var actualUnique = allItems.Count;
        Console.WriteLine($"Global cardinality (merged):");
        Console.WriteLine($"  HyperLogLog estimate: {globalHll.Count():F0}");
        Console.WriteLine($"  Actual unique: {actualUnique}");
        Console.WriteLine($"  Error: {Math.Abs(globalHll.Count() - actualUnique) / actualUnique * 100:F2}%");
        Console.WriteLine();
    }

    /// <summary>
    /// Serialization for storage/transmission
    /// </summary>
    private static void SerializationExample()
    {
        Console.WriteLine("5. Serialization Example");
        Console.WriteLine("=======================");

        var hll = new HyperLogLog(precision: 12);

        // Add data
        for (int i = 0; i < 10000; i++)
        {
            hll.Add($"data-{i}");
        }

        var originalCount = hll.Count();
        Console.WriteLine($"Original count: {originalCount:F0}");

        // Serialize
        var data = hll.ToByteArray();
        Console.WriteLine($"Serialized size: {data.Length} bytes");

        // Simulate save/load
        var loadedData = data;

        // Deserialize
        var restoredHll = HyperLogLog.FromByteArray(loadedData);
        var restoredCount = restoredHll.Count();

        Console.WriteLine($"Restored count: {restoredCount:F0}");
        Console.WriteLine($"Match: {Math.Abs(originalCount - restoredCount) < 1}");
        Console.WriteLine();
    }

    /// <summary>
    /// Analyze accuracy with different precisions
    /// </summary>
    private static void AccuracyAnalysis()
    {
        Console.WriteLine("6. Precision vs Accuracy Analysis");
        Console.WriteLine("=================================\n");

        const int testCardinality = 100000;
        var precisions = new[] { 8, 10, 12, 14, 16 };

        Console.WriteLine($"Test: Estimate cardinality of {testCardinality:N0} unique items\n");
        Console.WriteLine("Precision | Registers | Memory  | Estimate | Error%");
        Console.WriteLine("----------|-----------|---------|----------|--------");

        foreach (var precision in precisions)
        {
            var hll = new HyperLogLog(precision);

            // Add items
            for (int i = 0; i < testCardinality; i++)
            {
                hll.Add($"item-{i}");
            }

            var estimate = hll.Count();
            var error = Math.Abs(estimate - testCardinality) / testCardinality * 100;
            var memory = hll.ToByteArray().Length;
            var registers = hll.RegisterCount;

            Console.WriteLine($"{precision,9} | {registers,9} | {memory,7} | {estimate,8:F0} | {error,6:F2}%");
        }

        Console.WriteLine();
    }

    /// <summary>
    /// Bloom Filter vs HyperLogLog - Different purposes
    /// </summary>
    private static void BloomFilterVsHyperLogLogComparison()
    {
        Console.WriteLine("7. Bloom Filter vs HyperLogLog Comparison");
        Console.WriteLine("========================================\n");

        Console.WriteLine("Purpose Comparison:\n");

        Console.WriteLine("BLOOM FILTER - Tests Set Membership");
        Console.WriteLine("  Question: 'Is this element in the set?'");
        Console.WriteLine("  Answer: Definitely NOT or MIGHT be");
        Console.WriteLine("  Use case: Log compaction, caching, deduplication");
        Console.WriteLine("  Output: Boolean (true/false)");
        Console.WriteLine();

        Console.WriteLine("HYPERLOGLOG - Estimates Cardinality");
        Console.WriteLine("  Question: 'How many unique elements are there?'");
        Console.WriteLine("  Answer: Approximate count with error bounds");
        Console.WriteLine("  Use case: Unique visitors, stream cardinality, dedup counting");
        Console.WriteLine("  Output: Estimate with ~1% error");
        Console.WriteLine();

        // Practical example
        Console.WriteLine("Practical Example - Web Request Log Analysis:");
        Console.WriteLine();

        var requests = new List<string>();
        var userSet = new HashSet<string>();

        // Generate 10000 requests from 1000 unique users
        for (int i = 0; i < 10000; i++)
        {
            var userId = $"user-{i % 1000}";
            requests.Add(userId);
            userSet.Add(userId);
        }

        // Bloom Filter - "Should we log this request?"
        var bloomFilter = BloomFilter.CreateOptimal(1000, 0.01);
        var bannedUsers = new[] { "user-999", "user-500", "user-123" };
        foreach (var banned in bannedUsers)
        {
            bloomFilter.Add(banned);
        }

        Console.WriteLine("Bloom Filter Check (are users in ban list?):");
        foreach (var userId in new[] { "user-100", "user-500", "user-1000" })
        {
            if (bloomFilter.MightContain(userId))
            {
                Console.WriteLine($"  {userId}: might be banned - investigate");
            }
            else
            {
                Console.WriteLine($"  {userId}: definitely not banned - allow");
            }
        }

        Console.WriteLine();

        // HyperLogLog - "How many unique users made requests?"
        var hll = new HyperLogLog(14);
        foreach (var request in requests)
        {
            hll.Add(request);
        }

        Console.WriteLine("HyperLogLog Cardinality (unique users):");
        Console.WriteLine($"  HyperLogLog estimate: {hll.Count():F0}");
        Console.WriteLine($"  Actual unique: {userSet.Count}");
        Console.WriteLine($"  Error: {Math.Abs(hll.Count() - userSet.Count) / userSet.Count * 100:F2}%");
        Console.WriteLine();

        Console.WriteLine("Key Differences:");
        Console.WriteLine("  Bloom Filter: Returns boolean, tests membership");
        Console.WriteLine("  HyperLogLog: Returns cardinality estimate");
        Console.WriteLine("  Bloom Filter: 100% precision on negatives, false positives possible");
        Console.WriteLine("  HyperLogLog: ~1% error on cardinality estimate");
        Console.WriteLine("  Bloom Filter: Cannot remove elements");
        Console.WriteLine("  HyperLogLog: Mergeable estimates from multiple sources");
        Console.WriteLine();
    }
}
