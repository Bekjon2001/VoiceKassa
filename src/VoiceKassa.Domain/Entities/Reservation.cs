using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using VoiceKassa.Domain.Enums;

namespace VoiceKassa.Domain.Entities;

/// <summary>
/// Stol bron qilish yozuvi. Mijoz 6 raqamli CODE bilan "kiradi" (enter),
/// server unga SESSION_TOKEN beradi — menyudan keyingi barcha so'rovlar
/// shu token bilan faqat o'z stoliga bog'lanadi. START_TIME/END_TIME null
/// bo'lsa — bron butun kun uchun.
/// STATUS: 0=Kutilmoqda, 1=Kelgan, 2=Yopilgan (hisob yopilganda), 3=Bekor.
/// </summary>
[Table("RESERVATIONS", Schema = "voicekassa")]
public class Reservation
{
    [Key]
    [Column("ID")]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public long Id { get; set; }

    [Column("BUSINESS_ID")]
    public long BusinessId { get; set; }

    [Column("TABLE_ID")]
    public long TableId { get; set; }

    /// <summary>Bron sanasi (kun boshiga normalizalanadi).</summary>
    [Column("BOOKING_DATE", TypeName = "date")]
    public DateTime BookingDate { get; set; }

    /// <summary>Boshlanish vaqti (ixtiyoriy; null = kun boshidan).</summary>
    [Column("START_TIME")]
    public TimeSpan? StartTime { get; set; }

    /// <summary>Tugash vaqti (ixtiyoriy; null = kun oxirigacha).</summary>
    [Column("END_TIME")]
    public TimeSpan? EndTime { get; set; }

    [Column("CUSTOMER_NAME")]
    [MaxLength(200)]
    public string? CustomerName { get; set; }

    [Column("CUSTOMER_PHONE")]
    [MaxLength(30)]
    public string? CustomerPhone { get; set; }

    [Column("GUEST_COUNT")]
    public int GuestCount { get; set; }

    /// <summary>6 raqamli mijoz kodi (yagona).</summary>
    [Column("CODE")]
    [MaxLength(6)]
    public string Code { get; set; } = string.Empty;

    /// <summary>Kod bilan kirishda beriladigan sessiya kaliti (yagona, ixtiyoriy).</summary>
    [Column("SESSION_TOKEN")]
    [MaxLength(200)]
    public string? SessionToken { get; set; }

    [Column("STATUS")]
    public ReservationStatus Status { get; set; } = ReservationStatus.Waiting;

    [Column("CREATED_AT")]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    // ---- Navigation properties ----
    [ForeignKey(nameof(BusinessId))]
    public virtual Business? Business { get; set; }

    [ForeignKey(nameof(TableId))]
    public virtual Table? Table { get; set; }
}