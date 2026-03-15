# Url Shortener 

```C#
public class UrlShortener
{
    private static readonly RNGCryptoServiceProvider rng = new RNGCryptoServiceProvider();
    private static readonly ConcurrentDictionary<string, string> urlMap = new ConcurrentDictionary<string, string>();
    // A constant string of possible characters for the short url
    private const string UrlCharacters = "abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789";

    public string Get(string shortUrl)
    {
        urlMap.TryGetValue(shortUrl, out var value);
        return value;
    }


    public string ShortUrl(string longUrl)
    {
        // Generate a new short url
        string shortUrl = GenerateRandomString(7);

        // Check if the short url is already in the dictionary
        while (urlMap.ContainsKey(shortUrl))
        {
            // Generate another short url
            shortUrl = GenerateRandomString(7);
        }

        // Add the mapping to the dictionary
        urlMap.TryAdd(shortUrl, longUrl);

        // Return the new short url
        return shortUrl;
    }


    private static string GenerateRandomString(int length)
    {
        // Create a byte array to store the random bytes
        byte[] bytes = new byte[length];

        rng.GetBytes(bytes);


        // Convert the byte array to a character array
        char[] chars = new char[length];
        for (int i = 0; i < length; i++)
        {
            // Use the byte value as an index to the UrlCharacters string
            chars[i] = UrlCharacters[bytes[i] % UrlCharacters.Length];
        }

        // Return the character array as a string
        return new string(chars);
    }

}

```