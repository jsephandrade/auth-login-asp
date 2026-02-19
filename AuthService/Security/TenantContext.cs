using System.Security.Claims;

namespace AuthService.Security;

public record TenantContext(Guid TenantId, Guid UserId, Guid SessionId);

public interface ITenantContextAccessor
{
    TenantContext? Current { get; set; }
}

public class TenantContextAccessor : ITenantContextAccessor
{
    private static readonly AsyncLocal<TenantContext?> CurrentContext = new();
    public TenantContext? Current { get => CurrentContext.Value; set => CurrentContext.Value = value; }
}

public class TenantContextMiddleware
{
    private readonly RequestDelegate _next;

    public TenantContextMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public async Task Invoke(HttpContext context, ITenantContextAccessor accessor)
    {
        if (context.User?.Identity?.IsAuthenticated == true)
        {
            var tenantIdClaim = context.User.FindFirstValue(AuthClaims.TenantId)
                                ?? context.User.FindFirstValue(AuthClaims.TenantIdMs);
            var userIdClaim = context.User.FindFirstValue(ClaimTypes.NameIdentifier)
                              ?? context.User.FindFirstValue("sub");
            var sessionIdClaim = context.User.FindFirstValue(AuthClaims.SessionId);

            if (tenantIdClaim == null || userIdClaim == null || sessionIdClaim == null)
            {
                context.Response.StatusCode = StatusCodes.Status401Unauthorized;
                return;
            }

            if (context.Request.Headers.TryGetValue("X-Tenant-Id", out var headerTenant) &&
                !string.Equals(headerTenant.ToString(), tenantIdClaim, StringComparison.OrdinalIgnoreCase))
            {
                context.Response.StatusCode = StatusCodes.Status403Forbidden;
                return;
            }

            accessor.Current = new TenantContext(
                Guid.Parse(tenantIdClaim),
                Guid.Parse(userIdClaim),
                Guid.Parse(sessionIdClaim)
            );
        }

        await _next(context);
    }
}
