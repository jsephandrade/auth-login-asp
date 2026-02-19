using Microsoft.AspNetCore.Authorization;

namespace AuthService.Authorization;

public class RequirePermissionAttribute : AuthorizeAttribute
{
    public RequirePermissionAttribute(string permission)
    {
        Policy = PermissionPolicyProvider.PolicyPrefix + permission;
    }
}
