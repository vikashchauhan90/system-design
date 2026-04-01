# Text Sharing

```C#
using System;
using System.Collections.Concurrent;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;

public class OptimizedTextSharing : IDisposable
{
    private readonly ConcurrentDictionary<string, TextEntry> _textMap = new();
    private readonly IMemoryCache _cache;
    private readonly IDistributedLock _distributedLock;
    private readonly TextSharingConfig _config;
    private long _counter;
    private readonly object _counterLock = new();
    private readonly HashSet<string> _reservedIds = new() { "api", "admin", "health", "metrics" };
    
    // Base62 character set for compact IDs
    private const string Base62Chars = "0123456789ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz";
    private static readonly int Base62Length = Base62Chars.Length;
    
    // Base64URL character set for secure IDs
    private const string Base64UrlChars = "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789-_";
    
    public OptimizedTextSharing(TextSharingConfig config = null)
    {
        _config = config ?? new TextSharingConfig();
        _cache = new MemoryCache(new MemoryCacheOptions
        {
            SizeLimit = _config.CacheSizeLimit,
            ExpirationScanFrequency = TimeSpan.FromMinutes(5)
        });
        
        _distributedLock = _config.UseDistributedLock ? new RedisDistributedLock() : new LocalDistributedLock();
    }
    
    public enum IdGenerationStrategy
    {
        Counter,      // Sequential numeric IDs (fastest)
        Random,       // Random Base62 IDs
        Hash,         // Content-based hash (deterministic)
        Hybrid,       // Counter + Hash combination
        Guid,         // UUID/GUID (original approach)
        NanoId        // URL-friendly NanoID-like
    }
    
    public enum ExpirationPolicy
    {
        Never,        // Never expires
        TimeBased,    // Expires after configured duration
        AccessBased,  // Expires after period of inactivity
        CountBased    // Expires after number of views
    }
    
    /// <summary>
    /// Adds text snippet with various options
    /// </summary>
    public async Task<TextResponse> AddAsync(
        string textSnippet, 
        IdGenerationStrategy strategy = IdGenerationStrategy.Hybrid,
        ExpirationPolicy expirationPolicy = ExpirationPolicy.TimeBased,
        TimeSpan? customExpiration = null,
        string customId = null,
        bool isPublic = true,
        string password = null)
    {
        if (string.IsNullOrWhiteSpace(textSnippet))
            throw new ArgumentException("Text snippet cannot be empty", nameof(textSnippet));
        
        // Check size limits
        var contentSize = Encoding.UTF8.GetByteCount(textSnippet);
        if (contentSize > _config.MaxContentSizeBytes)
            throw new ArgumentException($"Text size exceeds maximum of {_config.MaxContentSizeBytes / 1024}KB");
        
        // Validate custom ID
        if (!string.IsNullOrEmpty(customId))
        {
            if (_reservedIds.Contains(customId.ToLowerInvariant()))
                throw new ArgumentException($"Custom ID '{customId}' is reserved");
            
            if (customId.Length < _config.MinCustomIdLength || customId.Length > _config.MaxCustomIdLength)
                throw new ArgumentException($"Custom ID must be between {_config.MinCustomIdLength} and {_config.MaxCustomIdLength} characters");
            
            if (!IsValidId(customId))
                throw new ArgumentException("Custom ID contains invalid characters");
        }
        
        string id = customId ?? strategy switch
        {
            IdGenerationStrategy.Counter => await GenerateCounterBasedIdAsync(),
            IdGenerationStrategy.Random => await GenerateRandomWithRetryAsync(),
            IdGenerationStrategy.Hash => GenerateHashBasedId(textSnippet),
            IdGenerationStrategy.Hybrid => await GenerateHybridIdAsync(textSnippet),
            IdGenerationStrategy.Guid => GenerateGuidId(),
            IdGenerationStrategy.NanoId => GenerateNanoId(),
            _ => await GenerateHybridIdAsync(textSnippet)
        };
        
        // Check for ID collision
        if (_textMap.ContainsKey(id))
        {
            if (string.IsNullOrEmpty(customId))
            {
                // Retry with different strategy
                return await AddAsync(textSnippet, IdGenerationStrategy.Random, expirationPolicy);
            }
            throw new InvalidOperationException($"ID '{id}' already exists");
        }
        
        // Calculate expiration
        DateTime? expiresAt = expirationPolicy switch
        {
            ExpirationPolicy.Never => null,
            ExpirationPolicy.TimeBased => DateTime.UtcNow.Add(customExpiration ?? TimeSpan.FromDays(_config.DefaultExpirationDays)),
            ExpirationPolicy.AccessBased => DateTime.UtcNow.Add(TimeSpan.FromDays(_config.AccessBasedExpirationDays)),
            ExpirationPolicy.CountBased => null, // Handled separately
            _ => DateTime.UtcNow.AddDays(_config.DefaultExpirationDays)
        };
        
        // Hash password if provided
        string passwordHash = null;
        if (!string.IsNullOrEmpty(password))
        {
            passwordHash = HashPassword(password);
        }
        
        var entry = new TextEntry
        {
            Id = id,
            Content = textSnippet,
            ContentHash = ComputeHash(textSnippet),
            CreatedAt = DateTime.UtcNow,
            ExpiresAt = expiresAt,
            ExpirationPolicy = expirationPolicy,
            AccessCount = 0,
            MaxAccessCount = expirationPolicy == ExpirationPolicy.CountBased ? _config.MaxAccessCount : null,
            IsPublic = isPublic,
            PasswordHash = passwordHash,
            Metadata = new TextMetadata
            {
                ContentType = DetectContentType(textSnippet),
                SizeBytes = contentSize,
                LineCount = textSnippet.Split('\n').Length,
                WordCount = textSnippet.Split(new[] { ' ', '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries).Length
            }
        };
        
        _textMap[id] = entry;
        
        // Cache the entry
        _cache.Set(id, entry, new MemoryCacheEntryOptions
        {
            Size = 1,
            AbsoluteExpirationRelativeToNow = TimeSpan.FromHours(_config.CacheHours)
        });
        
        return new TextResponse
        {
            Id = id,
            ShareUrl = $"{_config.BaseUrl}/{id}",
            CreatedAt = entry.CreatedAt,
            ExpiresAt = entry.ExpiresAt,
            Metadata = entry.Metadata
        };
    }
    
    /// <summary>
    /// Gets text snippet by ID
    /// </summary>
    public async Task<TextEntry> GetAsync(string id, string password = null)
    {
        if (string.IsNullOrWhiteSpace(id))
            return null;
        
        // Check cache first
        if (_cache.TryGetValue(id, out TextEntry cachedEntry))
        {
            if (!await ValidateAccess(cachedEntry, password))
                return null;
                
            await UpdateAccessStats(cachedEntry);
            return cachedEntry;
        }
        
        // Check main storage
        if (_textMap.TryGetValue(id, out var entry))
        {
            if (!await ValidateAccess(entry, password))
                return null;
            
            // Check expiration
            if (IsExpired(entry))
            {
                await RemoveAsync(id);
                return null;
            }
            
            // Update access stats
            await UpdateAccessStats(entry);
            
            // Update cache
            _cache.Set(id, entry, TimeSpan.FromHours(_config.CacheHours));
            return entry;
        }
        
        return null;
    }
    
    /// <summary>
    /// Updates existing text snippet
    /// </summary>
    public async Task<bool> UpdateAsync(string id, string newContent, string password = null)
    {
        var entry = await GetAsync(id, password);
        if (entry == null)
            return false;
        
        // Check if content changed
        var newHash = ComputeHash(newContent);
        if (entry.ContentHash == newHash)
            return true; // No change needed
        
        // Update entry
        entry.Content = newContent;
        entry.ContentHash = newHash;
        entry.UpdatedAt = DateTime.UtcNow;
        entry.Metadata = new TextMetadata
        {
            ContentType = DetectContentType(newContent),
            SizeBytes = Encoding.UTF8.GetByteCount(newContent),
            LineCount = newContent.Split('\n').Length,
            WordCount = newContent.Split(new[] { ' ', '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries).Length
        };
        
        _textMap[id] = entry;
        
        // Invalidate cache
        _cache.Remove(id);
        
        return true;
    }
    
    /// <summary>
    /// Removes text snippet
    /// </summary>
    public async Task<bool> RemoveAsync(string id, string password = null)
    {
        if (_config.UseDistributedLock)
        {
            using (await _distributedLock.AcquireLockAsync($"text:{id}"))
            {
                if (_textMap.TryRemove(id, out var entry))
                {
                    _cache.Remove(id);
                    return true;
                }
            }
        }
        else
        {
            if (_textMap.TryRemove(id, out var entry))
            {
                _cache.Remove(id);
                return true;
            }
        }
        
        return false;
    }
    
    // ID Generation Strategies
    
    private async Task<string> GenerateCounterBasedIdAsync()
    {
        long counter;
        lock (_counterLock)
        {
            counter = Interlocked.Increment(ref _counter);
        }
        
        return EncodeBase62(counter).PadLeft(_config.MinIdLength, '0');
    }
    
    private async Task<string> GenerateRandomWithRetryAsync(int maxRetries = 3)
    {
        for (int i = 0; i < maxRetries; i++)
        {
            var id = GenerateRandomString(_config.DefaultIdLength);
            
            if (_config.UseDistributedLock)
            {
                using (await _distributedLock.AcquireLockAsync($"text:{id}"))
                {
                    if (!_textMap.ContainsKey(id))
                        return id;
                }
            }
            else if (!_textMap.ContainsKey(id))
            {
                return id;
            }
        }
        
        // Fallback to counter-based
        return await GenerateCounterBasedIdAsync();
    }
    
    private string GenerateHashBasedId(string content)
    {
        using var sha256 = SHA256.Create();
        byte[] hashBytes = sha256.ComputeHash(Encoding.UTF8.GetBytes(content));
        
        // Take first 8 bytes and convert to Base62
        var shortBytes = new byte[8];
        Array.Copy(hashBytes, shortBytes, 8);
        long hashValue = BitConverter.ToInt64(shortBytes, 0);
        
        return EncodeBase62(Math.Abs(hashValue)).Substring(0, Math.Min(_config.DefaultIdLength, 10));
    }
    
    private async Task<string> GenerateHybridIdAsync(string content)
    {
        // Counter part (sequential)
        long counter;
        lock (_counterLock)
        {
            counter = Interlocked.Increment(ref _counter);
        }
        
        // Hash part (content-based)
        using var sha256 = SHA256.Create();
        byte[] hashBytes = sha256.ComputeHash(Encoding.UTF8.GetBytes(content));
        long hashValue = BitConverter.ToInt64(hashBytes, 0);
        
        // Combine counter (high bits) with hash (low bits)
        long combined = (counter << 32) | (Math.Abs(hashValue) & 0xFFFFFFFF);
        
        return EncodeBase62(combined);
    }
    
    private string GenerateGuidId()
    {
        return Guid.NewGuid().ToString("N").Substring(0, _config.DefaultIdLength);
    }
    
    private string GenerateNanoId()
    {
        // NanoID-style: ~4x more efficient than UUID
        var bytes = new byte[_config.DefaultIdLength];
        using (var rng = RandomNumberGenerator.Create())
        {
            rng.GetBytes(bytes);
        }
        
        var result = new char[_config.DefaultIdLength];
        for (int i = 0; i < _config.DefaultIdLength; i++)
        {
            result[i] = Base64UrlChars[bytes[i] % Base64UrlChars.Length];
        }
        
        return new string(result);
    }
    
    // Helper methods
    
    private static string EncodeBase62(long number)
    {
        if (number == 0)
            return Base62Chars[0].ToString();
        
        var result = new StringBuilder();
        while (number > 0)
        {
            result.Insert(0, Base62Chars[(int)(number % Base62Length)]);
            number /= Base62Length;
        }
        
        return result.ToString();
    }
    
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
    
    private static string ComputeHash(string content)
    {
        using var sha256 = SHA256.Create();
        var hashBytes = sha256.ComputeHash(Encoding.UTF8.GetBytes(content));
        return Convert.ToBase64String(hashBytes).Substring(0, 16);
    }
    
    private static string HashPassword(string password)
    {
        using var sha256 = SHA256.Create();
        var hashBytes = sha256.ComputeHash(Encoding.UTF8.GetBytes(password));
        return Convert.ToBase64String(hashBytes);
    }
    
    private static bool IsValidId(string id)
    {
        return System.Text.RegularExpressions.Regex.IsMatch(id, @"^[a-zA-Z0-9\-_]+$");
    }
    
    private static bool IsExpired(TextEntry entry)
    {
        if (entry.ExpiresAt.HasValue && entry.ExpiresAt.Value <= DateTime.UtcNow)
            return true;
        
        if (entry.ExpirationPolicy == ExpirationPolicy.CountBased && 
            entry.MaxAccessCount.HasValue && 
            entry.AccessCount >= entry.MaxAccessCount.Value)
            return true;
        
        return false;
    }
    
    private async Task<bool> ValidateAccess(TextEntry entry, string password)
    {
        // Check if snippet is private and requires password
        if (!entry.IsPublic)
        {
            if (string.IsNullOrEmpty(password))
                return false;
            
            var passwordHash = HashPassword(password);
            if (entry.PasswordHash != passwordHash)
                return false;
        }
        
        return true;
    }
    
    private async Task UpdateAccessStats(TextEntry entry)
    {
        Interlocked.Increment(ref entry.AccessCount);
        entry.LastAccessedAt = DateTime.UtcNow;
        
        // Update expiration for access-based policy
        if (entry.ExpirationPolicy == ExpirationPolicy.AccessBased)
        {
            entry.ExpiresAt = DateTime.UtcNow.AddDays(_config.AccessBasedExpirationDays);
        }
        
        // Update cache
        _cache.Set(entry.Id, entry, TimeSpan.FromHours(_config.CacheHours));
    }
    
    private static string DetectContentType(string content)
    {
        if (content.TrimStart().StartsWith("<") && content.Contains("</"))
            return "html";
        if (content.TrimStart().StartsWith("```") || content.Contains("function") || content.Contains("class "))
            return "code";
        if (content.Contains("http://") || content.Contains("https://"))
            return "url";
        if (content.Split('\n').Length > 20)
            return "long-text";
        
        return "text";
    }
    
    // Bulk operations
    public async Task<Dictionary<string, TextResponse>> AddMultipleAsync(
        IEnumerable<string> snippets, 
        IdGenerationStrategy strategy = IdGenerationStrategy.Hybrid)
    {
        var results = new Dictionary<string, TextResponse>();
        var tasks = snippets.Select(snippet => AddAsync(snippet, strategy));
        var responses = await Task.WhenAll(tasks);
        
        for (int i = 0; i < snippets.Count(); i++)
        {
            results[snippets.ElementAt(i)] = responses[i];
        }
        
        return results;
    }
    
    // Search functionality
    public async Task<IEnumerable<TextEntry>> SearchAsync(string query, int limit = 10)
    {
        return _textMap.Values
            .Where(e => e.Content.Contains(query, StringComparison.OrdinalIgnoreCase))
            .OrderByDescending(e => e.CreatedAt)
            .Take(limit)
            .ToList();
    }
    
    // Statistics
    public TextSharingStats GetStats()
    {
        return new TextSharingStats
        {
            TotalSnippets = _textMap.Count,
            ActiveSnippets = _textMap.Count(kvp => !IsExpired(kvp.Value)),
            TotalViews = _textMap.Values.Sum(v => v.AccessCount),
            CacheSize = _cache.Count,
            CurrentCounter = _counter,
            AverageSizeBytes = _textMap.Values.Any() ? 
                (long)_textMap.Values.Average(v => v.Metadata.SizeBytes) : 0
        };
    }
    
    // Cleanup expired entries
    public async Task<int> CleanupExpiredAsync()
    {
        var expiredIds = _textMap
            .Where(kvp => IsExpired(kvp.Value))
            .Select(kvp => kvp.Key)
            .ToList();
        
        foreach (var id in expiredIds)
        {
            await RemoveAsync(id);
        }
        
        return expiredIds.Count;
    }
    
    public void Dispose()
    {
        _cache?.Dispose();
        _distributedLock?.Dispose();
    }
}

