namespace AuthService.Domain;

public class UserRole
{
    public Guid TenantId { get; set; }
    public Guid UserId { get; set; }
    public Guid RoleId { get; set; }

    public DateTime CreatedAt { get; set; }

    public Role Role { get; set; } = null!;
    public User User { get; set; } = null!;
}
