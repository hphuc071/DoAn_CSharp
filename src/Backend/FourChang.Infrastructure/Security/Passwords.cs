using System.Security.Cryptography;
namespace FourChang.Infrastructure.Security;

public static class Passwords
{
    public static string Hash(string password)
    {
        var salt = RandomNumberGenerator.GetBytes(16);
        var hash = Rfc2898DeriveBytes.Pbkdf2(password, salt, 210_000, HashAlgorithmName.SHA512, 32);
        return $"PBKDF2-SHA512:210000:{Convert.ToBase64String(salt)}:{Convert.ToBase64String(hash)}";
    }
    public static bool Verify(string password, string stored)
    {
        try
        {
            var parts = stored.Split(':');
            if (parts.Length != 4 || parts[0] != "PBKDF2-SHA512" || !int.TryParse(parts[1], out var iterations) || iterations is < 100_000 or > 1_000_000) return false;
            var expected = Convert.FromBase64String(parts[3]);
            var actual = Rfc2898DeriveBytes.Pbkdf2(password, Convert.FromBase64String(parts[2]), iterations, HashAlgorithmName.SHA512, expected.Length);
            return CryptographicOperations.FixedTimeEquals(expected, actual);
        }
        catch (FormatException) { return false; }
    }
    public static byte[] TokenHash(string token) => SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(token));
}
