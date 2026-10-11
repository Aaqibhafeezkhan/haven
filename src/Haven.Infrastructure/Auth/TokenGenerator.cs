using System.Security.Cryptography;
using System.Text;

namespace Haven.Infrastructure.Auth;

public static class TokenGenerator
{
    // Returns the plaintext token (show once) plus the values you persist.
    public static (string PlainText, string Hash, string Prefix) Generate(string appPrefix = "myapp")
    {
        var bytes = RandomNumberGenerator.GetBytes(32); // 256 bits of entropy
        var secret = Base64UrlEncode(bytes);
        var plain = $"{appPrefix}_{secret}";

        return (plain, Hash(plain), plain[..Math.Min(plain.Length, 12)]);
    }

    public static string Hash(string token) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(token)));

    private static string Base64UrlEncode(byte[] data) =>
        Convert.ToBase64String(data).TrimEnd('=').Replace('+', '-').Replace('/', '_');
}