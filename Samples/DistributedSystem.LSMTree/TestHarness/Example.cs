using System;
using System.Collections.Generic;
using System.Text;

namespace DistributedSystem.LSMTree.TestHarness;

internal class Example
{
    public static void LSMExample()
    {
        Console.WriteLine("=== LSM Tree Demo ===\n");

        // Create LSM Tree with small memtable for demo
        var lsm = new LSMTree("./lsm_data", 1024 * 100); // 100KB memtable

        // 1. INSERT DATA
        Console.WriteLine("1. Inserting data...");
        for (int i = 0; i < 1000; i++)
        {
            string key = $"user_{i:D4}";
            byte[] val = Encoding.UTF8.GetBytes($"Value for user {i}");
            lsm.Add(key, val);
        }
        lsm.PrintStats();

        // 2. READ DATA
        Console.WriteLine("\n2. Reading data...");
        var testKey = "user_0042";
        var value = lsm.Get(testKey);
        Console.WriteLine($"Get('{testKey}'): {Encoding.UTF8.GetString(value ?? Array.Empty<byte>())}");

        // 3. DELETE DATA
        Console.WriteLine("\n3. Deleting user_0042...");
        lsm.Delete("user_0042");
        var deleted = lsm.Get("user_0042");
        Console.WriteLine($"Get('user_0042') after delete: {(deleted == null ? "Not found" : "Found")}");

        // 4. RANGE QUERY
        Console.WriteLine("\n4. Range Query (user_0100 to user_0200)...");
        var range = lsm.Range("user_0100", "user_0200").ToList();
        Console.WriteLine($"Found {range.Count} entries in range");
        foreach (var kvp in range.Take(5))
        {
            Console.WriteLine($"  {kvp.Key}: {Encoding.UTF8.GetString(kvp.Value)}");
        }
        if (range.Count > 5)
            Console.WriteLine($"  ... and {range.Count - 5} more");

        // 5. PERFORMANCE TEST
        Console.WriteLine("\n5. Performance Test - Write 10,000 entries...");
        var watch = System.Diagnostics.Stopwatch.StartNew();
        for (int i = 0; i < 10000; i++)
        {
            lsm.Add($"perf_{i:D5}", Encoding.UTF8.GetBytes($"Value {i}"));
        }
        watch.Stop();
        Console.WriteLine($"Wrote 10,000 entries in {watch.ElapsedMilliseconds}ms");
        Console.WriteLine($"  ({10_000 / (watch.ElapsedMilliseconds / 1000.0):F0} entries/sec)");

        lsm.PrintStats();

        // Clean up
        Console.WriteLine("\nCleaning up...");
        lsm.Dispose();
        Console.WriteLine("Done!");
    }
}
