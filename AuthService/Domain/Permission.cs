using System.ComponentModel.DataAnnotations;

namespace AuthService.Domain;

public class Permission
{
    public Guid Id { get; set; }

    [MaxLength(150)]
    public string Name { get; set; } = string.Empty;

    [MaxLength(255)]
    public string? Description { get; set; }

    public ICollection<RolePermission> RolePermissions { get; set; } = new List<RolePermission>();
}
