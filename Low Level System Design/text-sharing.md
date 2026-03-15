# Text Sharing

```C#
public class TextSharing
{
   
    /// <summary>
    /// Snippets collection.
    /// </summary>
    private static readonly ConcurrentDictionary<string, string> snippetMap = new ConcurrentDictionary<string, string>();


    public string GetSnippet(string Id)
    {
        snippetMap.TryGetValue(Id, out var snippet);

        return snippet;
    }

    public string Add(string textSnippet)
    {
        var snippetId = Guid.NewGuid().ToString();
        snippetMap.TryAdd(snippetId, textSnippet);

        return snippetId;
    }
}

```