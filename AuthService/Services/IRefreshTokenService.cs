using AuthService.Services.Models;

namespace AuthService.Services;

public interface IRefreshTokenService
{
    Task<RefreshResult> RotateAsync(string token, string? ip, string? userAgent, CancellationToken ct = default);
    Task<string> CreateAsync(Guid sessionId, Guid userId, Guid tenantId, string? ip, string? userAgent, CancellationToken ct = default);
    Task RevokeAllForSessionAsync(Guid sessionId, CancellationToken ct = default);
}
