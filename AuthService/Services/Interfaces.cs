namespace AuthService.Services;

public interface IPermissionService
{
    Task<IReadOnlyList<string>> GetPermissionsAsync(Guid userId, Guid tenantId, CancellationToken ct = default);
    Task<IReadOnlyList<string>> GetRolesAsync(Guid userId, Guid tenantId, CancellationToken ct = default);
}

public interface IAuditService
{
    Task LogAsync(string eventType, Guid? tenantId, Guid? actorUserId, Guid? targetUserId, string? ip, string? userAgent, object? metadata = null, CancellationToken ct = default);
}

public interface IRateLimiter
{
    Task EnsureAllowedAsync(string key, int limit, TimeSpan window, CancellationToken ct = default);
}

public interface IEmailSender
{
    Task SendVerifyEmailAsync(string email, string code, CancellationToken ct = default);
    Task SendPasswordResetAsync(string email, string code, CancellationToken ct = default);
}

public interface ISessionValidator
{
    Task ValidateAsync(Guid sessionId, int tokenVersion, Guid tenantId, Guid userId, CancellationToken ct = default);
}
