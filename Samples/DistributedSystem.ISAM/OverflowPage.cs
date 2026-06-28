
namespace DistributedSystem.ISAM;

public sealed class OverflowPage
{
    public List<Record> Records { get; } = [];

    public OverflowPage? Next { get; set; }
}
