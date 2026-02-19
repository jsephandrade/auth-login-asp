using System.Collections.Concurrent;
using AuthService.Middleware;

namespace AuthService.Services;

public class MemoryRateLimiter : IRateLimiter
{
    private sealed class Counter
    {
        public int Count;
        public DateTime WindowStart;
    }

    private readonly ConcurrentDictionary<string, Counter> _counters = new();

    public Task EnsureAllowedAsync(string key, int limit, TimeSpan window, CancellationToken ct = default)
    {
        var now = DateTime.UtcNow;
        var counter = _counters.GetOrAdd(key, _ => new Counter { Count = 0, WindowStart = now });

        lock (counter)
        {
            if (now - counter.WindowStart > window)
            {
                counter.WindowStart = now;
                counter.Count = 0;
            }

            counter.Count++;
            if (counter.Count > limit)
            {
                throw new ApiException(429, "rate_limited", "Too many requests. Try again later.");
            }
        }

        return Task.CompletedTask;
    }
}
