using AuthService.Configuration;
using AuthService.Contracts;
using AuthService.Data;
using AuthService.Domain;
using AuthService.Middleware;
using AuthService.Security;
using AuthService.Services.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using MySqlConnector;
using System.Globalization;
using System.Security.Cryptography;

namespace AuthService.Services;

public class AuthService : IAuthService
{
    private readonly AuthDbContext _db;
    private readonly IPasswordHasher _passwordHasher;
    private readonly IJwtService _jwt;
    private readonly IRefreshTokenService _refreshTokens;
    private readonly IPermissionService _permissions;
    private readonly ITokenHasher _tokenHasher;
    private readonly IAuditService _audit;
    private readonly IEmailSender _email;
    private readonly IRateLimiter _rateLimiter;
    private readonly TokenTtlOptions _tokenTtlOptions;
    private readonly ISessionCache _sessionCache;
    private readonly ILogger<AuthService> _logger;

    public AuthService(
        AuthDbContext db,
        IPasswordHasher passwordHasher,
        IJwtService jwt,
        IRefreshTokenService refreshTokens,
        IPermissionService permissions,
        ITokenHasher tokenHasher,
        IAuditService audit,
        IEmailSender email,
        IRateLimiter rateLimiter,
        IOptions<TokenTtlOptions> tokenTtlOptions,
        ISessionCache sessionCache,
        ILogger<AuthService> logger)
    {
        _db = db;
        _passwordHasher = passwordHasher;
        _jwt = jwt;
        _refreshTokens = refreshTokens;
        _permissions = permissions;
        _tokenHasher = tokenHasher;
        _audit = audit;
        _email = email;
        _rateLimiter = rateLimiter;
        _tokenTtlOptions = tokenTtlOptions.Value;
        _sessionCache = sessionCache;
        _logger = logger;
    }

