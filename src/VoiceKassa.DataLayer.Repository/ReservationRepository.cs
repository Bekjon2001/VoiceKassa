using Microsoft.EntityFrameworkCore;
using VoiceKassa.Application.Interfaces;
using VoiceKassa.DataLayer;
using VoiceKassa.Domain.Entities;
using VoiceKassa.Domain.Enums;

namespace VoiceKassa.DataLayer.Repository;

public class ReservationRepository : IReservationRepository
{
    private readonly AppDbContext _db;

    public ReservationRepository(AppDbContext db) => _db = db;

    public async Task<Reservation> AddAsync(Reservation reservation, CancellationToken ct = default)
    {
        _db.Reservations.Add(reservation);
        await _db.SaveChangesAsync(ct);
        return reservation;
    }

    public Task<Reservation?> GetByIdAsync(long reservationId, CancellationToken ct = default) =>
        _db.Reservations.FirstOrDefaultAsync(r => r.Id == reservationId, ct);

    public Task<Reservation?> GetByCodeAsync(string code, CancellationToken ct = default) =>
        _db.Reservations.FirstOrDefaultAsync(r => r.Code == code, ct);

    public Task<Reservation?> GetBySessionTokenAsync(string sessionToken, CancellationToken ct = default) =>
        _db.Reservations.FirstOrDefaultAsync(r => r.SessionToken == sessionToken, ct);

    public Task<bool> HasActiveForTableOnDateAsync(long tableId, DateTime bookingDate, CancellationToken ct = default) =>
        _db.Reservations.AnyAsync(r =>
            r.TableId == tableId &&
            r.BookingDate == bookingDate &&
            (r.Status == ReservationStatus.Waiting || r.Status == ReservationStatus.Arrived), ct);

    public async Task<bool> IsTableFreeAsync(long tableId, DateTime bookingDate, CancellationToken ct = default)
    {
        var hasActiveReservation = await _db.Reservations.AnyAsync(r =>
            r.TableId == tableId &&
            r.BookingDate == bookingDate &&
            (r.Status == ReservationStatus.Waiting || r.Status == ReservationStatus.Arrived), ct);
        if (hasActiveReservation) return false;

        // Ochiq buyurtma ham stolni band qiladi — walk-in'da ham tekshiriladi.
        var hasOpenOrder = await _db.Orders.AnyAsync(o =>
            o.TableId == tableId &&
            (o.Status == OrderStatus.Open || o.Status == OrderStatus.InProgress), ct);
        return !hasOpenOrder;
    }

    public Task<List<Reservation>> GetByTableAndDateAsync(long tableId, DateTime bookingDate, CancellationToken ct = default) =>
        _db.Reservations
            .Include(r => r.Table)
            .Where(r => r.TableId == tableId && r.BookingDate == bookingDate)
            .OrderBy(r => r.StartTime)
            .ThenBy(r => r.Id)
            .ToListAsync(ct);

    public Task<List<Reservation>> GetByBusinessAsync(long businessId, DateTime? bookingDate, CancellationToken ct = default)
    {
        var query = _db.Reservations
            .Include(r => r.Table)
            .Where(r => r.BusinessId == businessId);

        if (bookingDate.HasValue)
            query = query.Where(r => r.BookingDate == bookingDate.Value);

        return query
            .OrderByDescending(r => r.BookingDate)
            .ThenBy(r => r.TableId)
            .ToListAsync(ct);
    }

    public Task SaveChangesAsync(CancellationToken ct = default) => _db.SaveChangesAsync(ct);
}