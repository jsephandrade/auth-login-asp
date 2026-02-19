namespace AuthService.Security;

public interface ISecretProvider
{
    string GetPepper();
}

public class StaticSecretProvider : ISecretProvider
{
    private readonly string _pepper;

    public StaticSecretProvider(IConfiguration configuration)
    {
        _pepper = configuration["Security:Pepper"] ?? string.Empty;
        if (string.IsNullOrWhiteSpace(_pepper))
        {
            throw new InvalidOperationException("Security:Pepper must be configured.");
        }
    }

    public string GetPepper() => _pepper;
}
