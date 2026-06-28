
namespace DistributedSystem.ISAM;

public sealed class PagePointer
{
    public int PageId { get; }

    public PagePointer(int pageId)
    {
        PageId = pageId;
    }

    public override string ToString() => $"Page({PageId})";
}
