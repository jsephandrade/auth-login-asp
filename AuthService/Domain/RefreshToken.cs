namespace AuthService.Domain;

public class RefreshToken
{
    public Guid Id { get; set; }
    public Guid SessionId { get; set; }
    public Guid TenantId { get; set; }
    public Guid UserId { get; set; }

    public byte[] TokenHash { get; set; } = Array.Empty<byte>();

    public DateTime CreatedAt { get; set; }
    public DateTime ExpiresAt { get; set; }
    public Guid? RotatedFromId { get; set; }
    public DateTime? RevokedAt { get; set; }

    public byte[]? Ip { get; set; }
    public byte[]? UserAgentHash { get; set; }

    public Session Session { get; set; } = null!;
}
