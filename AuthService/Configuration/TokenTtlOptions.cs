namespace AuthService.Configuration;

public class TokenTtlOptions
{
    public int EmailVerificationMinutes { get; set; } = 15;
    public int PasswordResetMinutes { get; set; } = 60;
}
