using System.ComponentModel.DataAnnotations;

namespace AuthService.Domain;

public class Tenant
{
    public Guid Id { get; set; }

    [MaxLength(255)]
    public string Name { get; set; } = string.Empty;

    public TenantStatus Status { get; set; } = TenantStatus.Active;

    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }

    public ICollection<UserTenant> Members { get; set; } = new List<UserTenant>();
    public ICollection<Role> Roles { get; set; } = new List<Role>();
}
