namespace AuthService.Configuration;

public class JwtOptions
{
    public string Issuer { get; set; } = string.Empty;
    public string Audience { get; set; } = string.Empty;
    public int AccessTokenMinutes { get; set; } = 30;
    public int RefreshTokenDays { get; set; } = 30;
    public string CurrentKeyId { get; set; } = string.Empty;
    public List<JwtKeyOptions> Keys { get; set; } = new();
}

public class JwtKeyOptions
{
    public string KeyId { get; set; } = string.Empty;
    public string SecretBase64 { get; set; } = string.Empty;
}
