namespace AuthService.Configuration;

public class AuthCookieOptions
{
    public string RefreshCookieName { get; set; } = "refresh_token";
    public string Path { get; set; } = "/auth/refresh";
    public string SameSite { get; set; } = "Strict";
    public bool Secure { get; set; } = true;
    public bool HttpOnly { get; set; } = true;
    public string? Domain { get; set; }
}
