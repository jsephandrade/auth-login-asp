namespace AuthService.Domain;

public class PasswordResetToken
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }
    public Guid UserId { get; set; }

    public byte[] TokenHash { get; set; } = Array.Empty<byte>();

    public DateTime CreatedAt { get; set; }
    public DateTime ExpiresAt { get; set; }
    public DateTime? UsedAt { get; set; }

    public User User { get; set; } = null!;
    public Tenant Tenant { get; set; } = null!;
}
