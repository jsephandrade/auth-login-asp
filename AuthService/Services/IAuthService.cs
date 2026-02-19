using AuthService.Contracts;
using AuthService.Services.Models;
using System.Security.Claims;

namespace AuthService.Services;

public interface IAuthService
{
    Task<Guid> RegisterAsync(RegisterRequest request, string? ip, string? userAgent, CancellationToken ct = default);
    Task VerifyEmailAsync(VerifyEmailRequest request, CancellationToken ct = default);
    Task ResendVerificationCodeAsync(ResendVerificationCodeRequest request, CancellationToken ct = default);
    Task<AuthResult> LoginAsync(LoginRequest request, string? ip, string? userAgent, CancellationToken ct = default);
    Task<AuthResult?> RefreshAsync(string refreshToken, string? ip, string? userAgent, CancellationToken ct = default);
    Task LogoutAsync(ClaimsPrincipal user, CancellationToken ct = default);
    Task ForgotPasswordAsync(ForgotPasswordRequest request, CancellationToken ct = default);
    Task VerifyResetCodeAsync(VerifyResetCodeRequest request, CancellationToken ct = default);
    Task ResetPasswordAsync(ResetPasswordRequest request, CancellationToken ct = default);
    Task<MeResponse> GetMeAsync(ClaimsPrincipal user, CancellationToken ct = default);
}
