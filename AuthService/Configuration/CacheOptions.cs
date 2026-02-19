namespace AuthService.Configuration;

public class CacheOptions
{
    public int SessionSeconds { get; set; } = 120;
    public int PermissionSeconds { get; set; } = 300;
}
