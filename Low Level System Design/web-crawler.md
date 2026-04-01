# Web Crawler

```C#
public class WebCrawler
{
    private readonly HashSet<string> _visitedUrls = new HashSet<string>();
    private readonly HttpClient _httpClient = new HttpClient();
    private readonly SemaphoreSlim _concurrencyLimiter;
    private readonly int _maxConcurrency;

    public WebCrawler(int maxConcurrency = 5)
    {
        _maxConcurrency = maxConcurrency;
        _concurrencyLimiter = new SemaphoreSlim(maxConcurrency);
    }

    public async Task CrawlAsync(string startUrl)
    {
        var queue = new Queue<string>();
        var pendingTasks = new List<Task>();
        
        queue.Enqueue(startUrl);
        
        while (queue.Count > 0 || pendingTasks.Count > 0)
        {
            // Process URLs from queue while we have concurrency capacity
            while (queue.Count > 0 && pendingTasks.Count < _maxConcurrency)
            {
                var url = queue.Dequeue();
                
                // Skip if already visited
                if (!_visitedUrls.Contains(url))
                {
                    var task = ProcessUrlAsync(url, queue);
                    pendingTasks.Add(task);
                }
            }
            
            // Wait for any task to complete
            if (pendingTasks.Count > 0)
            {
                var completedTask = await Task.WhenAny(pendingTasks);
                pendingTasks.Remove(completedTask);
                
                // Check for exceptions
                await completedTask;
            }
        }
    }

    private async Task ProcessUrlAsync(string url, Queue<string> queue)
    {
        await _concurrencyLimiter.WaitAsync();
        
        try
        {
            // Mark as visited before processing to avoid race conditions
            lock (_visitedUrls)
            {
                if (_visitedUrls.Contains(url))
                {
                    return;
                }
                _visitedUrls.Add(url);
            }
            
            Console.WriteLine($"Crawling {url}");
            
            // Add timeout to avoid hanging on slow responses
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));
            
            var content = await _httpClient.GetStringAsync(url, cts.Token);
            var htmlDocument = new HtmlDocument();
            htmlDocument.LoadHtml(content);
            
            var links = htmlDocument.DocumentNode.SelectNodes("//a[@href]");
            if (links == null)
            {
                return;
            }
            
            var newUrls = new List<string>();
            
            foreach (var link in links)
            {
                var hrefValue = link.GetAttributeValue("href", string.Empty);
                if (Uri.IsWellFormedUriString(hrefValue, UriKind.Absolute))
                {
                    // Only add to queue if not already visited
                    lock (_visitedUrls)
                    {
                        if (!_visitedUrls.Contains(hrefValue))
                        {
                            newUrls.Add(hrefValue);
                        }
                    }
                }
            }
            
            // Enqueue all new URLs at once
            lock (queue)
            {
                foreach (var newUrl in newUrls)
                {
                    queue.Enqueue(newUrl);
                }
            }
        }
        catch (TaskCanceledException)
        {
            Console.WriteLine($"Timeout while crawling {url}");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error crawling {url}: {ex.Message}");
        }
        finally
        {
            _concurrencyLimiter.Release();
        }
    }
    
    public void Dispose()
    {
        _httpClient?.Dispose();
        _concurrencyLimiter?.Dispose();
    }
}
```