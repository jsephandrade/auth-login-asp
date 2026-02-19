namespace AuthService.Services.Models;

public record AuthResult(string AccessToken, string RefreshToken);

public record RefreshResult(bool Success, bool Compromised, string? NewRefreshToken, Guid SessionId, Guid UserId, Guid TenantId, int TokenVersion);
