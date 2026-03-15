# Rate Limiter

## Fixed Window RateLimiter

It will restrict the number of requests a client can make within a certain time window. It uses a dictionary to track the number of requests each client has made and when their window started. When a request comes in, it checks if the client’s window has expired. If it has, it starts a new window. If it hasn’t, it increments the client’s request count. If the count exceeds the limit, the request is denied. Otherwise, it’s allowed. This helps prevent any single client from overusing resources.


```C#

public class FixedWindowRateLimiter
{
    private readonly int requestLimit;
    private readonly TimeSpan windowDuration;
    private readonly ConcurrentDictionary<string, (int count, DateTime windowStart)> requestCounts = new ConcurrentDictionary<string, (int count, DateTime windowStart)>();

    public FixedWindowRateLimiter(int requestLimit, TimeSpan windowDuration)
    {
        this.requestLimit = requestLimit;
        this.windowDuration = windowDuration;
    }

    public bool IsAllowed(string clientId)
    {
        lock (requestCounts)
        {
            (int count, DateTime windowStart) = requestCounts.GetOrAdd(clientId, (0, DateTime.UtcNow));

            TimeSpan elapsed = DateTime.UtcNow - windowStart;
            if (elapsed > windowDuration)
            {
                requestCounts[clientId] = (0, DateTime.UtcNow); // Reset count for a new window
                return true;
            }

            if (count < requestLimit)
            {
                requestCounts[clientId] = (Interlocked.Increment(ref count), windowStart); // Incre-ment count
                return true;
            }

            return false; // Request limit reached
        }
    }
}

```
## Leaky Bucket RateLimiter


```C#


public class LeakyBucketRateLimiter
{
    private readonly int capacity;
    private readonly TimeSpan interval;
    private int tokens = 0;
    private DateTime lastRefill = DateTime.UtcNow;
    private readonly object lockObject = new object(); // Dedicated locking object

    public LeakyBucketRateLimiter(int capacity, TimeSpan interval)
    {
        this.capacity = capacity;
        this.interval = interval;
    }

    public bool IsAllowed()
    {
        lock (lockObject) // Lock on the separate object
        {
            RefillTokens();

            if (tokens > 0)
            {
                tokens--;
                return true;
            }

            return false;
        }
    }

    private void RefillTokens()
    {
        TimeSpan elapsed = DateTime.UtcNow - lastRefill;
        int tokensToAdd = (int)Math.Floor(elapsed.TotalSeconds / interval.TotalSeconds);
        tokens = Math.Min(tokens + tokensToAdd, capacity); // Cap at capacity
        lastRefill = lastRefill.AddSeconds(tokensToAdd * interval.TotalSeconds);
    }
}

```

It calculates the time passed since the last refill, determines how many tokens to add based on this elapsed time and a set interval, and adds these tokens up to a maximum capacity. The time of the last refill is then updated. This ensures tokens are added at a fixed rate and the total number doesn’t exceed the capacity.

## Sliding Window RateLimiter

```C#

public class SlidingWindowRateLimiter
{
    private readonly int requestLimit;
    private readonly TimeSpan windowDuration;
    private readonly ConcurrentQueue<DateTime> requestTimestamps = new ConcurrentQueue<DateTime>();

    public SlidingWindowRateLimiter(int requestLimit, TimeSpan windowDuration)
    {
        this.requestLimit = requestLimit;
        this.windowDuration = windowDuration;
    }

    public bool IsAllowed()
    {
        lock (requestTimestamps)
        {
            // Slide the window and remove expired timestamps
            DateTime threshold = DateTime.UtcNow - windowDuration;
            while (requestTimestamps.Count > 0 && requestTimestamps.TryPeek(out DateTime timeFrame) && timeFrame < threshold)
            {
                requestTimestamps.TryDequeue(out _);
            }

            // Check if the request count within the window is within limits
            return requestTimestamps.Count < requestLimit;
        }
    }

    public void AddRequest()
    {
        lock (requestTimestamps)
        {
            requestTimestamps.Enqueue(DateTime.UtcNow); // Add current timestamp to the window
        }
    }
}



```