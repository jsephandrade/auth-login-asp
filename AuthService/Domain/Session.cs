namespace AuthService.Domain;

public class Session
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }
    public Guid UserId { get; set; }

    public int SessionVersion { get; set; } = 1;

    public DateTime CreatedAt { get; set; }
    public DateTime? LastSeenAt { get; set; }
    public DateTime? RevokedAt { get; set; }
    public DateTime? CompromisedAt { get; set; }

    public byte[]? Ip { get; set; }
    public byte[]? UserAgentHash { get; set; }

    public User User { get; set; } = null!;
    public Tenant Tenant { get; set; } = null!;
    public ICollection<RefreshToken> RefreshTokens { get; set; } = new List<RefreshToken>();
}
