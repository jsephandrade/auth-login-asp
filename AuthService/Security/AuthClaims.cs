using System.Security.Claims;

namespace AuthService.Security;

public static class AuthClaims
{
    public const string TenantId = "tid";
    public const string TenantIdMs = "http://schemas.microsoft.com/identity/claims/tenantid";
    public const string SessionId = "sid";
    public const string TokenVersion = "token_version";
    public const string Permissions = "perm";
}

public static class ClaimsPrincipalExtensions
{
    public static Guid GetUserId(this ClaimsPrincipal user)
    {
        var value = user.FindFirstValue(ClaimTypes.NameIdentifier) ??
                    user.FindFirstValue("sub");
        return Guid.Parse(value ?? throw new InvalidOperationException("sub missing"));
    }

    public static Guid GetTenantId(this ClaimsPrincipal user)
    {
        var value = user.FindFirstValue(AuthClaims.TenantId) ??
                    user.FindFirstValue(AuthClaims.TenantIdMs);
        return Guid.Parse(value ?? throw new InvalidOperationException("tid missing"));
    }

    public static Guid GetSessionId(this ClaimsPrincipal user)
        => Guid.Parse(user.FindFirstValue(AuthClaims.SessionId) ?? throw new InvalidOperationException("sid missing"));

    public static int GetTokenVersion(this ClaimsPrincipal user)
    {
        var raw = user.FindFirstValue(AuthClaims.TokenVersion) ?? "0";
        return int.TryParse(raw, out var v) ? v : 0;
    }
}
