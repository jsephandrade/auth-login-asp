using System.Security.Cryptography;
using System.Text;

namespace AuthService.Security;

public interface ITokenHasher
{
    byte[] HashToken(string token);
}

public class TokenHasher : ITokenHasher
{
    private readonly ISecretProvider _secrets;

    public TokenHasher(ISecretProvider secrets)
    {
        _secrets = secrets;
    }

    public byte[] HashToken(string token)
    {
        var pepper = _secrets.GetPepper();
        var input = Encoding.UTF8.GetBytes(token + pepper);
        return SHA256.HashData(input);
    }
}

public interface ITokenGenerator
{
    string GenerateToken(int bytes = 32);
}

public class TokenGenerator : ITokenGenerator
{
    public string GenerateToken(int bytes = 32)
    {
        var data = RandomNumberGenerator.GetBytes(bytes);
        return Base64UrlEncode(data);
    }

    private static string Base64UrlEncode(byte[] data)
        => Convert.ToBase64String(data).TrimEnd('=').Replace('+', '-').Replace('/', '_');
}
