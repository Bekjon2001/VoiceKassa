using System.Collections.Concurrent;

namespace VoiceKassa.AiServices;

/// <summary>
/// Jarvis suhbat xotirasi — har bir sessiya uchun so'nggi MaxTurns (6) ta
/// almashinuvni (user matni + Jarvis javobi) RAM'da saqlaydi.
/// Singleton sifatida ro'yxatdan o'tkaziladi: server qayta ishga tushmagan
/// ekan xotira saqlanadi; restart bo'lsa tozalanadi (baza yo'q — bu me'yor).
/// </summary>
public class JarvisConversationMemory
{
    /// <summary>Kalit: sessionKey (super-admin token yoki "owner:{businessId}").</summary>
    private readonly ConcurrentDictionary<string, Queue<(string User, string Jarvis)>> _history = new();

    /// <summary>Saqlanadigan almashinuvlar soni — oshsa eng eskilari tashlab yuboriladi.</summary>
    private const int MaxTurns = 6;

    /// <summary>
    /// Sessiya tarixiga yangi almashinuvni qo'shadi (MaxTurns dan oshsa eng
    /// eski yozuv avtomatik chiqib ketadi). Bo'sh matnlar saqlanmaydi.
    /// </summary>
    public void Add(string sessionKey, string userText, string jarvisText)
    {
        if (string.IsNullOrWhiteSpace(sessionKey)) return;

        var user = string.IsNullOrWhiteSpace(userText) ? "" : userText.Trim();
        var jarvis = string.IsNullOrWhiteSpace(jarvisText) ? "" : jarvisText.Trim();
        if (user.Length == 0 && jarvis.Length == 0) return; // faqat matn, bo'sh yozuv kerak emas

        var queue = _history.GetOrAdd(sessionKey, _ => new Queue<(string User, string Jarvis)>());
        lock (queue)
        {
            queue.Enqueue((user, jarvis));
            while (queue.Count > MaxTurns) queue.Dequeue();
        }
    }

    /// <summary>
    /// Oxirgi almashinuvlarni "Foydalanuvchi: ...\nJarvis: ..." formatida qaytaradi;
    /// tarix bo'lmasa bo'sh satr qaytadi.
    /// </summary>
    public string GetHistoryText(string sessionKey)
    {
        if (string.IsNullOrWhiteSpace(sessionKey)) return "";
        if (!_history.TryGetValue(sessionKey, out var queue)) return "";

        lock (queue)
        {
            if (queue.Count == 0) return "";

            var lines = new List<string>();
            foreach (var (user, jarvis) in queue)
            {
                if (!string.IsNullOrWhiteSpace(user)) lines.Add($"Foydalanuvchi: {user}");
                if (!string.IsNullOrWhiteSpace(jarvis)) lines.Add($"Jarvis: {jarvis}");
            }
            return lines.Count == 0 ? "" : string.Join("\n", lines);
        }
    }

    /// <summary>Sessiya tarixini tozalaydi (masalan "suhbatni tozalash" tugmasi uchun).</summary>
    public void Clear(string sessionKey)
    {
        if (string.IsNullOrWhiteSpace(sessionKey)) return;
        _history.TryRemove(sessionKey, out _);
    }
}