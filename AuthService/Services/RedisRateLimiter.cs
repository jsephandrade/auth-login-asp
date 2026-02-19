using AuthService.Middleware;
using StackExchange.Redis;

namespace AuthService.Services;

public class RedisRateLimiter : IRateLimiter
{
    private readonly IDatabase _db;

    private const string Script = @"
local current = redis.call('INCR', KEYS[1])
if tonumber(current) == 1 then
  redis.call('EXPIRE', KEYS[1], ARGV[1])
end
return current
";

    public RedisRateLimiter(IConnectionMultiplexer mux)
    {
        _db = mux.GetDatabase();
    }

    public async Task EnsureAllowedAsync(string key, int limit, TimeSpan window, CancellationToken ct = default)
    {
        var windowSeconds = (int)Math.Ceiling(window.TotalSeconds);
        var windowStart = DateTimeOffset.UtcNow.ToUnixTimeSeconds() / windowSeconds * windowSeconds;
        var redisKey = $"rl:{key}:{windowStart}";

        var result = (long)await _db.ScriptEvaluateAsync(
            Script,
            new RedisKey[] { redisKey },
            new RedisValue[] { windowSeconds }
        );

        if (result > limit)
        {
            throw new ApiException(429, "rate_limited", "Too many requests. Try again later.");
        }
    }
}
