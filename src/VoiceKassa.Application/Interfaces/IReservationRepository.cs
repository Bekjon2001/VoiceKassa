using VoiceKassa.Domain.Entities;

namespace VoiceKassa.Application.Interfaces;

/// <summary>Bron (RESERVATIONS) jadvali bo'yicha mantiqiy qatlam.</summary>
public interface IReservationRepository
{
    Task<Reservation> AddAsync(Reservation reservation, CancellationToken ct = default);
    Task<Reservation?> GetByIdAsync(long reservationId, CancellationToken ct = default);

    /// <summary>6 raqamli kod bo'yicha bron topadi (kod bilan kirish uchun).</summary>
    Task<Reservation?> GetByCodeAsync(string code, CancellationToken ct = default);

    /// <summary>Sessiya kaliti bo'yicha bron topadi (X-Table-Token tekshiruvi uchun).</summary>
    Task<Reservation?> GetBySessionTokenAsync(string sessionToken, CancellationToken ct = default);

    /// <summary>Shu stol uchun shu sanada faol (Waiting/Arrived) bron bormi — yaratishdan oldin tekshiriladi.</summary>
    Task<bool> HasActiveForTableOnDateAsync(long tableId, DateTime bookingDate, CancellationToken ct = default);

    /// <summary>Stol bo'shmi: na faol bron, na ochiq buyurtma yo'qmi (joyida bron uchun).</summary>
    Task<bool> IsTableFreeAsync(long tableId, DateTime bookingDate, CancellationToken ct = default);

    /// <summary>Stolning shu sanadagi faol bronlari (admin ro'yxati uchun).</summary>
    Task<List<Reservation>> GetByTableAndDateAsync(long tableId, DateTime bookingDate, CancellationToken ct = default);

    /// <summary>Biznesdagi bronlar (ixtiyoriy sana filtri bilan, admin ro'yxati uchun).</summary>
    Task<List<Reservation>> GetByBusinessAsync(long businessId, DateTime? bookingDate, CancellationToken ct = default);

    Task SaveChangesAsync(CancellationToken ct = default);
}