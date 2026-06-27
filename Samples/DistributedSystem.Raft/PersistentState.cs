using System.Text.Json.Serialization;

namespace DistributedSystem.Raft;

public class PersistentState
{
    [JsonPropertyName("current_term")]
    public long CurrentTerm { get; set; }

    [JsonPropertyName("voted_for")]
    public string? VotedFor { get; set; }

    [JsonPropertyName("state")]
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public NodeState State { get; set; } = NodeState.Follower;

    [JsonPropertyName("log")]
    public List<LogEntry> Log { get; set; } = new();

    public void Append(LogEntry entry) => Log.Add(entry);
}