    public async Task<Guid> RegisterAsync(RegisterRequest request, string? ip, string? userAgent, CancellationToken ct = default)
    {
        await _rateLimiter.EnsureAllowedAsync($"register:{ip}:{request.Email}", 5, TimeSpan.FromMinutes(15), ct);

        if (request.TenantId is null && string.IsNullOrWhiteSpace(request.TenantName))
        {
            throw new ApiException(400, "tenant_required", "TenantId or TenantName is required.");
        }

        var emailNormalized = request.Email.Trim().ToLowerInvariant();
        var existing = await _db.Users.FirstOrDefaultAsync(u => u.EmailNormalized == emailNormalized, ct);
        if (existing != null)
        {
            throw new ApiException(409, "email_in_use", "Email already registered.");
        }

        await using var tx = await _db.Database.BeginTransactionAsync(ct);

        Tenant tenant;
        if (request.TenantId.HasValue)
        {
            tenant = await _db.Tenants.FirstOrDefaultAsync(t => t.Id == request.TenantId.Value, ct)
                ?? throw new ApiException(404, "tenant_not_found", "Tenant not found.");
        }
        else
        {
            tenant = new Tenant
            {
                Id = Guid.NewGuid(),
                Name = request.TenantName!.Trim(),
                Status = TenantStatus.Active,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };
            _db.Tenants.Add(tenant);
        }

        var user = new User
        {
            Id = Guid.NewGuid(),
            Email = request.Email.Trim(),
            EmailNormalized = emailNormalized,
            PasswordHash = _passwordHasher.HashPassword(request.Password),
            Status = UserStatus.Active,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        _db.Users.Add(user);

        var membership = new UserTenant
        {
            TenantId = tenant.Id,
            UserId = user.Id,
            Status = MembershipStatus.Active,
            CreatedAt = DateTime.UtcNow
        };
        _db.UserTenants.Add(membership);

        var (verify, verificationCode) = await BuildEmailVerificationTokenAsync(tenant.Id, user.Id, emailNormalized, ct);
        _db.EmailVerificationTokens.Add(verify);

        try
        {
            await _db.SaveChangesAsync(ct);
            await tx.CommitAsync(ct);
        }
        catch (DbUpdateException ex) when (TryMapRegisterConflict(ex, out var apiEx))
        {
            await tx.RollbackAsync(ct);
            throw apiEx;
        }

        try
        {
            await _email.SendVerifyEmailAsync(user.Email, verificationCode, ct);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Registration succeeded but verification email failed for {Email}", user.Email);
        }

        try
        {
            await _audit.LogAsync("REGISTER", tenant.Id, user.Id, user.Id, ip, userAgent, new { tenantId = tenant.Id });
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Registration succeeded but audit logging failed for {UserId}", user.Id);
        }

        return tenant.Id;
    }

    public async Task VerifyEmailAsync(VerifyEmailRequest request, CancellationToken ct = default)
    {
        var emailNormalized = request.Email.Trim().ToLowerInvariant();
        await _rateLimiter.EnsureAllowedAsync($"verify-code:{emailNormalized}", 10, TimeSpan.FromMinutes(15), ct);

        var user = await _db.Users.FirstOrDefaultAsync(u => u.EmailNormalized == emailNormalized, ct);
        if (user == null)
        {
            throw new ApiException(400, "invalid_verification_code", "Verification code is invalid.");
        }

        if (user.EmailVerifiedAt != null)
        {
            return;
        }

        var hash = HashVerificationCode(emailNormalized, request.Code.Trim());
        var now = DateTime.UtcNow;
        var token = await _db.EmailVerificationTokens
            .FirstOrDefaultAsync(t => t.UserId == user.Id && t.TokenHash == hash && t.UsedAt == null && t.ExpiresAt > now, ct);

        if (token == null)
        {
            var hasActiveCode = await _db.EmailVerificationTokens
                .AnyAsync(t => t.UserId == user.Id && t.UsedAt == null && t.ExpiresAt > now, ct);
            if (!hasActiveCode)
            {
                throw new ApiException(400, "verification_code_expired", "Verification code expired. Request a new code.");
            }

            throw new ApiException(400, "invalid_verification_code", "Verification code is invalid.");
        }

        user.EmailVerifiedAt = DateTime.UtcNow;
        user.UpdatedAt = DateTime.UtcNow;
        token.UsedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync(ct);
        try
        {
            await _audit.LogAsync("EMAIL_VERIFIED", token.TenantId, user.Id, user.Id, null, null);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Email verification succeeded but audit logging failed for {UserId}", user.Id);
        }
    }

    public async Task ResendVerificationCodeAsync(ResendVerificationCodeRequest request, CancellationToken ct = default)
    {
        var emailNormalized = request.Email.Trim().ToLowerInvariant();
        await _rateLimiter.EnsureAllowedAsync($"resend-verify:{emailNormalized}", 5, TimeSpan.FromMinutes(15), ct);

        var user = await _db.Users.FirstOrDefaultAsync(u => u.EmailNormalized == emailNormalized, ct);
        if (user == null || user.EmailVerifiedAt != null)
        {
            return;
        }

        var tenantId = await _db.UserTenants.Where(ut => ut.UserId == user.Id)
            .Select(ut => ut.TenantId)
            .FirstOrDefaultAsync(ct);
        if (tenantId == Guid.Empty)
        {
            return;
        }

        var now = DateTime.UtcNow;
        await _db.EmailVerificationTokens
            .Where(t => t.UserId == user.Id && t.UsedAt == null && t.ExpiresAt > now)
            .ExecuteUpdateAsync(setters => setters.SetProperty(t => t.UsedAt, now), ct);

        var (verify, verificationCode) = await BuildEmailVerificationTokenAsync(tenantId, user.Id, emailNormalized, ct);
        _db.EmailVerificationTokens.Add(verify);
        await _db.SaveChangesAsync(ct);

        try
        {
            await _email.SendVerifyEmailAsync(user.Email, verificationCode, ct);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Resend verification code succeeded but email send failed for {Email}", user.Email);
        }

        try
        {
            await _audit.LogAsync("EMAIL_VERIFICATION_CODE_RESENT", tenantId, user.Id, user.Id, null, null);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Resend verification code succeeded but audit logging failed for {UserId}", user.Id);
        }
    }

    public async Task<AuthResult> LoginAsync(LoginRequest request, string? ip, string? userAgent, CancellationToken ct = default)
    {
        await _rateLimiter.EnsureAllowedAsync($"login:{ip}:{request.Email}", 5, TimeSpan.FromMinutes(15), ct);

        var emailNormalized = request.Email.Trim().ToLowerInvariant();
        var user = await _db.Users.FirstOrDefaultAsync(u => u.EmailNormalized == emailNormalized, ct);
        if (user == null || !_passwordHasher.Verify(request.Password, user.PasswordHash))
        {
            try
            {
                await _audit.LogAsync("LOGIN_FAILED", request.TenantId, user?.Id, user?.Id, ip, userAgent);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Login failed but audit logging failed for {Email}", request.Email);
            }
            throw new ApiException(401, "invalid_credentials", "Invalid credentials.");
        }

        if (user.EmailVerifiedAt == null)
        {
            throw new ApiException(403, "email_not_verified", "Email not verified.");
        }

        var tenantId = await ResolveLoginTenantAsync(user, request, ct);

        await using var tx = await _db.Database.BeginTransactionAsync(ct);

        var activeSessionIds = await _db.Sessions.Where(s => s.UserId == user.Id && s.RevokedAt == null)
            .Select(s => s.Id)
            .ToListAsync(ct);

        await _db.Sessions.Where(s => s.UserId == user.Id && s.RevokedAt == null)
            .ExecuteUpdateAsync(setters => setters.SetProperty(s => s.RevokedAt, DateTime.UtcNow), ct);
        await _db.RefreshTokens.Where(r => r.UserId == user.Id && r.RevokedAt == null)
            .ExecuteUpdateAsync(setters => setters.SetProperty(r => r.RevokedAt, DateTime.UtcNow), ct);

        var session = new Session
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            UserId = user.Id,
            SessionVersion = 1,
            CreatedAt = DateTime.UtcNow,
            Ip = NetUtils.IpToBytes(ip),
            UserAgentHash = NetUtils.HashUserAgent(userAgent)
        };

        _db.Sessions.Add(session);
        await _db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);

