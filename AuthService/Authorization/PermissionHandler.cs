using AuthService.Security;
using AuthService.Services;
using Microsoft.AspNetCore.Authorization;

namespace AuthService.Authorization;

public class PermissionHandler : AuthorizationHandler<PermissionRequirement>
{
    private readonly IPermissionService _permissions;

    public PermissionHandler(IPermissionService permissions)
    {
        _permissions = permissions;
    }

    protected override async Task HandleRequirementAsync(AuthorizationHandlerContext context, PermissionRequirement requirement)
    {
        if (context.User?.Identity?.IsAuthenticated != true)
        {
            return;
        }

        var claimPerms = context.User.FindAll(AuthClaims.Permissions).Select(c => c.Value);
        if (claimPerms.Contains(requirement.Permission, StringComparer.OrdinalIgnoreCase))
        {
            context.Succeed(requirement);
            return;
        }

        var userId = context.User.GetUserId();
        var tenantId = context.User.GetTenantId();
        var permissions = await _permissions.GetPermissionsAsync(userId, tenantId);
        if (permissions.Contains(requirement.Permission, StringComparer.OrdinalIgnoreCase))
        {
            context.Succeed(requirement);
        }
    }
}
