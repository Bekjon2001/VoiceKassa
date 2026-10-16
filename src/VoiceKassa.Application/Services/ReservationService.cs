using System.Security.Cryptography;
using VoiceKassa.Application.DTOs;
using VoiceKassa.Application.Interfaces;
using VoiceKassa.Domain.Entities;
using VoiceKassa.Domain.Enums;

namespace VoiceKassa.Application.Services;

/// <summary>
/// Bron tizimi: yaratish (to'qnashuv tekshiruvi + 6 raqamli kod), kod bilan
/// kirish (SESSION_TOKEN beriladi, status "Kelgan"), joyida bron (stol
/// bo'shligi tekshirilib darhol yaratiladi) va bekor qilish.
/// </summary>
public class ReservationService
{
    private readonly IReservationRepository _repo;
    private readonly IBusinessRepository _businessRepo;

    public ReservationService(IReservationRepository repo, IBusinessRepository businessRepo)
    {
        _repo = repo;
        _businessRepo = businessRepo;
    }

    /// <summary>Yangi bron yaratadi. Vaqt kiritilmasa — bron kun bo'yi stolni yopadi.</summary>
    public async Task<(bool Success, string? Error, ReservationResponse? Reservation)> CreateAsync(
        CreateReservationRequest request, CancellationToken ct = default)
    {
        var (ok, error, table, bookingDate) = await ValidateTableAndDateAsync(request.BusinessId, request.TableId, request.BookingDate, ct);
        if (!ok) return (false, error, null);

        var (timeOk, timeError) = ValidateTimes(request.StartTime, request.EndTime);
        if (!timeOk) return (false, timeError, null);

        // Majburiy maydonlar: ism va telefon bo'sh bo'lmasa kerak.
        if (string.IsNullOrWhiteSpace(request.CustomerName))
            return (false, "Ism bo'sh bo'lishi mumkin emas.", null);
        if (string.IsNullOrWhiteSpace(request.CustomerPhone))
            return (false, "Telefon raqam bo'sh bo'lishi mumkin emas.", null);

        // UNIQUE(BUSINESS_ID, TABLE_ID, BOOKING_DATE) — bir stolga bir sanada bitta bron.
        if (await _repo.HasActiveForTableOnDateAsync(table!.Id, bookingDate, ct))
            return (false, "Bu stol shu sanaga allaqachon bron qilingan.", null);

        // Mehmonlar soni 1 dan kam bo'lmasin.
        if (request.GuestCount <= 0)
            return (false, "Mehmonlar soni 1 dan kam bo'lmasin.", null);

        var code = await GenerateUniqueCodeAsync(ct);
        var reservation = new Reservation
        {
            BusinessId = request.BusinessId,
            TableId = table.Id,
            BookingDate = bookingDate,
            StartTime = request.StartTime,
            EndTime = request.EndTime,
            CustomerName = Normalize(request.CustomerName)!,
            CustomerPhone = Normalize(request.CustomerPhone)!,
            GuestCount = request.GuestCount,
            Code = code,
            Status = ReservationStatus.Waiting,
            CreatedAt = DateTime.UtcNow,
        };
        await _repo.AddAsync(reservation, ct);

        // Mehmonlar soni stol sig'imidan oshsa — ogohlantirish, lekin bronni bloklamaydi.
        string? warning = null;
        if (request.GuestCount > table.Capacity)
            warning = $"E'tibor bering: mehmonlar soni ({request.GuestCount}) stollar sig'imidan ({table.Capacity}) ortiq.";

        return (true, null, ToResponse(reservation, table.Name, warning));
    }

    /// <summary>
    /// Joyida bron (walk-in): mijoz hozir stolga o'tirdi. Stol bo'shligi
    /// tekshiriladi, bron darhol "Kelgan" holatida yaratiladi va sessiya
    /// kaliti beriladi — mijoz shu zahoti menyuga kirishi mumkin.
    /// </summary>
    public async Task<(bool Success, string? Error, EnterReservationResponse? Result)> WalkInAsync(
        WalkInReservationRequest request, CancellationToken ct = default)
    {
        // Majburiy maydonlar: ism va telefon (SQL NOT NULL).
        if (string.IsNullOrWhiteSpace(request.CustomerName))
            return (false, "Ism bo'sh bo'lishi mumkin emas.", null);
        if (string.IsNullOrWhiteSpace(request.CustomerPhone))
            return (false, "Telefon raqam bo'sh bo'lishi mumkin emas.", null);

        var todayUtc = (DateTime.UtcNow + LocalClock.UzbekistanOffset).Date;
        var (ok, error, table, bookingDate) = await ValidateTableAndDateAsync(request.BusinessId, request.TableId, todayUtc, ct);
        if (!ok) return (false, error, null);

        if (!await _repo.IsTableFreeAsync(table!.Id, bookingDate, ct))
            return (false, "Stol hozir band (faol bron yoki ochiq buyurtma bor).", null);

        var reservation = new Reservation
        {
            BusinessId = request.BusinessId,
            TableId = table.Id,
            BookingDate = bookingDate,
            CustomerName = Normalize(request.CustomerName)!,
            CustomerPhone = Normalize(request.CustomerPhone)!,
            GuestCount = request.GuestCount,
            Code = await GenerateUniqueCodeAsync(ct),
            SessionToken = AccessTokens.New(),
            Status = ReservationStatus.Arrived,
            CreatedAt = DateTime.UtcNow,
        };
        await _repo.AddAsync(reservation, ct);

        return (true, null, new EnterReservationResponse
        {
            SessionToken = reservation.SessionToken!,
            ReservationId = reservation.Id,
            BusinessId = reservation.BusinessId,
            TableId = reservation.TableId,
            TableName = table.Name,
            Status = reservation.Status,
        });
    }

