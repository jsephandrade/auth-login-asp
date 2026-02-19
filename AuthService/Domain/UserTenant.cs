namespace AuthService.Domain;

public class UserTenant
{
    public Guid TenantId { get; set; }
    public Guid UserId { get; set; }

    public MembershipStatus Status { get; set; } = MembershipStatus.Active;

    public DateTime CreatedAt { get; set; }

    public Tenant Tenant { get; set; } = null!;
    public User User { get; set; } = null!;
}
