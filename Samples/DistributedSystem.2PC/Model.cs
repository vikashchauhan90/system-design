namespace DistributedSystem._2PC;

public sealed record Transaction(string TransactionId, string Payload);

public sealed record VoteRequest(string TransactionId, string Payload);
public sealed record VoteResponse(string TransactionId, VoteDecision Decision);
public sealed record GlobalDecision(string TransactionId, bool Commit);
