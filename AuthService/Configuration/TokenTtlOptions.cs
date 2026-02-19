namespace AuthService.Configuration;

public class TokenTtlOptions
{
    public int EmailVerificationHours { get; set; } = 24;
    public int PasswordResetMinutes { get; set; } = 60;
}
