namespace DistributedSystem.AppendLog;

public sealed class AppendLogConfig
{
    public long IndexIntervalBytes { get; init; } = 4096;
    public long MaxSegmentBytes { get; init; } = 64 * 1024;
    public TimeSpan MaxSegmentAge { get; init; } = TimeSpan.FromHours(1);
    public int OffsetIndexInterval { get; init; } = 100;
    public int TimeIndexMaxEntries { get; init; } = 1024;
    public TimeSpan TimeIndexSparseInterval { get; init; } = TimeSpan.FromSeconds(1);
}
