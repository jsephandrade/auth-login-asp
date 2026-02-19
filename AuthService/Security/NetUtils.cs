using System.Net;

namespace AuthService.Security;

public static class NetUtils
{
    public static byte[]? IpToBytes(string? ip)
    {
        if (string.IsNullOrWhiteSpace(ip)) return null;
        return IPAddress.Parse(ip).GetAddressBytes();
    }

    public static byte[]? HashUserAgent(string? userAgent)
    {
        if (string.IsNullOrWhiteSpace(userAgent)) return null;
        return System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(userAgent));
    }
}
