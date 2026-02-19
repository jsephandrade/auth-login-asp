using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using AuthService.Configuration;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace AuthService.Services;

public interface IJwtValidator
{
    ClaimsPrincipal? Validate(string token);
}

public class JwtValidator : IJwtValidator
{
    private readonly JwtOptions _options;
    private readonly ISigningKeyProvider _keys;

    public JwtValidator(IOptions<JwtOptions> options, ISigningKeyProvider keys)
    {
        _options = options.Value;
        _keys = keys;
    }

    public ClaimsPrincipal? Validate(string token)
    {
        var handler = new JwtSecurityTokenHandler();
        var parameters = new TokenValidationParameters
        {
            ValidIssuer = _options.Issuer,
            ValidAudience = _options.Audience,
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ClockSkew = TimeSpan.FromMinutes(1),
            IssuerSigningKeys = _keys.GetAll().Select(k => new SymmetricSecurityKey(k.Secret) { KeyId = k.KeyId })
        };

        try
        {
            return handler.ValidateToken(token, parameters, out _);
        }
        catch
        {
            return null;
        }
    }
}
