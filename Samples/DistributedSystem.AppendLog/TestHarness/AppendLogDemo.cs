using System.Text;

namespace DistributedSystem.AppendLog.TestHarness;

public static class AppendLogDemo
{
    public static void RunDemo()
    {
        var config = new AppendLogConfig
        {
            IndexIntervalBytes = 4096,
            MaxSegmentBytes = 16 * 1024,
            MaxSegmentAge = TimeSpan.FromMinutes(10),
            OffsetIndexInterval = 100,
            TimeIndexMaxEntries = 256,
            TimeIndexSparseInterval = TimeSpan.FromSeconds(1)
        };

        var directory = Path.Combine(Directory.GetCurrentDirectory(), "append-log-data");
        var segment = new LogSegment(directory, 0, config);

        Console.WriteLine("AppendLog demo starting...");

        for (var batchIndex = 0; batchIndex < 8; batchIndex++)
        {
            var records = new List<Record>();
            for (var i = 0; i < 10; i++)
            {
                var payload = new string('x', 120);
                records.Add(Record.FromString($"key-{batchIndex}-{i}", payload, DateTime.UtcNow));
            }

            var batch = new RecordBatch(segment.NextOffset, DateTime.UtcNow, records);
            segment.Append(batch);

            Console.WriteLine($"Appended batch [{batch.BaseOffset}..{batch.LastOffset}] at segment position {segment.OffsetIndex.Entries.Last().Position}");
        }

        var offsetToFind = 15L;
        Console.WriteLine($"\nSearching for offset {offsetToFind}...");
        var batchFound = segment.ReadBatchAtOffset(offsetToFind);

        if (batchFound is null)
        {
            Console.WriteLine("Batch not found.");
        }
        else
        {
            Console.WriteLine($"Found batch at offset {batchFound.BaseOffset} containing {batchFound.Count} records.");
            var withinIndex = offsetToFind - batchFound.BaseOffset;
            if (withinIndex >= 0 && withinIndex < batchFound.Count)
            {
                var record = batchFound.Records[(int)withinIndex];
                Console.WriteLine($"Record key={Encoding.UTF8.GetString(record.Key)} valueLength={record.Value.Length}");
            }
        }

        Console.WriteLine("Index summary:");
        Console.WriteLine($"Offset index entries: {segment.OffsetIndex.Entries.Count}");
        Console.WriteLine($"Time index entries: {segment.TimeIndex.Entries.Count}");
    }
}