// Configuration
public class TextSharingConfig
{
    public int DefaultIdLength { get; set; } = 8;
    public int MinIdLength { get; set; } = 4;
    public int MaxIdLength { get; set; } = 32;
    public int MinCustomIdLength { get; set; } = 4;
    public int MaxCustomIdLength { get; set; } = 32;
    public int DefaultExpirationDays { get; set; } = 7;
    public int AccessBasedExpirationDays { get; set; } = 30;
    public int MaxAccessCount { get; set; } = 1000;
    public int MaxContentSizeBytes { get; set; } = 1024 * 1024; // 1MB
    public int CacheHours { get; set; } = 24;
    public int CacheSizeLimit { get; set; } = 10000;
    public bool UseDistributedLock { get; set; } = false;
    public string BaseUrl { get; set; } = "https://textshare.com";
    public string RedisConnectionString { get; set; }
}

// Data structures
public class TextEntry
{
    public string Id { get; set; }
    public string Content { get; set; }
    public string ContentHash { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
    public DateTime? ExpiresAt { get; set; }
    public DateTime? LastAccessedAt { get; set; }
    public ExpirationPolicy ExpirationPolicy { get; set; }
    public long AccessCount { get; set; }
    public int? MaxAccessCount { get; set; }
    public bool IsPublic { get; set; }
    public string PasswordHash { get; set; }
    public TextMetadata Metadata { get; set; }
}

public class TextMetadata
{
    public string ContentType { get; set; }
    public long SizeBytes { get; set; }
    public int LineCount { get; set; }
    public int WordCount { get; set; }
}

public class TextResponse
{
    public string Id { get; set; }
    public string ShareUrl { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? ExpiresAt { get; set; }
    public TextMetadata Metadata { get; set; }
}

public class TextSharingStats
{
    public int TotalSnippets { get; set; }
    public int ActiveSnippets { get; set; }
    public long TotalViews { get; set; }
    public long CacheSize { get; set; }
    public long CurrentCounter { get; set; }
    public long AverageSizeBytes { get; set; }
}

// Distributed lock interfaces (same as URL shortener)
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