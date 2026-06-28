
namespace DistributedSystem.ISAM;

public sealed class IsamFile
{
    private readonly List<DataPage> _pages = new();
    private int _nextPageId = 0;

    public int PageSize { get; }

    public IsamFile(int pageSize = 4)
    {
        PageSize = pageSize;
    }

    public int CreatePage()
    {
        _pages.Add(new DataPage { Id = _nextPageId });
        return _nextPageId++;
    }

    public DataPage GetPage(int pageId)
        => _pages[pageId];

    public void Insert(DataPage page, Record record)
    {
        if (page.Records.Count < PageSize)
        {
            page.Records.Add(record);
            page.Records.Sort((a, b) => a.Key.CompareTo(b.Key));
            return;
        }

        // overflow handling
        if (page.Overflow == null)
        {
            page.Overflow = new OverflowPage();
        }

        var overflow = page.Overflow;

        while (overflow.Next != null)
            overflow = overflow.Next;

        overflow.Records.Add(record);
    }

    public Record? Search(DataPage page, int key, ref int steps)
    {
        steps++;

        var direct = page.Records.FirstOrDefault(r => r.Key == key);
        if (direct != null)
            return direct;

        var overflow = page.Overflow;

        while (overflow != null)
        {
            steps++;
            var match = overflow.Records.FirstOrDefault(r => r.Key == key);
            if (match != null)
                return match;

            overflow = overflow.Next;
        }

        return null;
    }

    public IEnumerable<DataPage> GetAllPages()
        => _pages;
}
