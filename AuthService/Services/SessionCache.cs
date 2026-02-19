using System.Text.Json;
using AuthService.Configuration;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Options;

namespace AuthService.Services;

public interface ISessionCache
{
    Task<SessionCacheEntry?> GetAsync(Guid sessionId, CancellationToken ct = default);
    Task SetAsync(SessionCacheEntry entry, CancellationToken ct = default);
    Task InvalidateAsync(Guid sessionId, CancellationToken ct = default);
}

public record SessionCacheEntry(Guid SessionId, Guid UserId, Guid TenantId, int SessionVersion, bool Revoked, bool Compromised);

public class SessionCache : ISessionCache
{
    private readonly IDistributedCache _cache;
    private readonly CacheOptions _options;

    public SessionCache(IDistributedCache cache, IOptions<CacheOptions> options)
    {
        _cache = cache;
        _options = options.Value;
    }

    public async Task<SessionCacheEntry?> GetAsync(Guid sessionId, CancellationToken ct = default)
    {
        var key = GetKey(sessionId);
        var raw = await _cache.GetStringAsync(key, ct);
        if (string.IsNullOrWhiteSpace(raw)) return null;
        return JsonSerializer.Deserialize<SessionCacheEntry>(raw);
    }

    public Task SetAsync(SessionCacheEntry entry, CancellationToken ct = default)
    {
        var key = GetKey(entry.SessionId);
        return _cache.SetStringAsync(
            key,
            JsonSerializer.Serialize(entry),
            new DistributedCacheEntryOptions
            {
                AbsoluteExpirationRelativeToNow = TimeSpan.FromSeconds(_options.SessionSeconds)
            },
            ct);
    }

    public Task InvalidateAsync(Guid sessionId, CancellationToken ct = default)
        => _cache.RemoveAsync(GetKey(sessionId), ct);

    private static string GetKey(Guid sessionId) => $"session:{sessionId}";
}
