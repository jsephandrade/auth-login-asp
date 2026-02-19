namespace AuthService.Services;

public class NoopRateLimiter : IRateLimiter
{
    public Task EnsureAllowedAsync(string key, int limit, TimeSpan window, CancellationToken ct = default)
        => Task.CompletedTask;
}
