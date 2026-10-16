using VoiceKassa.Domain.Enums;

namespace VoiceKassa.Application.DTOs;

// ---------- Bron (Reservation) ----------

/// <summary>Yangi bron yaratish (admin panel yoki REST).</summary>
public class CreateReservationRequest
{
    public long BusinessId { get; set; }
    public long TableId { get; set; }

    /// <summary>Bron sanasi (yyyy-MM-dd). Bugundan oldin bo'lishi mumkin emas.</summary>
    public DateTime BookingDate { get; set; }

    /// <summary>Boshlanish vaqti (ixtiyoriy; null bo'lsa — bron kun boshidan yopadi).</summary>
    public TimeSpan? StartTime { get; set; }

    /// <summary>Tugash vaqti (ixtiyoriy; null bo'lsa — bron kun oxirigacha yopadi).</summary>
    public TimeSpan? EndTime { get; set; }

    public string? CustomerName { get; set; }
    public string? CustomerPhone { get; set; }
}

/// <summary>Joyida bron (walk-in): mijoz hozir stolga o'tirdi — sana bugun.</summary>
public class WalkInReservationRequest
{
    public long BusinessId { get; set; }
    public long TableId { get; set; }
    public string? CustomerName { get; set; }
    public string? CustomerPhone { get; set; }
}

/// <summary>Kod bilan kirish (enter). Ochiq endpoint — urinishlar cheklanadi.</summary>
public class EnterReservationRequest
{
    /// <summary>6 raqamli bron kodi.</summary>
    public string Code { get; set; } = string.Empty;
}

/// <summary>Kod bilan kirish javobi: sessiya kaliti va stol ma'lumoti.</summary>
public class EnterReservationResponse
{
    /// <summary>Keyingi barcha so'rovlar shu token bilan X-Table-Token sifatida yuboriladi.</summary>
    public string SessionToken { get; set; } = string.Empty;

    public long ReservationId { get; set; }
    public long BusinessId { get; set; }
    public long TableId { get; set; }
    public string TableName { get; set; } = string.Empty;
    public ReservationStatus Status { get; set; }
}

/// <summary>Bron yozuvi (admin ro'yxati uchun).</summary>
public class ReservationResponse
{
    public long Id { get; set; }
    public long BusinessId { get; set; }
    public long TableId { get; set; }
    public string TableName { get; set; } = string.Empty;
    public DateTime BookingDate { get; set; }
    public TimeSpan? StartTime { get; set; }
    public TimeSpan? EndTime { get; set; }
    public string? CustomerName { get; set; }
    public string? CustomerPhone { get; set; }
    public string Code { get; set; } = string.Empty;
    public ReservationStatus Status { get; set; }

    /// <summary>Mijoz hozir sessiya ochganmi (token mavjudmi).</summary>
    public bool HasActiveSession { get; set; }
    public DateTime CreatedAt { get; set; }
}