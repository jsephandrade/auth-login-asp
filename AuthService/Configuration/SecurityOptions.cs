namespace AuthService.Configuration;

public class SecurityOptions
{
    public string Pepper { get; set; } = string.Empty;
    public int BcryptWorkFactor { get; set; } = 12;
}