    // ---------- Kod bilan kirish / bekor qilish ----------

    /// <summary>Sessiya tokendan bron topadi (OrderController X-Table-Token tekshiruvi uchun).</summary>
    public Task<Reservation?> GetBySessionTokenAsync(string sessionToken, CancellationToken ct = default) =>
        _repo.GetBySessionTokenAsync(sessionToken, ct);

    /// <summary>
    /// Kod bilan kirish: SESSION_TOKEN beriladi va status "Kelgan" bo'ladi.
    /// Yopilgan/bekor qilingan bron bilan kirib bo'lmaydi.
    /// </summary>
    public async Task<(bool Success, string? Error, EnterReservationResponse? Result)> EnterByCodeAsync(
        EnterReservationRequest request, CancellationToken ct = default)
    {
        var code = (request.Code ?? string.Empty).Trim();
        if (code.Length != 6 || !code.All(char.IsDigit))
            return (false, "Bron kodi 6 raqamdan iborat bo'lishi kerak.", null);

        var reservation = await _repo.GetByCodeAsync(code, ct);
        if (reservation is null)
            return (false, "Bunday kodli bron topilmadi.", null);
        if (reservation.Status == ReservationStatus.Cancelled)
            return (false, "Bu bron bekor qilingan.", null);
        if (reservation.Status == ReservationStatus.Closed)
            return (false, "Bu bron yopilgan (hisob allaqachon yopilgan).", null);

        var table = await _businessRepo.GetTableByIdAsync(reservation.TableId, ct);
        if (table is null)
            return (false, "Stol topilmadi.", null);

        // Har bir "kirish" yangi sessiya ochadi (eski token bekor qilinadi) —
        // kodni boshqa qurilmada qayta kiritsa ham ishlaydi.
        reservation.SessionToken = AccessTokens.New();
        if (reservation.Status == ReservationStatus.Waiting)
            reservation.Status = ReservationStatus.Arrived;
        await _repo.SaveChangesAsync(ct);

        return (true, null, new EnterReservationResponse
        {
            SessionToken = reservation.SessionToken,
            ReservationId = reservation.Id,
            BusinessId = reservation.BusinessId,
            TableId = reservation.TableId,
            TableName = table.Name,
            Status = reservation.Status,
        });
    }

    /// <summary>Bekor qilish: sessiya tokeni o'chiriladi, status "Bekor" bo'ladi.</summary>
    public async Task<(bool Success, string? Error, ReservationResponse? Reservation)> CancelAsync(
        long reservationId, long businessId, CancellationToken ct = default)
    {
        var reservation = await _repo.GetByIdAsync(reservationId, ct);
        if (reservation is null) return (false, "Bron topilmadi.", null);
        if (reservation.BusinessId != businessId) return (false, "Bron topilmadi.", null);
        if (reservation.Status == ReservationStatus.Closed)
            return (false, "Yopilgan bronni bekor qilib bo'lmaydi.", null);

        reservation.Status = ReservationStatus.Cancelled;
        reservation.SessionToken = null;
        await _repo.SaveChangesAsync(ct);

        var table = await _businessRepo.GetTableByIdAsync(reservation.TableId, ct);
        return (true, null, ToResponse(reservation, table?.Name ?? $"Stol {reservation.TableId}"));
    }

    /// <summary>
    /// Kodni qayta berish (admin): eski kod o'chiriladi, yangi yagona kod
    /// generatsiya qilinadi. Sessiya saqlanib qoladi.
    /// </summary>
    public async Task<(bool Success, string? Error, ReservationResponse? Reservation)> RegenerateCodeAsync(
        long reservationId, long businessId, CancellationToken ct = default)
    {
        var reservation = await _repo.GetByIdAsync(reservationId, ct);
        if (reservation is null) return (false, "Bron topilmadi.", null);
        if (reservation.BusinessId != businessId) return (false, "Bron topilmadi.", null);
        if (reservation.Status is ReservationStatus.Closed or ReservationStatus.Cancelled)
            return (false, "Yopilgan/bekor qilingan bronning kodini qayta berib bo'lmaydi.", null);

        reservation.Code = await GenerateUniqueCodeAsync(ct);
        await _repo.SaveChangesAsync(ct);

        var table = await _businessRepo.GetTableByIdAsync(reservation.TableId, ct);
        return (true, null, ToResponse(reservation, table?.Name ?? $"Stol {reservation.TableId}"));
    }

