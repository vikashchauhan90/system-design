namespace DistributedSystem.CountMinSketch.TestHarness;


public static class CountMinSketchTestHarness
{
    public static void Run()
    {
        Console.WriteLine("=====================================");
        Console.WriteLine("Count-Min Sketch Test Harness");
        Console.WriteLine("=====================================");
        Console.WriteLine();

        TestBasicCounting();
        TestMerge();
        TestSerialization();
        TestLargeDataset();
        TestHeavyHitters();

        Console.WriteLine();
        Console.WriteLine("All tests completed.");
    }

    private static void TestBasicCounting()
    {
        Console.WriteLine("Test 1: Basic Counting");

        var cms = new CountMinSketch();

        cms.Add("apple");
        cms.Add("apple");
        cms.Add("apple");

        cms.Add("banana");

        var apple = cms.EstimateCount("apple");
        var banana = cms.EstimateCount("banana");
        var orange = cms.EstimateCount("orange");

        Console.WriteLine($"apple  => {apple} (expected >= 3)");
        Console.WriteLine($"banana => {banana} (expected >= 1)");
        Console.WriteLine($"orange => {orange} (expected 0)");

        Console.WriteLine();
    }

    private static void TestMerge()
    {
        Console.WriteLine("Test 2: Merge");

        var left = new CountMinSketch();
        var right = new CountMinSketch();

        left.Add("login");
        left.Add("login");

        right.Add("login");
        right.Add("logout");

        left.Merge(right);

        Console.WriteLine($"login  => {left.EstimateCount("login")} (expected >= 3)");
        Console.WriteLine($"logout => {left.EstimateCount("logout")} (expected >= 1)");

        Console.WriteLine();
    }

    private static void TestSerialization()
    {
        Console.WriteLine("Test 3: Serialization");

        var cms = new CountMinSketch();

        for (var i = 0; i < 100; i++)
        {
            cms.Add("event");
        }

        var bytes = cms.ToByteArray();

        var restored = CountMinSketch.FromByteArray(bytes);

        var original = cms.EstimateCount("event");
        var recovered = restored.EstimateCount("event");

        Console.WriteLine($"Original  => {original}");
        Console.WriteLine($"Recovered => {recovered}");
        Console.WriteLine($"Serialized Size => {bytes.Length:N0} bytes");

        Console.WriteLine();
    }

    private static void TestLargeDataset()
    {
        Console.WriteLine("Test 4: Large Dataset");

        const int totalEvents = 1_000_000;

        var cms = new CountMinSketch(
            width: 4096,
            depth: 5);

        var exact = new Dictionary<string, int>();

        for (var i = 0; i < totalEvents; i++)
        {
            var key = $"user-{i % 10000}";

            cms.Add(key);

            exact.TryGetValue(key, out var count);
            exact[key] = count + 1;
        }

        var sampleKey = "user-123";

        var actual = exact[sampleKey];
        var estimate = cms.EstimateCount(sampleKey);

        Console.WriteLine($"Actual Count    => {actual}");
        Console.WriteLine($"Estimated Count => {estimate}");
        Console.WriteLine($"Error           => {estimate - actual}");

        Console.WriteLine();
    }

    private static void TestHeavyHitters()
    {
        Console.WriteLine("Test 5: Heavy Hitters");

        var cms = new CountMinSketch();

        var actual = new Dictionary<string, int>();

        void Add(string key, int count)
        {
            for (var i = 0; i < count; i++)
            {
                cms.Add(key);
            }

            actual[key] = count;
        }

        Add("google", 10000);
        Add("amazon", 5000);
        Add("microsoft", 3000);
        Add("apple", 1000);
        Add("netflix", 500);

        Console.WriteLine("Item\t\tActual\tEstimate");

        foreach (var pair in actual.OrderByDescending(x => x.Value))
        {
            var estimate = cms.EstimateCount(pair.Key);

            Console.WriteLine(
                $"{pair.Key,-12}" +
                $"{pair.Value,8}" +
                $"{estimate,12}");
        }

        Console.WriteLine();
    }
}
