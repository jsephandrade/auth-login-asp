using System.Text.Json;
using AuthService.Configuration;
using AuthService.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Options;

namespace AuthService.Services;

public class PermissionService : IPermissionService
{
    private readonly AuthDbContext _db;
    private readonly IDistributedCache _cache;
    private readonly CacheOptions _options;

    public PermissionService(AuthDbContext db, IDistributedCache cache, IOptions<CacheOptions> options)
    {
        _db = db;
        _cache = cache;
        _options = options.Value;
    }

    public async Task<IReadOnlyList<string>> GetPermissionsAsync(Guid userId, Guid tenantId, CancellationToken ct = default)
    {
        var key = $"perm:{tenantId}:{userId}";
        var cached = await _cache.GetStringAsync(key, ct);
        if (!string.IsNullOrWhiteSpace(cached))
        {
            return JsonSerializer.Deserialize<List<string>>(cached) ?? new List<string>();
        }

        var permissions = await (
            from ur in _db.UserRoles
            join rp in _db.RolePermissions on ur.RoleId equals rp.RoleId
            join p in _db.Permissions on rp.PermissionId equals p.Id
            where ur.UserId == userId && ur.TenantId == tenantId
            select p.Name
        ).Distinct().ToListAsync(ct);

        await _cache.SetStringAsync(
            key,
            JsonSerializer.Serialize(permissions),
            new DistributedCacheEntryOptions { AbsoluteExpirationRelativeToNow = TimeSpan.FromSeconds(_options.PermissionSeconds) },
            ct
        );

        return permissions;
    }

    public async Task<IReadOnlyList<string>> GetRolesAsync(Guid userId, Guid tenantId, CancellationToken ct = default)
    {
        var key = $"role:{tenantId}:{userId}";
        var cached = await _cache.GetStringAsync(key, ct);
        if (!string.IsNullOrWhiteSpace(cached))
        {
            return JsonSerializer.Deserialize<List<string>>(cached) ?? new List<string>();
        }

        var roles = await (
            from ur in _db.UserRoles
            join r in _db.Roles on ur.RoleId equals r.Id
            where ur.UserId == userId && ur.TenantId == tenantId
            select r.Name
        ).Distinct().ToListAsync(ct);

        await _cache.SetStringAsync(
            key,
            JsonSerializer.Serialize(roles),
            new DistributedCacheEntryOptions { AbsoluteExpirationRelativeToNow = TimeSpan.FromSeconds(_options.PermissionSeconds) },
            ct
        );

        return roles;
    }
}
