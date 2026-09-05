using System.Security.Cryptography;

namespace VoiceKassa.Application.Services;

/// <summary>
/// Parolni hashlash bo'yicha yagona manba.
///
/// Yangi parollar PBKDF2 (SHA-256, 120_000 iteratsiya) bilan hashlanadi va
/// "salt.hash" (base64) ko'rinishida saqlanadi — BusinessService avval ham
/// shu formatdan foydalanardi, DB dagi mavjud yozuvlar shu formatda.
///
/// Verify() esa IKKALA formatni taniydi:
///   - Eski/yangi BCrypt hashlar ($2a$/$2b$/$2y$) — BCrypt.Net orqali;
///   - PBKDF2 "salt.hash" — Rfc2898DeriveBytes orqali.
/// Bu orqali bazadagi eski BCrypt yozuvlar ham, yangi PBKDF2 yozuvlar ham
/// muammosiz tekshiriladi (bir-biriga o'tish davrida xavfsiz).
/// </summary>
public static class PasswordHasher
{
    private const int Iterations = 120_000;
    private const int SaltSize = 16;
    private const int HashSize = 32;

    public static string Hash(string password)
    {
        var salt = RandomNumberGenerator.GetBytes(SaltSize);
        var hash = Rfc2898DeriveBytes.Pbkdf2(password, salt, Iterations, HashAlgorithmName.SHA256, HashSize);
        return $"{Convert.ToBase64String(salt)}.{Convert.ToBase64String(hash)}";
    }

    public static bool Verify(string password, string? storedHash)
    {
        if (string.IsNullOrWhiteSpace(storedHash))
            return false;

        // Legacy: BCrypt formati ("$2a$...", "$2b$...", "$2y$...").
        if (storedHash.StartsWith("$2", StringComparison.Ordinal))
        {
            try
            {
                return BCrypt.Net.BCrypt.Verify(password, storedHash);
            }
            catch
            {
                return false;
            }
        }

        // Standart: PBKDF2 "salt.hash".
        var parts = storedHash.Split('.', 2);
        if (parts.Length != 2)
            return false;

        try
        {
            var salt = Convert.FromBase64String(parts[0]);
            var expected = Convert.FromBase64String(parts[1]);
            var actual = Rfc2898DeriveBytes.Pbkdf2(password, salt, Iterations, HashAlgorithmName.SHA256, HashSize);
            return CryptographicOperations.FixedTimeEquals(actual, expected);
        }
        catch
        {
            return false;
        }
    }
}