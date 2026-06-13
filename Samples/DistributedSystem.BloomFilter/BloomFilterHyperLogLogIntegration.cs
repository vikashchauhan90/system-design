using System.Diagnostics;
using DistributedSystem.BloomFilter;

namespace DistributedSystem.BloomFilter.Comparison;

/// <summary>
/// Integrated example showing Bloom Filter and HyperLogLog working together
/// in real distributed system scenarios.
/// </summary>
public sealed class BloomFilterHyperLogLogIntegration
{
    /// <summary>
    /// Stream deduplication system using both algorithms
    /// </summary>
    public static void StreamDeduplicationExample()
    {
        Console.WriteLine("=== Stream Deduplication with Both Algorithms ===\n");

        // Scenario: Processing event stream with deduplication
        // - Use Bloom Filter to quickly reject already-seen events
        // - Use HyperLogLog to track cardinality statistics

        var bloomFilter = BloomFilter.CreateOptimal(100000, 0.01);
        var hll = new HyperLogLog(14);
        var actualDeduplicator = new HashSet<string>();

        var eventStream = GenerateEventStream(100000, uniqueRatio: 0.8);
        var processedCount = 0;
        var skippedCount = 0;

        var sw = Stopwatch.StartNew();

        foreach (var eventId in eventStream)
        {
            // Phase 1: Quick reject using Bloom Filter
            if (bloomFilter.MightContain(eventId))
            {
                // Might be duplicate - check actual store
                if (actualDeduplicator.Contains(eventId))
                {
                    skippedCount++;
                    continue; // Skip duplicate
                }
            }

            // Phase 2: Process new event
            bloomFilter.Add(eventId);
            hll.Add(eventId);
            actualDeduplicator.Add(eventId);
            processedCount++;
        }

        sw.Stop();

        Console.WriteLine($"Processed {eventStream.Count:N0} events");
        Console.WriteLine($"Processed (new): {processedCount:N0}");
        Console.WriteLine($"Skipped (duplicates): {skippedCount:N0}");
        Console.WriteLine();
        Console.WriteLine($"HyperLogLog Cardinality Estimate: {hll.Count():F0}");
        Console.WriteLine($"Actual Unique: {actualDeduplicator.Count}");
        Console.WriteLine($"Accuracy: {(1 - Math.Abs(hll.Count() - actualDeduplicator.Count) / actualDeduplicator.Count) * 100:F2}%");
        Console.WriteLine();
        Console.WriteLine($"Bloom Filter Memory: {bloomFilter.MemoryUsageBytes:N0} bytes");
        Console.WriteLine($"HyperLogLog Memory: {hll.ToByteArray().Length} bytes");
        Console.WriteLine($"Total Overhead: {(bloomFilter.MemoryUsageBytes + hll.ToByteArray().Length) / 1024.0:F2} KB for {actualDeduplicator.Count:N0} unique items");
        Console.WriteLine($"Processing Time: {sw.ElapsedMilliseconds} ms");
        Console.WriteLine();
    }

    /// <summary>
    /// Cache coherence using Bloom Filter + HyperLogLog statistics
    /// </summary>
    public static void CacheCoherenceExample()
    {
        Console.WriteLine("=== Cache Coherence with Statistics ===\n");

        var cacheFilter = BloomFilter.CreateOptimal(50000, 0.01);
        var accessStats = new HyperLogLog(12);

        var cacheHits = 0;
        var cacheMisses = 0;
        var queries = GenerateQueryStream(10000, 5000); // 10k queries, 5k unique

        foreach (var query in queries)
        {
            if (cacheFilter.MightContain(query))
            {
                // Might be in cache
                cacheHits++;
            }
            else
            {
                // Definitely not in cache - fetch from DB
                cacheMisses++;
                cacheFilter.Add(query);
            }

            // Track access patterns for analytics
            accessStats.Add(query);
        }

        Console.WriteLine($"Queries processed: {queries.Count:N0}");
        Console.WriteLine($"Cache hits: {cacheHits:N0}");
        Console.WriteLine($"Cache misses: {cacheMisses:N0}");
        Console.WriteLine($"Hit ratio: {(double)cacheHits / queries.Count * 100:F2}%");
        Console.WriteLine();
        Console.WriteLine($"Unique query patterns (HyperLogLog): {accessStats.Count():F0}");
        Console.WriteLine();
    }

