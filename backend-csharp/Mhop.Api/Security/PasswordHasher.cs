using System.Security.Cryptography;
using System.Text;

namespace Mhop.Security;

/// <summary>
/// 与 Python 版完全相同的密码格式：pbkdf2_sha256$120000$salthex$hashhex
/// 因此现有数据库里的用户（含 admin/admin123）无需改密即可登录 C# 版。
/// </summary>
public static class PasswordHasher
{
    private const int Rounds = 120_000;
    private const int KeyLength = 32;

    public static string Hash(string password)
    {
        var salt = RandomNumberGenerator.GetBytes(16);
        var dk = Rfc2898DeriveBytes.Pbkdf2(
            Encoding.UTF8.GetBytes(password), salt, Rounds, HashAlgorithmName.SHA256, KeyLength);
        return $"pbkdf2_sha256${Rounds}${Convert.ToHexString(salt).ToLowerInvariant()}${Convert.ToHexString(dk).ToLowerInvariant()}";
    }

    public static bool Verify(string password, string stored)
    {
        try
        {
            var parts = stored.Split('$');
            if (parts is not [_, var roundsStr, var saltHex, var hashHex]) return false;
            var rounds = int.Parse(roundsStr);
            var salt = Convert.FromHexString(saltHex);
            var expected = Convert.FromHexString(hashHex);
            var dk = Rfc2898DeriveBytes.Pbkdf2(
                Encoding.UTF8.GetBytes(password), salt, rounds, HashAlgorithmName.SHA256, expected.Length);
            return CryptographicOperations.FixedTimeEquals(dk, expected);
        }
        catch
        {
            return false;
        }
    }
}