    /// <summary>
    /// Hisob yopilganda yoki buyurtma bekor qilinganda chaqiriladi: bron
    /// "Yopilgan" bo'ladi va sessiya kaliti o'chiriladi (kod endi ishlamaydi).
    /// </summary>
    public async Task CloseReservationAsync(long reservationId, CancellationToken ct = default)
    {
        var reservation = await _repo.GetByIdAsync(reservationId, ct);
        if (reservation is null) return;
        if (reservation.Status is ReservationStatus.Closed or ReservationStatus.Cancelled) return;

        reservation.Status = ReservationStatus.Closed;
        reservation.SessionToken = null;
        await _repo.SaveChangesAsync(ct);
    }

    /// <summary>Admin ro'yxati: biznes bronlari (ixtiyoriy sana filtri bilan).</summary>
    public async Task<List<ReservationResponse>> GetListAsync(long businessId, DateTime? bookingDate, CancellationToken ct = default)
    {
        var reservations = await _repo.GetByBusinessAsync(businessId, bookingDate?.Date, ct);
        var tableNames = new Dictionary<long, string>();
        var result = new List<ReservationResponse>();
        foreach (var r in reservations)
        {
            if (!tableNames.TryGetValue(r.TableId, out var name))
            {
                var table = await _businessRepo.GetTableByIdAsync(r.TableId, ct);
                name = table?.Name ?? $"Stol {r.TableId}";
                tableNames[r.TableId] = name;
            }
            result.Add(ToResponse(r, name));
        }
        return result;
    }

    /// <summary>
    /// Mijoz uchun: berilgan sanada bronda bo'lmagan (faol bron yoki ochiq
    /// buyurtma yo'q) va sig'imi yetishadigan stollar. Token shart emas.
    /// </summary>
    public async Task<List<TableResponse>> GetAvailableTablesAsync(
        long businessId, DateTime bookingDate, int minGuests, CancellationToken ct = default)
    {
        var tables = await _businessRepo.GetTablesByBusinessAsync(businessId, ct);
        var date = bookingDate.Date;
        var result = new List<TableResponse>();
        foreach (var table in tables)
        {
            if (table.Capacity < minGuests) continue;
            if (!await _repo.IsTableFreeAsync(table.Id, date, ct)) continue;
            result.Add(new TableResponse
            {
                Id = table.Id,
                BusinessId = table.BusinessId,
                Name = table.Name,
                Capacity = table.Capacity,
                Status = table.Status,
            });
        }
        return result.OrderBy(t => t.Name).ToList();
    }

    // ---------- Yordamchi metodlar ----------

    private async Task<(bool Ok, string? Error, Table? Table, DateTime BookingDate)> ValidateTableAndDateAsync(
        long businessId, long tableId, DateTime bookingDate, CancellationToken ct)
    {
        if (businessId <= 0 || tableId <= 0)
            return (false, "BusinessId/TableId noto'g'ri.", null, default);

        var table = await _businessRepo.GetTableByIdAsync(tableId, ct);
        if (table is null || table.BusinessId != businessId)
            return (false, "Stol shu biznesga tegishli emas.", null, default);

        var normalizedDate = bookingDate.Date;
        var todayUtc = (DateTime.UtcNow + LocalClock.UzbekistanOffset).Date;
        if (normalizedDate < todayUtc)
            return (false, "Bron sanasi bugundan oldin bo'lishi mumkin emas.", null, default);

        return (true, null, table, normalizedDate);
    }

    private static (bool Ok, string? Error) ValidateTimes(TimeSpan? start, TimeSpan? end)
    {
        if (start.HasValue && end.HasValue && end.Value <= start.Value)
            return (false, "Tugash vaqti boshlanish vaqtidan keyin bo'lishi kerak.");
        return (true, null);
    }

    private static string? Normalize(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    /// <summary>6 raqamli yagona kod (100000..999999), takrorlanmaguncha qayta generatsiya.</summary>
    private async Task<string> GenerateUniqueCodeAsync(CancellationToken ct)
    {
        for (var attempt = 0; attempt < 20; attempt++)
        {
            var code = RandomNumberGenerator.GetInt32(100000, 1000000).ToString();
            if (await _repo.GetByCodeAsync(code, ct) is null)
                return code;
        }
        // Amalda bo'lmaydi (10^6 kod maydoni), lekin xavfsizlik uchun.
        throw new InvalidOperationException("Bron kodini generatsiya qilib bo'lmadi — qayta urinib ko'ring.");
    }

    private static ReservationResponse ToResponse(Reservation r, string tableName, string? warning = null) => new()
    {
        Id = r.Id,
        BusinessId = r.BusinessId,
        TableId = r.TableId,
        TableName = tableName,
        BookingDate = r.BookingDate,
        StartTime = r.StartTime,
        EndTime = r.EndTime,
        CustomerName = r.CustomerName,
        CustomerPhone = r.CustomerPhone,
        Code = r.Code,
        Status = r.Status,
        HasActiveSession = !string.IsNullOrEmpty(r.SessionToken),
        Warning = warning,
        CreatedAt = r.CreatedAt,
    };
}