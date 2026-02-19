using System.Collections.Concurrent;

namespace AuthService.Services;

public interface IPasswordResetCodeDebugStore
{
    void SetCode(string email, string code);
    string? GetActiveCode(string email, TimeSpan maxAge);
}

public class InMemoryPasswordResetCodeDebugStore : IPasswordResetCodeDebugStore
{
    private readonly ConcurrentDictionary<string, Entry> _codes = new(StringComparer.OrdinalIgnoreCase);

    public void SetCode(string email, string code)
    {
        var key = NormalizeEmail(email);
        if (string.IsNullOrWhiteSpace(key))
        {
            return;
        }

        _codes[key] = new Entry(code, DateTime.UtcNow);
    }

    public string? GetActiveCode(string email, TimeSpan maxAge)
    {
        var key = NormalizeEmail(email);
        if (string.IsNullOrWhiteSpace(key))
        {
            return null;
        }

        if (!_codes.TryGetValue(key, out var entry))
        {
            return null;
        }

        if (DateTime.UtcNow - entry.CreatedAt > maxAge)
        {
            _codes.TryRemove(key, out _);
            return null;
        }

        return entry.Code;
    }

    private static string NormalizeEmail(string email)
        => (email ?? string.Empty).Trim().ToLowerInvariant();

    private sealed record Entry(string Code, DateTime CreatedAt);
}
