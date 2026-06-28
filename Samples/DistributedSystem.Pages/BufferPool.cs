using System;
using System.Collections.Generic;
using System.Text;

namespace DistributedSystem.Pages;

public sealed class BufferPool
{
    private readonly Dictionary<int, Page> _pages = new();

    public Page GetOrAdd(Page page)
    {
        _pages[page.PageId] = page;
        return page;
    }

    public bool TryGet(int pageId, out Page? page)
    {
        return _pages.TryGetValue(pageId, out page);
    }

    public IEnumerable<Page> GetAllPages() => _pages.Values;
}
