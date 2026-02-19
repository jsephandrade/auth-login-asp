using AuthService.Services.Models;

namespace AuthService.Services;

public interface IJwtService
{
    string CreateAccessToken(JwtUserContext context);
}
