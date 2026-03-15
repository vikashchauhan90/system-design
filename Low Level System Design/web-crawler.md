# Web Crawler

```C#
public class WebCrawler
{
    private readonly HashSet<string> _visitedUrls = new HashSet<string>();
    private readonly HttpClient _httpClient = new HttpClient();

    public async Task CrawlAsync(string url)
    {
        if (!_visitedUrls.Add(url))
        {
            // URL has already been visited.
            return;
        }

        Console.WriteLine($"Crawling {url}");

        var content = await _httpClient.GetStringAsync(url);
        var htmlDocument = new HtmlDocument();
        htmlDocument.LoadHtml(content);

        var links = htmlDocument.DocumentNode.SelectNodes("//a[@href]");
        if (links == null)
        {
            return;
        }

        foreach (var link in links)
        {
            var hrefValue = link.GetAttributeValue("href", string.Empty);
            if (Uri.IsWellFormedUriString(hrefValue, UriKind.Absolute))
            {
                await CrawlAsync(hrefValue);
            }
        }
    }
}
```