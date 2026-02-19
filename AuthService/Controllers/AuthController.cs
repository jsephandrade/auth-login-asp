using AuthService.Authorization;
using AuthService.Configuration;
using AuthService.Contracts;
using AuthService.Middleware;
using AuthService.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

namespace AuthService.Controllers;

[ApiController]
[Route("auth")]
public class AuthController : ControllerBase
{
    private readonly IAuthService _auth;
    private readonly IEmailSender _emailSender;
    private readonly IVerificationCodeDebugStore _verificationCodeStore;
    private readonly IPasswordResetCodeDebugStore _passwordResetCodeStore;
    private readonly AuthCookieOptions _cookieOptions;
    private readonly JwtOptions _jwtOptions;

    public AuthController(
        IAuthService auth,
        IEmailSender emailSender,
        IVerificationCodeDebugStore verificationCodeStore,
        IPasswordResetCodeDebugStore passwordResetCodeStore,
        IOptions<AuthCookieOptions> cookieOptions,
        IOptions<JwtOptions> jwtOptions)
    {
        _auth = auth;
        _emailSender = emailSender;
        _verificationCodeStore = verificationCodeStore;
        _passwordResetCodeStore = passwordResetCodeStore;
        _cookieOptions = cookieOptions.Value;
        _jwtOptions = jwtOptions.Value;
    }

    [HttpPost("register")]
    public async Task<ActionResult<RegisterResponse>> Register([FromBody] RegisterRequest request, CancellationToken ct)
    {
        var tenantId = await _auth.RegisterAsync(request, HttpContext.Connection.RemoteIpAddress?.ToString(), Request.Headers.UserAgent.ToString(), ct);
        return StatusCode(201, new RegisterResponse
        {
            TenantId = tenantId,
            VerificationCode = ResolveDevVerificationCode(request.Email)
        });
    }

    [HttpPost("verify-email")]
    public async Task<IActionResult> VerifyEmail([FromBody] VerifyEmailRequest request, CancellationToken ct)
    {
        await _auth.VerifyEmailAsync(request, ct);
        return NoContent();
    }

    [HttpPost("resend-verification-code")]
    public async Task<ActionResult<ResendVerificationCodeResponse>> ResendVerificationCode([FromBody] ResendVerificationCodeRequest request, CancellationToken ct)
    {
        await _auth.ResendVerificationCodeAsync(request, ct);
        return Ok(new ResendVerificationCodeResponse
        {
            VerificationCode = ResolveDevVerificationCode(request.Email)
        });
    }

    [HttpPost("login")]
    public async Task<ActionResult<RefreshResponse>> Login([FromBody] LoginRequest request, CancellationToken ct)
    {
        var result = await _auth.LoginAsync(request, HttpContext.Connection.RemoteIpAddress?.ToString(), Request.Headers.UserAgent.ToString(), ct);
        SetRefreshCookie(result.RefreshToken);
        return Ok(new RefreshResponse { AccessToken = result.AccessToken });
    }

    [HttpPost("refresh")]
    public async Task<ActionResult<RefreshResponse>> Refresh(CancellationToken ct)
    {
        var refreshToken = Request.Cookies[_cookieOptions.RefreshCookieName];
        if (string.IsNullOrWhiteSpace(refreshToken))
        {
            throw new ApiException(401, "refresh_missing", "Refresh token missing.");
        }

        var result = await _auth.RefreshAsync(refreshToken, HttpContext.Connection.RemoteIpAddress?.ToString(), Request.Headers.UserAgent.ToString(), ct);
        if (result == null)
        {
            ClearRefreshCookie();
            throw new ApiException(401, "refresh_invalid", "Refresh token invalid.");
        }

        SetRefreshCookie(result.RefreshToken);
        return Ok(new RefreshResponse { AccessToken = result.AccessToken });
    }

    [Authorize]
    [HttpPost("logout")]
    public async Task<IActionResult> Logout(CancellationToken ct)
    {
        await _auth.LogoutAsync(User, ct);
        ClearRefreshCookie();
        return NoContent();
    }

    [Authorize]
    [HttpGet("me")]
    public async Task<ActionResult<MeResponse>> Me(CancellationToken ct)
    {
        var me = await _auth.GetMeAsync(User, ct);
        return Ok(me);
    }

    [HttpPost("forgot-password")]
    public async Task<ActionResult<ForgotPasswordResponse>> ForgotPassword([FromBody] ForgotPasswordRequest request, CancellationToken ct)
    {
        await _auth.ForgotPasswordAsync(request, ct);
        return Ok(new ForgotPasswordResponse
        {
            ResetCode = ResolveDevResetCode(request.Email)
        });
    }

    [HttpPost("verify-reset-code")]
    public async Task<IActionResult> VerifyResetCode([FromBody] VerifyResetCodeRequest request, CancellationToken ct)
    {
        await _auth.VerifyResetCodeAsync(request, ct);
        return NoContent();
    }

    [HttpPost("reset-password")]
    public async Task<IActionResult> ResetPassword([FromBody] ResetPasswordRequest request, CancellationToken ct)
    {
        await _auth.ResetPasswordAsync(request, ct);
        return NoContent();
    }

    private void SetRefreshCookie(string token)
    {
        var options = new CookieOptions
        {
            HttpOnly = _cookieOptions.HttpOnly,
            Secure = _cookieOptions.Secure,
            SameSite = ParseSameSite(_cookieOptions.SameSite),
            Path = _cookieOptions.Path,
            Domain = _cookieOptions.Domain,
            MaxAge = TimeSpan.FromDays(_jwtOptions.RefreshTokenDays)
        };

        Response.Cookies.Append(_cookieOptions.RefreshCookieName, token, options);
    }

    private void ClearRefreshCookie()
    {
        var options = new CookieOptions
        {
            HttpOnly = _cookieOptions.HttpOnly,
            Secure = _cookieOptions.Secure,
            SameSite = ParseSameSite(_cookieOptions.SameSite),
            Path = _cookieOptions.Path,
            Domain = _cookieOptions.Domain
        };
        Response.Cookies.Delete(_cookieOptions.RefreshCookieName, options);
    }

    private static SameSiteMode ParseSameSite(string value)
        => Enum.TryParse<SameSiteMode>(value, ignoreCase: true, out var mode) ? mode : SameSiteMode.Strict;

    private string? ResolveDevVerificationCode(string email)
    {
        if (_emailSender is not NoopEmailSender)
        {
            return null;
        }

        return _verificationCodeStore.GetActiveCode(email, TimeSpan.FromMinutes(15));
    }

    private string? ResolveDevResetCode(string email)
    {
        if (_emailSender is not NoopEmailSender)
        {
            return null;
        }

        return _passwordResetCodeStore.GetActiveCode(email, TimeSpan.FromMinutes(15));
    }
}
