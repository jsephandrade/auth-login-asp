using System.ComponentModel.DataAnnotations;

namespace AuthService.Contracts;

public class RegisterRequest
{
    [Required, EmailAddress, MaxLength(320)]
    public string Email { get; set; } = string.Empty;

    [Required, MinLength(8), MaxLength(100)]
    public string Password { get; set; } = string.Empty;

    public Guid? TenantId { get; set; }

    [MaxLength(255)]
    public string? TenantName { get; set; }
}

public class RegisterResponse
{
    public Guid TenantId { get; set; }
    public string? VerificationCode { get; set; }
}

public class VerifyEmailRequest
{
    [Required, EmailAddress, MaxLength(320)]
    public string Email { get; set; } = string.Empty;

    [Required, RegularExpression(@"^\d{6}$")]
    public string Code { get; set; } = string.Empty;
}

public class ResendVerificationCodeRequest
{
    [Required, EmailAddress, MaxLength(320)]
    public string Email { get; set; } = string.Empty;
}

public class ResendVerificationCodeResponse
{
    public string? VerificationCode { get; set; }
}

public class LoginRequest
{
    [Required, EmailAddress, MaxLength(320)]
    public string Email { get; set; } = string.Empty;

    [Required]
    public string Password { get; set; } = string.Empty;

    public Guid? TenantId { get; set; }
}

public class RefreshResponse
{
    public string AccessToken { get; set; } = string.Empty;
}

public class ForgotPasswordRequest
{
    [Required, EmailAddress, MaxLength(320)]
    public string Email { get; set; } = string.Empty;
}

public class ForgotPasswordResponse
{
    public string? ResetCode { get; set; }
}

public class VerifyResetCodeRequest
{
    [Required, EmailAddress, MaxLength(320)]
    public string Email { get; set; } = string.Empty;

    [Required, RegularExpression(@"^\d{6}$")]
    public string Code { get; set; } = string.Empty;
}

public class ResetPasswordRequest
{
    [Required, EmailAddress, MaxLength(320)]
    public string Email { get; set; } = string.Empty;

    [Required, RegularExpression(@"^\d{6}$")]
    public string Code { get; set; } = string.Empty;

    [Required, MinLength(8), MaxLength(100)]
    public string NewPassword { get; set; } = string.Empty;
}

public class MeResponse
{
    public Guid UserId { get; set; }
    public Guid TenantId { get; set; }
    public string Email { get; set; } = string.Empty;
    public IReadOnlyList<string> Roles { get; set; } = Array.Empty<string>();
    public IReadOnlyList<string> Permissions { get; set; } = Array.Empty<string>();
}