    /// <summary>
    /// Real-time fraud detection using both algorithms
    /// </summary>
    public static void FraudDetectionExample()
    {
        Console.WriteLine("=== Real-time Fraud Detection ===\n");

        // Known fraud patterns
        var knownFraudFilter = BloomFilter.CreateOptimal(10000, 0.01);
        var fraudPatterns = new[] 
        { 
            "card-4111-1111-1111-1111",
            "suspicious-ip-192-168-1-1",
            "velocity-10-txns-per-minute",
            "impossible-travel-distance"
        };

        foreach (var pattern in fraudPatterns)
        {
            knownFraudFilter.Add(pattern);
        }

        // Track unique suspicious transactions
        var suspiciousTransactions = new HyperLogLog(14);

        var transactions = new[]
        {
            ("txn-1", "card-4111-1111-1111-1111", "fraud"),    // Matches known pattern
            ("txn-2", "card-5555-5555-5555-5555", "normal"),   // Safe card
            ("txn-3", "suspicious-ip-192-168-1-1", "fraud"),   // Matches pattern
            ("txn-4", "card-3333-3333-3333-3333", "normal"),
            ("txn-5", "card-4111-1111-1111-1111", "fraud"),    // Duplicate of txn-1
        };

        var flagged = 0;
        foreach (var (txnId, pattern, expected) in transactions)
        {
            if (knownFraudFilter.MightContain(pattern))
            {
                Console.WriteLine($"  FRAUD ALERT: {txnId} matches known pattern");
                flagged++;
            }
            else
            {
                Console.WriteLine($"  SAFE: {txnId} - no known fraud patterns");
            }

            suspiciousTransactions.Add(pattern);
        }

        Console.WriteLine();
        Console.WriteLine($"Transactions flagged: {flagged}");
        Console.WriteLine($"Unique suspicious patterns seen: {suspiciousTransactions.Count():F0}");
        Console.WriteLine();
    }

    /// <summary>
    /// Log compaction with cardinality tracking
    /// </summary>
    public static void LogCompactionWithStatisticsExample()
    {
        Console.WriteLine("=== Log Compaction with Statistics ===\n");

        var segments = new Dictionary<string, (BloomFilter filter, HyperLogLog stats, int keyCount)>();

        // Create 3 segments with different key distributions
        var segmentConfigs = new[]
        {
            ("segment-1", 1000),
            ("segment-2", 1500),
            ("segment-3", 800)
        };

        foreach (var (segmentId, keyCount) in segmentConfigs)
        {
            var filter = BloomFilter.CreateOptimal(keyCount, 0.01);
            var stats = new HyperLogLog(12);

            for (int i = 0; i < keyCount; i++)
            {
                var key = $"key-{i % 2000}"; // Some overlap across segments
                filter.Add(key);
                stats.Add(key);
            }

            segments[segmentId] = (filter, stats, keyCount);
        }

        Console.WriteLine("Segment Analysis:");
        foreach (var (segId, (filter, stats, count)) in segments)
        {
            Console.WriteLine($"  {segId}:");
            Console.WriteLine($"    Keys stored: {count}");
            Console.WriteLine($"    Unique keys (HyperLogLog): {stats.Count():F0}");
            Console.WriteLine($"    Memory (BF + HLL): {filter.MemoryUsageBytes + stats.ToByteArray().Length} bytes");
        }

        Console.WriteLine();

        // Simulate compaction lookup
        var keysToFind = new[] { "key-100", "key-500", "key-1000", "key-1500" };
        var segmentChecks = 0;
        var bloomFilterSkips = 0;

        foreach (var key in keysToFind)
        {
            foreach (var (segId, (filter, _, _)) in segments)
            {
                segmentChecks++;
                if (!filter.MightContain(key))
                {
                    bloomFilterSkips++;
                }
            }
        }

        Console.WriteLine($"Compaction Efficiency:");
        Console.WriteLine($"  Total segment checks needed: {segmentChecks}");
        Console.WriteLine($"  Skipped by Bloom Filter: {bloomFilterSkips} ({(double)bloomFilterSkips / segmentChecks * 100:F1}%)");
        Console.WriteLine();
    }

