using System.Text.Json;
using AuthService.Data;
using AuthService.Domain;
using AuthService.Security;
using Microsoft.EntityFrameworkCore;

namespace AuthService.Services;

public class AuditService : IAuditService
{
    private readonly AuthDbContext _db;
    private readonly ILogger<AuditService> _logger;

    public AuditService(AuthDbContext db, ILogger<AuditService> logger)
    {
        _db = db;
        _logger = logger;
    }

    public async Task LogAsync(string eventType, Guid? tenantId, Guid? actorUserId, Guid? targetUserId, string? ip, string? userAgent, object? metadata = null, CancellationToken ct = default)
    {
        _logger.LogInformation("audit {event} tenant={tenant} actor={actor} target={target}", eventType, tenantId, actorUserId, targetUserId);

        var log = new AuditLog
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            ActorUserId = actorUserId,
            TargetUserId = targetUserId,
            EventType = eventType,
            CreatedAt = DateTime.UtcNow,
            Ip = NetUtils.IpToBytes(ip),
            UserAgent = userAgent,
            MetadataJson = metadata is null ? null : JsonSerializer.Serialize(metadata)
        };

        _db.AuditLogs.Add(log);
        await _db.SaveChangesAsync(ct);
    }
}
