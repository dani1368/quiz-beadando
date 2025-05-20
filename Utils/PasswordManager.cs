using System.Security.Cryptography;
using System.Text;

namespace Backend.Utils;

public static class PasswordManager
{
    public static string GenerateSalt()
    {
        var saltBytes = new byte[16];
        using var rng = RandomNumberGenerator.Create();
        rng.GetBytes(saltBytes);
        return Convert.ToBase64String(saltBytes);
    }

    public static string GeneratePasswordHash(string password, string salt)
    {
        using var sha256 = SHA256.Create();
        var combined = Encoding.UTF8.GetBytes(password + salt);
        var hash = sha256.ComputeHash(combined);
        return Convert.ToBase64String(hash);
    }

    public static bool Verify(string password, string salt, string expectedHash)
    {
        var hash = GeneratePasswordHash(password, salt);
        var hashBytes = Encoding.UTF8.GetBytes(hash);
        var expectedBytes = Encoding.UTF8.GetBytes(expectedHash);

        return CryptographicOperations.FixedTimeEquals(hashBytes, expectedBytes);
    }
}