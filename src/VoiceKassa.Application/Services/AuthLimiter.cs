using System.Collections.Concurrent;

namespace VoiceKassa.Application.Services;

/// <summary>
/// Login endpointlariga rate limiting (cheklov):
///  - per-IP (haqiqiy client IP, ForwardedHeaders orqali o'lgan)
///  - umumiylimit (barcha IP'dan noto'g'ri urinishlar)
/// In-memory, singleton. MVP uchun yetarli.
/// </summary>
public class AuthLimiter
{
    private const int MaxPerIpPerMinute = 5;
    private const int MaxGlobalPerMinute = 50;
    private static readonly TimeSpan Window = TimeSpan.FromMinutes(1);

    private readonly ConcurrentDictionary<string, WindowCounter> _ipCounters = new();
    private readonly WindowCounter _globalCounter = new(DateTime.UtcNow);

    public bool TryEnter(string ipKey)
    {
        if (string.IsNullOrWhiteSpace(ipKey)) ipKey = "unknown";
        var now = DateTime.UtcNow;

        lock (_globalCounter)
        {
            if (now - _globalCounter.WindowStart >= Window)
            {
                _globalCounter.WindowStart = now;
                _globalCounter.Count = 0;
            }
        }

        var ipCounter = _ipCounters.GetOrAdd(ipKey, _ => new WindowCounter(now));
        lock (ipCounter)
        {
            if (now - ipCounter.WindowStart >= Window)
            {
                ipCounter.WindowStart = now;
                ipCounter.Count = 0;
            }

            lock (_globalCounter)
            {
                if (now - _globalCounter.WindowStart >= Window)
                {
                    _globalCounter.WindowStart = now;
                    _globalCounter.Count = 0;
                }
            }

            if (ipCounter.Count >= MaxPerIpPerMinute ||
                _globalCounter.Count >= MaxGlobalPerMinute)
                return false;

            ipCounter.Count++;
            _globalCounter.Count++;
            return true;
        }
    }

    private sealed class WindowCounter
    {
        public WindowCounter(DateTime start) => WindowStart = start;
        public DateTime WindowStart;
        public int Count;
    }
}