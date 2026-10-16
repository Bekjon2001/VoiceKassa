using System.Collections.Concurrent;

namespace VoiceKassa.Application.Services;

/// <summary>
/// Bron kodi bilan kirish (enter) uchun oddiy cheklov: bitta IP uchun
/// 10 daqiqada 5 dan ortiq urinishga yo'l qo'yilmaydi. In-memory
/// (singleton) — server qayta ishga tushsa hisobga qaytadi; MVP uchun
/// yetarli, production'da Redis/asp.net rate-limiting almashtiriladi.
/// </summary>
public class ReservationEnterLimiter
{
    private const int MaxAttempts = 5;
    private static readonly TimeSpan Window = TimeSpan.FromMinutes(10);

    private readonly ConcurrentDictionary<string, WindowCounter> _counters = new();

    /// <summary>Urinishga ruxsat bormi (false — limit tugagan, 429 qaytarish kerak).</summary>
    public bool TryEnter(string key)
    {
        if (string.IsNullOrWhiteSpace(key)) key = "unknown";
        var now = DateTime.UtcNow;
        var counter = _counters.GetOrAdd(key, _ => new WindowCounter(now));

        lock (counter)
        {
            if (now - counter.WindowStart >= Window)
            {
                counter.WindowStart = now;
                counter.Count = 0;
            }

            if (counter.Count >= MaxAttempts) return false;
            counter.Count++;
            return true;
        }
    }

    /// <summary>Muvaffaqiyatli kirishdan keyin hisobni tozalaydi.</summary>
    public void Reset(string key)
    {
        if (string.IsNullOrWhiteSpace(key)) return;
        _counters.TryRemove(key, out _);
    }

    private sealed class WindowCounter
    {
        public WindowCounter(DateTime start) => WindowStart = start;
        public DateTime WindowStart;
        public int Count;
    }
}