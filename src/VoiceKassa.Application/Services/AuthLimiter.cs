using System.Collections.Concurrent;

namespace VoiceKassa.Application.Services;

/// <summary>
/// Login endpointlarida faqat muvaffaqiyatsiz urinishlarni IP bo'yicha cheklaydi.
/// In-memory, singleton. MVP uchun yetarli.
/// </summary>
public class AuthLimiter
{
    private const int MaxPerIpPerMinute = 5;
    private static readonly TimeSpan Window = TimeSpan.FromMinutes(1);

    private readonly ConcurrentDictionary<string, WindowCounter> _ipCounters = new();

    public bool TryEnter(string ipKey)
    {
        ipKey = NormalizeIp(ipKey);
        var now = DateTime.UtcNow;
        if (!_ipCounters.TryGetValue(ipKey, out var counter))
            return true;

        lock (counter)
        {
            ResetIfExpired(counter, now);
            return counter.Count < MaxPerIpPerMinute;
        }
    }

    public void RegisterFailure(string ipKey)
    {
        ipKey = NormalizeIp(ipKey);
        var now = DateTime.UtcNow;
        var counter = _ipCounters.GetOrAdd(ipKey, _ => new WindowCounter(now));
        lock (counter)
        {
            ResetIfExpired(counter, now);
            counter.Count++;
        }
    }

    public void RegisterSuccess(string ipKey)
    {
        if (!_ipCounters.TryGetValue(NormalizeIp(ipKey), out var counter))
            return;

        lock (counter)
        {
            counter.WindowStart = DateTime.UtcNow;
            counter.Count = 0;
        }
    }

    private static string NormalizeIp(string ipKey) =>
        string.IsNullOrWhiteSpace(ipKey) ? "unknown" : ipKey;

    private static void ResetIfExpired(WindowCounter counter, DateTime now)
    {
        if (now - counter.WindowStart < Window) return;
        counter.WindowStart = now;
        counter.Count = 0;
    }

    private sealed class WindowCounter
    {
        public WindowCounter(DateTime start) => WindowStart = start;
        public DateTime WindowStart;
        public int Count;
    }
}