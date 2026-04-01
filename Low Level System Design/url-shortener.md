# Url Shortener 

```C#
using System;
using System.Collections.Concurrent;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Caching.Memory;

public class OptimizedUrlShortener : IDisposable
{
    private readonly ConcurrentDictionary<string, UrlEntry> _urlMap = new();
    private readonly IMemoryCache _cache;
    private readonly IDistributedLock _distributedLock;
    private readonly ShortenerConfig _config;
    private long _counter;
    private readonly object _counterLock = new();
    
    // Base62 character set
    private const string Base62Chars = "0123456789ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz";
    private static readonly int Base62Length = Base62Chars.Length;
    
    public OptimizedUrlShortener(ShortenerConfig config = null)
    {
        _config = config ?? new ShortenerConfig();
        _cache = new MemoryCache(new MemoryCacheOptions
        {
            SizeLimit = _config.CacheSizeLimit,
            ExpirationScanFrequency = TimeSpan.FromMinutes(5)
        });
        
        // For distributed environments, use Redis or similar
        _distributedLock = _config.UseDistributedLock ? new RedisDistributedLock() : new LocalDistributedLock();
    }
    
    // Multiple strategies for different use cases
    public enum ShorteningStrategy
    {
        Counter,      // Fastest, sequential
        Hash,         // Deterministic, collision-resistant
        Random,       // Simple random
        Hybrid        // Combines counter + hash for best of both
    }
    
    public async Task<string> ShortenUrlAsync(string longUrl, ShorteningStrategy strategy = ShorteningStrategy.Hybrid)
    {
        if (string.IsNullOrWhiteSpace(longUrl))
            throw new ArgumentException("URL cannot be empty", nameof(longUrl));
        
        // Normalize URL
        longUrl = NormalizeUrl(longUrl);
        
        // Check cache first
        if (_cache.TryGetValue(longUrl, out string cachedShortUrl))
            return cachedShortUrl;
        
        string shortUrl = strategy switch
        {
            ShorteningStrategy.Counter => await GenerateCounterBasedAsync(longUrl),
            ShorteningStrategy.Hash => GenerateHashBased(longUrl),
            ShorteningStrategy.Random => await GenerateRandomWithRetryAsync(),
            ShorteningStrategy.Hybrid => await GenerateHybridAsync(longUrl),
            _ => await GenerateHybridAsync(longUrl)
        };
        
        // Store with expiration
        var urlEntry = new UrlEntry
        {
            ShortUrl = shortUrl,
            LongUrl = longUrl,
            CreatedAt = DateTime.UtcNow,
            ExpiresAt = DateTime.UtcNow.AddDays(_config.UrlExpirationDays)
        };
        
        _urlMap[shortUrl] = urlEntry;
        
        // Cache the reverse mapping
        _cache.Set(longUrl, shortUrl, new MemoryCacheEntryOptions
        {
            Size = 1,
            AbsoluteExpirationRelativeToNow = TimeSpan.FromHours(_config.CacheHours)
        });
        
        // Cache forward mapping
        _cache.Set(shortUrl, longUrl, new MemoryCacheEntryOptions
        {
            Size = 1,
            AbsoluteExpirationRelativeToNow = TimeSpan.FromHours(_config.CacheHours)
        });
        
        return shortUrl;
    }
    
    public async Task<string> GetLongUrlAsync(string shortUrl)
    {
        if (string.IsNullOrWhiteSpace(shortUrl))
            return null;
        
        // Check cache first
        if (_cache.TryGetValue(shortUrl, out string longUrl))
            return longUrl;
        
        // Check database/memory
        if (_urlMap.TryGetValue(shortUrl, out var entry))
        {
            // Check expiration
            if (entry.ExpiresAt > DateTime.UtcNow)
            {
                // Update cache
                _cache.Set(shortUrl, entry.LongUrl, TimeSpan.FromHours(_config.CacheHours));
                return entry.LongUrl;
            }
            
            // Remove expired entry
            _urlMap.TryRemove(shortUrl, out _);
        }
        
        return null;
    }
    
    // Strategy 1: Counter-based (Fastest, sequential)
    private async Task<string> GenerateCounterBasedAsync(string longUrl)
    {
        long counter;
        lock (_counterLock)
        {
            counter = Interlocked.Increment(ref _counter);
        }
        
        var shortUrl = EncodeBase62(counter);
        
        // Ensure uniqueness in distributed environment
        if (_config.UseDistributedLock)
        {
            using (await _distributedLock.AcquireLockAsync($"url:{shortUrl}"))
            {
                if (_urlMap.ContainsKey(shortUrl))
                {
                    // Collision occurred, retry with next counter
                    return await GenerateCounterBasedAsync(longUrl);
                }
            }
        }
        
        return shortUrl;
    }
    
    // Strategy 2: Hash-based (Deterministic, no storage needed)
    private string GenerateHashBased(string longUrl)
    {
        using var sha256 = SHA256.Create();
        byte[] hashBytes = sha256.ComputeHash(System.Text.Encoding.UTF8.GetBytes(longUrl));
        
        // Take first 6-8 bytes and convert to Base62
        var shortBytes = new byte[6];
        Array.Copy(hashBytes, shortBytes, 6);
        
        // Convert to Base62
        long hashValue = BitConverter.ToInt64(shortBytes, 0);
        return EncodeBase62(Math.Abs(hashValue));
    }
    
    // Strategy 3: Random with improved uniqueness
    private async Task<string> GenerateRandomWithRetryAsync(int maxRetries = 3)
    {
        for (int i = 0; i < maxRetries; i++)
        {
            var shortUrl = GenerateRandomString(_config.ShortUrlLength);
            
            if (_config.UseDistributedLock)
            {
                using (await _distributedLock.AcquireLockAsync($"url:{shortUrl}"))
                {
                    if (!_urlMap.ContainsKey(shortUrl))
                        return shortUrl;
                }
            }
            else if (!_urlMap.ContainsKey(shortUrl))
            {
                return shortUrl;
            }
        }
        
        // If we exhausted retries, fallback to counter-based
        return await GenerateCounterBasedAsync("");
    }
    
    // Strategy 4: Hybrid (Counter + Hash)
    private async Task<string> GenerateHybridAsync(string longUrl)
    {
        // Get counter part
        long counter;
        lock (_counterLock)
        {
            counter = Interlocked.Increment(ref _counter);
        }
        
        // Get hash part from URL
        using var sha256 = SHA256.Create();
        byte[] hashBytes = sha256.ComputeHash(System.Text.Encoding.UTF8.GetBytes(longUrl));
        long hashValue = BitConverter.ToInt64(hashBytes, 0);
        
        // Combine counter (high bits) with hash (low bits)
        long combined = (counter << 32) | (Math.Abs(hashValue) & 0xFFFFFFFF);
        
        return EncodeBase62(combined);
    }
    
    // Base62 encoding for compact URLs
    private static string EncodeBase62(long number)
    {
        if (number == 0)
            return Base62Chars[0].ToString();
        
        var result = new System.Text.StringBuilder();
        while (number > 0)
        {
            result.Insert(0, Base62Chars[(int)(number % Base62Length)]);
            number /= Base62Length;
        }
        
        return result.ToString();
    }
    
    // Improved random string generation
    private static string GenerateRandomString(int length)
    {
        var result = new char[length];
        var bytes = new byte[length];
        
        using (var rng = RandomNumberGenerator.Create())
        {
            rng.GetBytes(bytes);
        }
        
        for (int i = 0; i < length; i++)
        {
            result[i] = Base62Chars[bytes[i] % Base62Length];
        }
        
        return new string(result);
    }
    
    // URL normalization
    private static string NormalizeUrl(string url)
    {
        url = url.Trim();
        
        // Add protocol if missing
        if (!url.StartsWith("http://", StringComparison.OrdinalIgnoreCase) &&
            !url.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
        {
            url = "https://" + url;
        }
        
        // Remove trailing slash
        url = url.TrimEnd('/');
        
        // Convert to lowercase for consistency
        url = url.ToLowerInvariant();
        
        return url;
    }
    
    // Bulk operations
    public async Task<Dictionary<string, string>> ShortenUrlsAsync(IEnumerable<string> longUrls)
    {
        var results = new Dictionary<string, string>();
        var tasks = longUrls.Select(url => ShortenUrlAsync(url));
        var shortenedUrls = await Task.WhenAll(tasks);
        
        var urlList = longUrls.ToList();
        for (int i = 0; i < urlList.Count; i++)
        {
            results[urlList[i]] = shortenedUrls[i];
        }
        
        return results;
    }
    
    // Statistics and monitoring
    public UrlShortenerStats GetStats()
    {
        return new UrlShortenerStats
        {
            TotalUrls = _urlMap.Count,
            CacheSize = _cache.Count,
            CurrentCounter = _counter,
            ActiveUrls = _urlMap.Count(kvp => kvp.Value.ExpiresAt > DateTime.UtcNow)
        };
    }
    
    public void Dispose()
    {
        _cache?.Dispose();
        _distributedLock?.Dispose();
    }
}

// Configuration
public class ShortenerConfig
{
    public int ShortUrlLength { get; set; } = 7;
    public int UrlExpirationDays { get; set; } = 30;
    public int CacheHours { get; set; } = 24;
    public int CacheSizeLimit { get; set; } = 10000;
    public bool UseDistributedLock { get; set; } = false;
    public string RedisConnectionString { get; set; }
}

// Data structure
public class UrlEntry
{
    public string ShortUrl { get; set; }
    public string LongUrl { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime ExpiresAt { get; set; }
    public int AccessCount { get; set; }
}

// Statistics
public class UrlShortenerStats
{
    public int TotalUrls { get; set; }
    public long CacheSize { get; set; }
    public long CurrentCounter { get; set; }
    public int ActiveUrls { get; set; }
}

// Distributed lock interface (for production use with Redis)
public interface IDistributedLock : IDisposable
{
    Task<IDisposable> AcquireLockAsync(string key, TimeSpan? timeout = null);
}

public class LocalDistributedLock : IDistributedLock
{
    private static readonly ConcurrentDictionary<string, SemaphoreSlim> _locks = new();
    
    public async Task<IDisposable> AcquireLockAsync(string key, TimeSpan? timeout = null)
    {
        var semaphore = _locks.GetOrAdd(key, _ => new SemaphoreSlim(1, 1));
        await semaphore.WaitAsync(timeout ?? TimeSpan.FromSeconds(30));
        return new Releaser(semaphore);
    }
    
    private class Releaser : IDisposable
    {
        private readonly SemaphoreSlim _semaphore;
        public Releaser(SemaphoreSlim semaphore) => _semaphore = semaphore;
        public void Dispose() => _semaphore.Release();
    }
    
    public void Dispose() { }
}

// Redis implementation placeholder
public class RedisDistributedLock : IDistributedLock
{
    public Task<IDisposable> AcquireLockAsync(string key, TimeSpan? timeout = null)
    {
        // Implement Redis distributed lock here
        throw new NotImplementedException();
    }
    
    public void Dispose() { }
}
```