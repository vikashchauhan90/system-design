
namespace DistributedSystem.ISAM;

public sealed class IsamIndex
{
    public List<IndexEntry> Entries { get; } = [];

    private readonly IsamFile _file;
    private readonly int _pageSize;

    public IsamIndex(IsamFile file, int pageSize)
    {
        _file = file;
        _pageSize = pageSize;
    }

    public void Insert(Record record)
    {
        // find correct page
        var page = FindPage(record.Key);

        if (page == null)
        {
            int pageId = _file.CreatePage();
            page = _file.GetPage(pageId);

            Entries.Add(new IndexEntry(record.Key, new PagePointer(pageId)));
            Entries.Sort((a, b) => a.MaxKey.CompareTo(b.MaxKey));
        }

        _file.Insert(page, record);
    }

    public Record? Search(int key)
    {
        int steps = 0;

        var page = FindPage(key);
        if (page == null)
            return null;

        return _file.Search(page, key, ref steps);
    }

    public SearchResult SearchDetailed(int key)
    {
        int steps = 0;

        var entry = Entries.FirstOrDefault(e => key <= e.MaxKey);
        if (entry == null)
            return SearchResult.NotFound(steps, "Index Miss");

        steps++;

        var page = _file.GetPage(entry.Pointer.PageId);

        var result = _file.Search(page, key, ref steps);

        return result != null
            ? SearchResult.Success(result, steps, $"Page {entry.Pointer.PageId}")
            : SearchResult.NotFound(steps, $"Page {entry.Pointer.PageId}");
    }

    public void Rebuild()
    {
        var allRecords = new List<Record>();

        foreach (var page in _file.GetAllPages())
        {
            allRecords.AddRange(page.Records);

            var overflow = page.Overflow;
            while (overflow != null)
            {
                allRecords.AddRange(overflow.Records);
                overflow = overflow.Next;
            }
        }

        Entries.Clear();

        var newFile = new IsamFile(_pageSize);

        foreach (var record in allRecords.OrderBy(r => r.Key))
        {
            var page = FindOrCreatePage(newFile, record.Key);
            newFile.Insert(page, record);
        }

        // replace internal file state (simple reset)
        typeof(IsamIndex)
            .GetField("_file", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!
            .SetValue(this, newFile);
    }

    private DataPage? FindPage(int key)
    {
        var entry = Entries.FirstOrDefault(e => key <= e.MaxKey);
        return entry == null
            ? null
            : _file.GetPage(entry.Pointer.PageId);
    }

    private DataPage FindOrCreatePage(IsamFile file, int key)
    {
        var existing = file.GetAllPages().FirstOrDefault();
        if (existing != null)
            return existing;

        int id = file.CreatePage();
        Entries.Add(new IndexEntry(key, new PagePointer(id)));
        return file.GetPage(id);
    }
}
