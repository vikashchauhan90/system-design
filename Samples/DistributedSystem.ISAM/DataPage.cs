
namespace DistributedSystem.ISAM;

public sealed class DataPage
{
    public int Id { get; set; }

    public List<Record> Records { get; } = new();

    public OverflowPage? Overflow { get; set; }
}
