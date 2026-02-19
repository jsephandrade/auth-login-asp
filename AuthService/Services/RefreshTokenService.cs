using AuthService.Configuration;
using AuthService.Data;
using AuthService.Domain;
using AuthService.Security;
using AuthService.Services.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace AuthService.Services;

public class RefreshTokenService : IRefreshTokenService
{
    private readonly AuthDbContext _db;
    private readonly ITokenGenerator _tokens;
    private readonly ITokenHasher _hasher;
    private readonly JwtOptions _jwtOptions;
    private readonly IAuditService _audit;
    private readonly ISessionCache _sessionCache;
    private readonly ILogger<RefreshTokenService> _logger;

    public RefreshTokenService(AuthDbContext db, ITokenGenerator tokens, ITokenHasher hasher, IOptions<JwtOptions> jwtOptions, IAuditService audit, ISessionCache sessionCache, ILogger<RefreshTokenService> logger)
    {
        _db = db;
        _tokens = tokens;
        _hasher = hasher;
        _jwtOptions = jwtOptions.Value;
        _audit = audit;
        _sessionCache = sessionCache;
        _logger = logger;
    }

    public async Task<string> CreateAsync(Guid sessionId, Guid userId, Guid tenantId, string? ip, string? userAgent, CancellationToken ct = default)
    {
        var token = _tokens.GenerateToken(32);
        var hash = _hasher.HashToken(token);

        var entity = new RefreshToken
        {
            Id = Guid.NewGuid(),
            SessionId = sessionId,
            UserId = userId,
            TenantId = tenantId,
            TokenHash = hash,
            CreatedAt = DateTime.UtcNow,
            ExpiresAt = DateTime.UtcNow.AddDays(_jwtOptions.RefreshTokenDays),
            Ip = NetUtils.IpToBytes(ip),
            UserAgentHash = NetUtils.HashUserAgent(userAgent)
        };

        _db.RefreshTokens.Add(entity);
        await _db.SaveChangesAsync(ct);

        return token;
    }

    public async Task<RefreshResult> RotateAsync(string token, string? ip, string? userAgent, CancellationToken ct = default)
    {
        var hash = _hasher.HashToken(token);
        var current = await _db.RefreshTokens.FirstOrDefaultAsync(rt => rt.TokenHash == hash, ct);
        if (current == null || current.ExpiresAt <= DateTime.UtcNow)
        {
            return new RefreshResult(false, false, null, Guid.Empty, Guid.Empty, Guid.Empty, 0);
        }

        var session = await _db.Sessions.FirstOrDefaultAsync(s => s.Id == current.SessionId, ct);
        if (session == null || session.RevokedAt != null || session.CompromisedAt != null)
        {
            return new RefreshResult(false, false, null, Guid.Empty, Guid.Empty, Guid.Empty, 0);
        }

        if (current.RevokedAt != null)
        {
            await using var tx = await _db.Database.BeginTransactionAsync(ct);
            session.CompromisedAt = DateTime.UtcNow;
            await _db.RefreshTokens.Where(r => r.SessionId == session.Id && r.RevokedAt == null)
                .ExecuteUpdateAsync(setters => setters.SetProperty(r => r.RevokedAt, DateTime.UtcNow), ct);
            session.RevokedAt = DateTime.UtcNow;
            await _db.SaveChangesAsync(ct);
            await tx.CommitAsync(ct);

            await _sessionCache.InvalidateAsync(session.Id, ct);
            try
            {
                await _audit.LogAsync("REFRESH_REUSE_DETECTED", session.TenantId, session.UserId, session.UserId, ip, userAgent,
                    new { sessionId = session.Id });
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Refresh token reuse detected but audit logging failed for {SessionId}", session.Id);
            }

            return new RefreshResult(false, true, null, session.Id, session.UserId, session.TenantId, session.SessionVersion);
        }

        await using var rotationTx = await _db.Database.BeginTransactionAsync(ct);
        current.RevokedAt = DateTime.UtcNow;

        var newToken = _tokens.GenerateToken(32);
        var newHash = _hasher.HashToken(newToken);

        var next = new RefreshToken
        {
            Id = Guid.NewGuid(),
            SessionId = current.SessionId,
            UserId = current.UserId,
            TenantId = current.TenantId,
            TokenHash = newHash,
            CreatedAt = DateTime.UtcNow,
            ExpiresAt = DateTime.UtcNow.AddDays(_jwtOptions.RefreshTokenDays),
            RotatedFromId = current.Id,
            Ip = NetUtils.IpToBytes(ip),
            UserAgentHash = NetUtils.HashUserAgent(userAgent)
        };

        _db.RefreshTokens.Add(next);
        await _db.SaveChangesAsync(ct);
        await rotationTx.CommitAsync(ct);

        return new RefreshResult(true, false, newToken, session.Id, session.UserId, session.TenantId, session.SessionVersion);
    }

    public async Task RevokeAllForSessionAsync(Guid sessionId, CancellationToken ct = default)
    {
        await _db.RefreshTokens.Where(r => r.SessionId == sessionId && r.RevokedAt == null)
            .ExecuteUpdateAsync(setters => setters.SetProperty(r => r.RevokedAt, DateTime.UtcNow), ct);
    }
}
