using System.Security.Cryptography;
using System.Text;

namespace VoiceKassa.Api;

public sealed class SetupKeyOptions
{
    public string? FirstSuperAdminKey { get; set; }
}

public static class SetupKeyValidator
{
    public static bool IsAuthorized(string? configuredKey, string? providedKey, bool isDevelopment)
    {
        if (isDevelopment) return true;
        if (string.IsNullOrEmpty(configuredKey) || string.IsNullOrEmpty(providedKey)) return false;
        return CryptographicOperations.FixedTimeEquals(
            Encoding.UTF8.GetBytes(configuredKey),
            Encoding.UTF8.GetBytes(providedKey));
    }
}
