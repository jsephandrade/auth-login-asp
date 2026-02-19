using System.ComponentModel.DataAnnotations;

namespace AuthService.Domain;

public class Role
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }

    [MaxLength(100)]
    public string Name { get; set; } = string.Empty;

    [MaxLength(255)]
    public string? Description { get; set; }

    public DateTime CreatedAt { get; set; }

    public Tenant Tenant { get; set; } = null!;
    public ICollection<RolePermission> RolePermissions { get; set; } = new List<RolePermission>();
    public ICollection<UserRole> UserRoles { get; set; } = new List<UserRole>();
}
