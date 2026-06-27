using System;
using System.Collections.Generic;

namespace DistributedSystem._3PC;

public static class ThreePhaseCommitDemo
{
    public static void RunDemo()
    {
        Console.WriteLine("=== Three-Phase Commit (3PC) Demo ===");
        Console.WriteLine();

        var participants = new List<Participant>
        {
            new("participant-1"),
            new("participant-2", transaction => !transaction.Payload.Contains("abort", StringComparison.OrdinalIgnoreCase)),
            new("participant-3", transaction => !transaction.Payload.Contains("abort", StringComparison.OrdinalIgnoreCase), transaction => !transaction.Payload.Contains("fail", StringComparison.OrdinalIgnoreCase))
        };

        var coordinator = new Coordinator(participants);
        var transaction = new Transaction("tx-1001", "account-transfer-100");

        var committed = coordinator.ExecuteTransaction(transaction);

        Console.WriteLine($"Transaction {transaction.TransactionId}: {(committed ? "Committed" : "Aborted")}");
        Console.WriteLine($"Coordinator state: {coordinator.State}");

        foreach (var participant in participants)
        {
            Console.WriteLine($"{participant.ParticipantId}: {participant.State}");
        }
    }
}
