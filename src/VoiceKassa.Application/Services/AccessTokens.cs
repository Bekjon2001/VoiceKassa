namespace VoiceKassa.Application.Services;

public static class AccessTokens
{
    public static string New() => Guid.NewGuid().ToString("N") + Guid.NewGuid().ToString("N");
}
