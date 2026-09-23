namespace VoiceKassa.Application.Services;

/// <summary>O'zbekiston (UTC+5) bo'yicha "bugun" oralig'ini UTC ga aylantiradi.</summary>
public static class LocalClock
{
    public static readonly TimeSpan UzbekistanOffset = TimeSpan.FromHours(5);

    public static (DateTime FromUtc, DateTime ToUtc) TodayRangeUtc(DateTime? utcNow = null)
    {
        var nowUtc = utcNow ?? DateTime.UtcNow;
        if (nowUtc.Kind == DateTimeKind.Unspecified)
            nowUtc = DateTime.SpecifyKind(nowUtc, DateTimeKind.Utc);

        var local = nowUtc + UzbekistanOffset;
        var startLocal = local.Date;
        var fromUtc = DateTime.SpecifyKind(startLocal - UzbekistanOffset, DateTimeKind.Utc);
        var toUtc = fromUtc.AddDays(1);
        return (fromUtc, toUtc);
    }
}
