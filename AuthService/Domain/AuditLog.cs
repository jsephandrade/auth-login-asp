using System.ComponentModel.DataAnnotations;

namespace AuthService.Domain;

public class AuditLog
{
    public Guid Id { get; set; }
    public Guid? TenantId { get; set; }
    public Guid? ActorUserId { get; set; }
    public Guid? TargetUserId { get; set; }

    [MaxLength(100)]
    public string EventType { get; set; } = string.Empty;

    public DateTime CreatedAt { get; set; }

    public byte[]? Ip { get; set; }

    [MaxLength(512)]
    public string? UserAgent { get; set; }

    public string? MetadataJson { get; set; }
}
