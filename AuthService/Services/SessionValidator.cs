using AuthService.Data;
using AuthService.Middleware;
using Microsoft.EntityFrameworkCore;

namespace AuthService.Services;

public class SessionValidator : ISessionValidator
{
    private readonly AuthDbContext _db;
    private readonly ISessionCache _cache;

    public SessionValidator(AuthDbContext db, ISessionCache cache)
    {
        _db = db;
        _cache = cache;
    }

    public async Task ValidateAsync(Guid sessionId, int tokenVersion, Guid tenantId, Guid userId, CancellationToken ct = default)
    {
        var cached = await _cache.GetAsync(sessionId, ct);
        if (cached != null)
        {
            if (cached.Revoked || cached.Compromised || cached.SessionVersion != tokenVersion || cached.UserId != userId || cached.TenantId != tenantId)
            {
                throw new ApiException(401, "session_invalid", "Session is invalid.");
            }
            return;
        }

        var session = await _db.Sessions.FirstOrDefaultAsync(s => s.Id == sessionId && s.UserId == userId && s.TenantId == tenantId, ct);
        if (session == null || session.RevokedAt != null || session.CompromisedAt != null)
        {
            throw new ApiException(401, "session_invalid", "Session is invalid.");
        }

        if (session.SessionVersion != tokenVersion)
        {
            throw new ApiException(401, "token_version_invalid", "Token version is invalid.");
        }

        await _cache.SetAsync(new SessionCacheEntry(session.Id, session.UserId, session.TenantId, session.SessionVersion, false, false), ct);
    }
}
