namespace DistributedSystem._3PC;

public sealed record Transaction(string TransactionId, string Payload);
public sealed record CanCommitRequest(string TransactionId, string Payload);
public sealed record PrepareRequest(string TransactionId, string Payload);
public sealed record VoteResponse(string TransactionId, bool CanCommit, string Message = "");
public sealed record GlobalDecision(string TransactionId, bool Commit);