        foreach (var sessionId in activeSessionIds)
        {
            await _sessionCache.InvalidateAsync(sessionId, ct);
        }

        var refresh = await _refreshTokens.CreateAsync(session.Id, user.Id, tenantId, ip, userAgent, ct);

        var roles = await _permissions.GetRolesAsync(user.Id, tenantId, ct);
        var perms = await _permissions.GetPermissionsAsync(user.Id, tenantId, ct);

        var access = _jwt.CreateAccessToken(new JwtUserContext(user.Id, tenantId, session.Id, session.SessionVersion, roles, perms));

        try
        {
            await _audit.LogAsync("LOGIN_SUCCESS", tenantId, user.Id, user.Id, ip, userAgent, new { sessionId = session.Id });
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Login succeeded but audit logging failed for {UserId}", user.Id);
        }

        return new AuthResult(access, refresh);
    }

    public async Task<AuthResult?> RefreshAsync(string refreshToken, string? ip, string? userAgent, CancellationToken ct = default)
    {
        await _rateLimiter.EnsureAllowedAsync($"refresh:{ip}", 30, TimeSpan.FromMinutes(5), ct);

        var result = await _refreshTokens.RotateAsync(refreshToken, ip, userAgent, ct);
        if (!result.Success)
        {
            return null;
        }

        var roles = await _permissions.GetRolesAsync(result.UserId, result.TenantId, ct);
        var perms = await _permissions.GetPermissionsAsync(result.UserId, result.TenantId, ct);

        var access = _jwt.CreateAccessToken(new JwtUserContext(result.UserId, result.TenantId, result.SessionId, result.TokenVersion, roles, perms));
        return new AuthResult(access, result.NewRefreshToken!);
    }

    public async Task LogoutAsync(System.Security.Claims.ClaimsPrincipal user, CancellationToken ct = default)
    {
        var sessionId = user.GetSessionId();
        await _db.Sessions.Where(s => s.Id == sessionId)
            .ExecuteUpdateAsync(setters => setters.SetProperty(s => s.RevokedAt, DateTime.UtcNow), ct);
        await _refreshTokens.RevokeAllForSessionAsync(sessionId, ct);
        await _sessionCache.InvalidateAsync(sessionId, ct);
        try
        {
            await _audit.LogAsync("LOGOUT", user.GetTenantId(), user.GetUserId(), user.GetUserId(), null, null, new { sessionId });
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Logout succeeded but audit logging failed for {UserId}", user.GetUserId());
        }
    }

    public async Task ForgotPasswordAsync(ForgotPasswordRequest request, CancellationToken ct = default)
    {
        var emailNormalized = request.Email.Trim().ToLowerInvariant();
        await _rateLimiter.EnsureAllowedAsync($"forgot:{emailNormalized}", 5, TimeSpan.FromMinutes(15), ct);

        var user = await _db.Users.FirstOrDefaultAsync(u => u.EmailNormalized == emailNormalized, ct);
        if (user == null)
        {
            return;
        }

        var tenantId = await _db.UserTenants.Where(ut => ut.UserId == user.Id)
            .Select(ut => ut.TenantId)
            .FirstOrDefaultAsync(ct);

        var now = DateTime.UtcNow;
        await _db.PasswordResetTokens
            .Where(t => t.UserId == user.Id && t.UsedAt == null && t.ExpiresAt > now)
            .ExecuteUpdateAsync(setters => setters.SetProperty(t => t.UsedAt, now), ct);

        var (tokenHash, resetCode) = await GenerateUniqueResetCodeHashAsync(emailNormalized, ct);

        var reset = new PasswordResetToken
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            UserId = user.Id,
            TokenHash = tokenHash,
            CreatedAt = now,
            ExpiresAt = now.AddMinutes(_tokenTtlOptions.PasswordResetMinutes)
        };

        _db.PasswordResetTokens.Add(reset);
        await _db.SaveChangesAsync(ct);

        try
        {
            await _email.SendPasswordResetAsync(user.Email, resetCode, ct);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Forgot-password token created but email send failed for {Email}", user.Email);
        }

        try
        {
            await _audit.LogAsync("PASSWORD_RESET_REQUEST", tenantId, user.Id, user.Id, null, null);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Forgot-password succeeded but audit logging failed for {UserId}", user.Id);
        }
    }

    public async Task VerifyResetCodeAsync(VerifyResetCodeRequest request, CancellationToken ct = default)
    {
        var emailNormalized = request.Email.Trim().ToLowerInvariant();
        await _rateLimiter.EnsureAllowedAsync($"verify-reset:{emailNormalized}", 10, TimeSpan.FromMinutes(15), ct);
        await ResolveActiveResetTokenAsync(emailNormalized, request.Code.Trim(), ct);
    }

    public async Task ResetPasswordAsync(ResetPasswordRequest request, CancellationToken ct = default)
    {
        var emailNormalized = request.Email.Trim().ToLowerInvariant();
        await _rateLimiter.EnsureAllowedAsync($"reset-password:{emailNormalized}", 10, TimeSpan.FromMinutes(15), ct);
        var (token, user) = await ResolveActiveResetTokenAsync(emailNormalized, request.Code.Trim(), ct);

        user.PasswordHash = _passwordHasher.HashPassword(request.NewPassword);
        user.UpdatedAt = DateTime.UtcNow;
        token.UsedAt = DateTime.UtcNow;

        await _db.Sessions.Where(s => s.UserId == user.Id && s.RevokedAt == null)
            .ExecuteUpdateAsync(setters => setters.SetProperty(s => s.RevokedAt, DateTime.UtcNow), ct);
        await _db.RefreshTokens.Where(r => r.UserId == user.Id && r.RevokedAt == null)
            .ExecuteUpdateAsync(setters => setters.SetProperty(r => r.RevokedAt, DateTime.UtcNow), ct);

        await _db.SaveChangesAsync(ct);
        var revokedSessionIds = await _db.Sessions.Where(s => s.UserId == user.Id)
            .Select(s => s.Id)
            .ToListAsync(ct);
        foreach (var sessionId in revokedSessionIds)
        {
            await _sessionCache.InvalidateAsync(sessionId, ct);
        }

        try
        {
            await _audit.LogAsync("PASSWORD_RESET_COMPLETE", token.TenantId, user.Id, user.Id, null, null);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Password reset succeeded but audit logging failed for {UserId}", user.Id);
        }
    }

    public async Task<MeResponse> GetMeAsync(System.Security.Claims.ClaimsPrincipal user, CancellationToken ct = default)
    {
        var userId = user.GetUserId();
        var tenantId = user.GetTenantId();

        var u = await _db.Users.FirstAsync(x => x.Id == userId, ct);
        var roles = await _permissions.GetRolesAsync(userId, tenantId, ct);
        var perms = await _permissions.GetPermissionsAsync(userId, tenantId, ct);

        return new MeResponse
        {
            UserId = userId,
            TenantId = tenantId,
            Email = u.Email,
            Roles = roles,
            Permissions = perms
        };
    }

    private static bool TryMapRegisterConflict(DbUpdateException ex, out ApiException apiException)
    {
        apiException = null!;

        if (ex.InnerException is not MySqlException mysql || mysql.Number != 1062)
        {
            return false;
        }

        var message = mysql.Message;
        if (message.Contains("IX_users_EmailNormalized", StringComparison.OrdinalIgnoreCase))
        {
            apiException = new ApiException(409, "email_in_use", "Email already registered.");
            return true;
        }

        if (message.Contains("IX_tenants_Name", StringComparison.OrdinalIgnoreCase))
        {
            apiException = new ApiException(409, "tenant_name_in_use", "Tenant name already in use.");
            return true;
        }

        apiException = new ApiException(409, "conflict", "Resource already exists.");
        return true;
    }

    private async Task<Guid> ResolveLoginTenantAsync(User user, LoginRequest request, CancellationToken ct)
    {
        var candidates = await (
            from ut in _db.UserTenants.AsNoTracking()
            join t in _db.Tenants.AsNoTracking() on ut.TenantId equals t.Id
            where ut.UserId == user.Id &&
                  ut.Status == MembershipStatus.Active &&
                  t.Status == TenantStatus.Active
            select new
            {
                ut.TenantId,
                ut.CreatedAt,
                TenantName = t.Name
            }
        ).ToListAsync(ct);

        if (request.TenantId.HasValue)
        {
            candidates = candidates.Where(c => c.TenantId == request.TenantId.Value).ToList();
        }

        if (candidates.Count == 0)
        {
            throw new ApiException(403, "tenant_access_denied", "No active workspace found for this account.");
        }

        var selected = candidates
            .OrderBy(c => c.CreatedAt)
            .ThenBy(c => c.TenantName, StringComparer.OrdinalIgnoreCase)
            .First();

        return selected.TenantId;
    }

    private async Task<(EmailVerificationToken Token, string Code)> BuildEmailVerificationTokenAsync(
        Guid tenantId,
        Guid userId,
        string emailNormalized,
        CancellationToken ct)
    {
        var (hash, code) = await GenerateUniqueVerificationCodeHashAsync(emailNormalized, ct);
        var token = new EmailVerificationToken
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            UserId = userId,
            TokenHash = hash,
            CreatedAt = DateTime.UtcNow,
            ExpiresAt = DateTime.UtcNow.AddMinutes(_tokenTtlOptions.EmailVerificationMinutes)
        };
        return (token, code);
    }

    private async Task<(byte[] Hash, string Code)> GenerateUniqueVerificationCodeHashAsync(string emailNormalized, CancellationToken ct)
    {
        for (var i = 0; i < 12; i++)
        {
            var code = GenerateVerificationCode();
            var hash = HashVerificationCode(emailNormalized, code);
            var exists = await _db.EmailVerificationTokens.AnyAsync(t => t.TokenHash == hash, ct);
            if (!exists)
            {
                return (hash, code);
            }
        }

        throw new ApiException(503, "verification_code_unavailable", "Unable to generate verification code. Please try again.");
    }

    private byte[] HashVerificationCode(string emailNormalized, string code)
        => _tokenHasher.HashToken($"verify:{emailNormalized}:{code}");

    private static string GenerateVerificationCode()
    {
        var value = RandomNumberGenerator.GetInt32(0, 1_000_000);
        return value.ToString("D6", CultureInfo.InvariantCulture);
    }

    private byte[] HashPasswordResetCode(string emailNormalized, string code)
        => _tokenHasher.HashToken($"reset:{emailNormalized}:{code}");

    private async Task<(byte[] Hash, string Code)> GenerateUniqueResetCodeHashAsync(string emailNormalized, CancellationToken ct)
    {
        for (var i = 0; i < 12; i++)
        {
            var code = GenerateVerificationCode();
            var hash = HashPasswordResetCode(emailNormalized, code);
            var exists = await _db.PasswordResetTokens.AnyAsync(t => t.TokenHash == hash, ct);
            if (!exists)
            {
                return (hash, code);
            }
        }

        throw new ApiException(503, "reset_code_unavailable", "Unable to generate reset code. Please try again.");
    }

    private async Task<(PasswordResetToken Token, User User)> ResolveActiveResetTokenAsync(string emailNormalized, string code, CancellationToken ct)
    {
        var user = await _db.Users.FirstOrDefaultAsync(u => u.EmailNormalized == emailNormalized, ct);
        if (user == null)
        {
            throw new ApiException(400, "invalid_reset_code", "Reset code is invalid.");
        }

        var now = DateTime.UtcNow;
        var hash = HashPasswordResetCode(emailNormalized, code);
        var token = await _db.PasswordResetTokens
            .FirstOrDefaultAsync(t => t.UserId == user.Id && t.TokenHash == hash && t.UsedAt == null && t.ExpiresAt > now, ct);
        if (token != null)
        {
            return (token, user);
        }

        var hasActiveCode = await _db.PasswordResetTokens
            .AnyAsync(t => t.UserId == user.Id && t.UsedAt == null && t.ExpiresAt > now, ct);
        if (!hasActiveCode)
        {
            throw new ApiException(400, "reset_code_expired", "Reset code expired. Request a new code.");
        }

        throw new ApiException(400, "invalid_reset_code", "Reset code is invalid.");
    }
}
