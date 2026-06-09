namespace DistributedSystem._2PC.TestHarness;

public static class TwoPhaseCommitDemo
{
    public static void RunDemo()
    {
        var participants = new List<Participant>
        {
            new("participant-1"),
            new("participant-2"),
            new("participant-3", transaction => transaction.Payload.Length % 2 == 0 ? VoteDecision.Commit : VoteDecision.Abort)
        };

        var coordinator = new Coordinator(participants);
        var transaction = new Transaction("tx-123", "important-message");

        Console.WriteLine("Starting two-phase commit transaction...");
        var committed = coordinator.ExecuteTransaction(transaction);

        Console.WriteLine($"Global decision: {(committed ? "COMMIT" : "ABORT")}");
        Console.WriteLine($"Coordinator state: {coordinator.State}");

        foreach (var participant in participants)
        {
            Console.WriteLine($"Participant {participant.ParticipantId} state: {participant.State}");
        }
    }
}
