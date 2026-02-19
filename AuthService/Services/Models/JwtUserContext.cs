namespace AuthService.Services.Models;

public record JwtUserContext(
    Guid UserId,
    Guid TenantId,
    Guid SessionId,
    int TokenVersion,
    IReadOnlyList<string> Roles,
    IReadOnlyList<string> Permissions
);