    /// <summary>
    /// Distributed tracing with both algorithms
    /// </summary>
    public static void DistributedTracingExample()
    {
        Console.WriteLine("=== Distributed Request Tracing ===\n");

        // Each service tracks its own request patterns
        var services = new Dictionary<string, (BloomFilter seenRequests, HyperLogLog uniqueUsers)>
        {
            { "API-Gateway", (BloomFilter.CreateOptimal(10000, 0.01), new HyperLogLog(12)) },
            { "AuthService", (BloomFilter.CreateOptimal(10000, 0.01), new HyperLogLog(12)) },
            { "DataService", (BloomFilter.CreateOptimal(10000, 0.01), new HyperLogLog(12)) }
        };

        var requests = GenerateDistributedRequests(5000);

        foreach (var (serviceName, (seenFilter, uniqueUsers)) in services)
        {
            foreach (var (requestId, userId, service) in requests)
            {
                if (service == serviceName)
                {
                    seenFilter.Add(requestId);
                    uniqueUsers.Add(userId);
                }
            }
        }

        Console.WriteLine("Service Statistics:");
        foreach (var (serviceName, (seenFilter, uniqueUsers)) in services)
        {
            Console.WriteLine($"  {serviceName}:");
            Console.WriteLine($"    Unique users: {uniqueUsers.Count():F0}");
            Console.WriteLine($"    Memory footprint: {seenFilter.MemoryUsageBytes + uniqueUsers.ToByteArray().Length} bytes");
        }

        Console.WriteLine();

        // Detect request loops (request seen in multiple services)
        var globalSeen = BloomFilter.CreateOptimal(10000, 0.01);
        var loopDetected = 0;

        foreach (var (serviceName, (seenFilter, _)) in services)
        {
            // Merge this service's filter with global
            globalSeen.Merge(seenFilter);
        }

        Console.WriteLine($"Request tracking across services initialized");
        Console.WriteLine();
    }

    // Helper methods

    private static List<string> GenerateEventStream(int count, double uniqueRatio)
    {
        var uniqueCount = (int)(count * uniqueRatio);
        var events = new List<string>();
        var random = new Random(42);

        for (int i = 0; i < count; i++)
        {
            events.Add($"event-{random.Next(uniqueCount)}");
        }

        return events;
    }

    private static List<string> GenerateQueryStream(int count, int uniqueQueries)
    {
        var queries = new List<string>();
        var random = new Random(42);

        for (int i = 0; i < count; i++)
        {
            queries.Add($"query-{random.Next(uniqueQueries)}");
        }

        return queries;
    }

    private static List<(string requestId, string userId, string service)> GenerateDistributedRequests(int count)
    {
        var requests = new List<(string, string, string)>();
        var random = new Random(42);
        var services = new[] { "API-Gateway", "AuthService", "DataService" };

        for (int i = 0; i < count; i++)
        {
            var requestId = $"req-{i}";
            var userId = $"user-{random.Next(1000)}";
            var service = services[i % services.Length];
            requests.Add((requestId, userId, service));
        }

        return requests;
    }
}

/// <summary>
/// Main entry point for integration examples
/// </summary>
public static class BloomHyperLogLogDemo
{
    public static void Main(string[] args)
    {
        Console.WriteLine("╔════════════════════════════════════════════════════════════╗");
        Console.WriteLine("║   Bloom Filter & HyperLogLog Integration Examples          ║");
        Console.WriteLine("╚════════════════════════════════════════════════════════════╝\n");

        BloomFilterHyperLogLogIntegration.StreamDeduplicationExample();
        BloomFilterHyperLogLogIntegration.CacheCoherenceExample();
        BloomFilterHyperLogLogIntegration.FraudDetectionExample();
        BloomFilterHyperLogLogIntegration.LogCompactionWithStatisticsExample();
        BloomFilterHyperLogLogIntegration.DistributedTracingExample();

        Console.WriteLine("=== Summary ===\n");
        Console.WriteLine("Bloom Filter + HyperLogLog provide complementary capabilities:");
        Console.WriteLine();
        Console.WriteLine("1. DEDUPLICATION:");
        Console.WriteLine("   - Bloom Filter: Quick reject of duplicates");
        Console.WriteLine("   - HyperLogLog: Track cardinality for analytics");
        Console.WriteLine();
        Console.WriteLine("2. CACHING:");
        Console.WriteLine("   - Bloom Filter: Avoid lookups for missing keys");
        Console.WriteLine("   - HyperLogLog: Track unique access patterns");
        Console.WriteLine();
        Console.WriteLine("3. FRAUD DETECTION:");
        Console.WriteLine("   - Bloom Filter: Quickly match against fraud patterns");
        Console.WriteLine("   - HyperLogLog: Track prevalence of suspicious indicators");
        Console.WriteLine();
        Console.WriteLine("4. LOG COMPACTION:");
        Console.WriteLine("   - Bloom Filter: Skip segments without keys");
        Console.WriteLine("   - HyperLogLog: Estimate compaction benefits");
        Console.WriteLine();
        Console.WriteLine("5. DISTRIBUTED SYSTEMS:");
        Console.WriteLine("   - Bloom Filter: Local fast filtering");
        Console.WriteLine("   - HyperLogLog: Mergeable global statistics");
    }
}
