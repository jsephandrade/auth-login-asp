using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using AuthService.Configuration;
using AuthService.Security;
using AuthService.Services.Models;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace AuthService.Services;

public interface ISigningKeyProvider
{
    SigningKey GetCurrent();
    IEnumerable<SigningKey> GetAll();
}

public record SigningKey(string KeyId, byte[] Secret);

public class InMemorySigningKeyProvider : ISigningKeyProvider
{
    private readonly JwtOptions _options;
    private readonly Dictionary<string, SigningKey> _keys;

    public InMemorySigningKeyProvider(IOptions<JwtOptions> options)
    {
        _options = options.Value;
        _keys = _options.Keys.ToDictionary(k => k.KeyId, k =>
        {
            var secret = ParseSecret(k.SecretBase64, k.KeyId);
            return new SigningKey(k.KeyId, secret);
        });
    }

    private static byte[] ParseSecret(string secretValue, string keyId)
    {
        if (string.IsNullOrWhiteSpace(secretValue))
        {
            throw new InvalidOperationException($"JWT key '{keyId}' secret is missing.");
        }

        try
        {
            return Convert.FromBase64String(secretValue);
        }
        catch (FormatException)
        {
            // Development fallback: accept raw text keys to avoid crashing anonymous endpoints
            // when SecretBase64 is left as a placeholder.
            return Encoding.UTF8.GetBytes(secretValue);
        }
    }

    public SigningKey GetCurrent()
    {
        if (!_keys.TryGetValue(_options.CurrentKeyId, out var key))
        {
            throw new InvalidOperationException("Current signing key not configured.");
        }
        return key;
    }

    public IEnumerable<SigningKey> GetAll() => _keys.Values;
}

public class JwtService : IJwtService
{
    private readonly JwtOptions _options;
    private readonly ISigningKeyProvider _keys;

    public JwtService(IOptions<JwtOptions> options, ISigningKeyProvider keys)
    {
        _options = options.Value;
        _keys = keys;
    }

    public string CreateAccessToken(JwtUserContext context)
    {
        var key = _keys.GetCurrent();
        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, context.UserId.ToString()),
            new(AuthClaims.TenantId, context.TenantId.ToString()),
            new(AuthClaims.SessionId, context.SessionId.ToString()),
            new(AuthClaims.TokenVersion, context.TokenVersion.ToString()),
            new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString("N")),
            new(JwtRegisteredClaimNames.Iat, DateTimeOffset.UtcNow.ToUnixTimeSeconds().ToString(), ClaimValueTypes.Integer64)
        };

        foreach (var role in context.Roles)
        {
            claims.Add(new Claim(ClaimTypes.Role, role));
        }

        foreach (var perm in context.Permissions)
        {
            claims.Add(new Claim(AuthClaims.Permissions, perm));
        }

        var creds = new SigningCredentials(new SymmetricSecurityKey(key.Secret), SecurityAlgorithms.HmacSha256);
        var now = DateTime.UtcNow;

        var token = new JwtSecurityToken(
            issuer: _options.Issuer,
            audience: _options.Audience,
            claims: claims,
            notBefore: now,
            expires: now.AddMinutes(_options.AccessTokenMinutes),
            signingCredentials: creds
        );

        token.Header["kid"] = key.KeyId;
        return new JwtSecurityTokenHandler().WriteToken(token);
    }
}
