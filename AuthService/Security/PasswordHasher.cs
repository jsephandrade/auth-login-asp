using BCrypt.Net;

namespace AuthService.Security;

public interface IPasswordHasher
{
    string HashPassword(string password);
    bool Verify(string password, string hash);
}

public class BcryptPasswordHasher : IPasswordHasher
{
    private readonly int _workFactor;

    public BcryptPasswordHasher(IConfiguration configuration)
    {
        _workFactor = int.TryParse(configuration["Security:BcryptWorkFactor"], out var wf) ? wf : 12;
    }

    public string HashPassword(string password)
        => BCrypt.Net.BCrypt.HashPassword(password, workFactor: _workFactor);

    public bool Verify(string password, string hash)
        => BCrypt.Net.BCrypt.Verify(password, hash);
}
