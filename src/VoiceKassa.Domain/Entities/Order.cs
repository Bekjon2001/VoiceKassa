using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using VoiceKassa.Domain.Enums;

namespace VoiceKassa.Domain.Entities;

/// <summary>
/// Eski "Sale" o'rniga - universal buyurtma/chek. Restoranda TableId
/// to'ldiriladi va Status Open -> InProgress -> Completed bosqichlaridan
/// o'tadi. Do'konda TableId null qoladi, odatda to'g'ridan-to'g'ri
/// Completed holatida yaratiladi (bitta ovozli gap = tugallangan sotuv).
/// </summary>
[Table("ORDERS", Schema = "voicekassa")]
public class Order
{
    [Key]
    [Column("ID")]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public long Id { get; set; }

    [Column("BUSINESS_ID")]
    public long BusinessId { get; set; }

    [Column("TABLE_ID")]
    public long? TableId { get; set; }

    [Column("STAFF_ID")]
    public long? StaffId { get; set; }

    [Column("STATUS")]
    public OrderStatus Status { get; set; } = OrderStatus.Open;

    [Column("TOTAL_AMOUNT", TypeName = "numeric(18,2)")]
    public decimal TotalAmount { get; set; }

    [Column("PAYMENT_TYPE")]
    public PaymentType PaymentType { get; set; } = PaymentType.Unknown;

    // Kassir/ofitsiant aytgan asl gap - keyinchalik tekshirish/tuzatish uchun.
    [Column("TRANSCRIPT_TEXT")]
    [MaxLength(1000)]
    public string TranscriptText { get; set; } = string.Empty;

    [Column("CREATED_AT")]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    [Column("CLOSED_AT")]
    public DateTime? ClosedAt { get; set; }

    // ---- Navigation properties ----
    [ForeignKey(nameof(BusinessId))]
    public virtual Business? Business { get; set; }

    [ForeignKey(nameof(TableId))]
    public virtual Table? Table { get; set; }

    [ForeignKey(nameof(StaffId))]
    public virtual Staff? Staff { get; set; }

    public virtual ICollection<OrderItem> Items { get; set; } = new List<OrderItem>();
}
