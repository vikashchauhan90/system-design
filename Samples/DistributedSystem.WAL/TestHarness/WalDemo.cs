namespace DistributedSystem.WAL.TestHarness;

public static class WalDemo
{
    public static void RunDemo()
    {
        var directory = Path.Combine(Directory.GetCurrentDirectory(), "wal-data");
        using var wal = new WriteAheadLog(directory);

        Console.WriteLine("Appending WAL entries...");
        wal.Append("first-message");
        wal.Append("second-message");
        wal.Append("third-message");

        Console.WriteLine($"WAL path: {wal.LogFilePath}");
        Console.WriteLine($"Last sequence number: {wal.LastSequenceNumber}");

        Console.WriteLine("Recovering WAL entries...");
        var recovered = wal.Recover().ToList();
        foreach (var entry in recovered)
        {
            Console.WriteLine($"[{entry.SequenceNumber}] {entry.Timestamp:O} => {entry.PayloadText}");
        }
    }
}
