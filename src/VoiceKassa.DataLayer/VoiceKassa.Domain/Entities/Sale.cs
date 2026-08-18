using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using VoiceKassa.Domain.Enums;

namespace VoiceKassa.Domain.Entities;

[Table("SALES", Schema = "voicekassa")]
public class Sale
{
    [Key]
    [Column("ID")]
    public Guid Id { get; set; } = Guid.NewGuid();

    [Column("SHOP_ID")]
    public Guid ShopId { get; set; }

    [Column("CASHIER_ID")]
    public Guid? CashierId { get; set; }

    [Column("TOTAL_AMOUNT", TypeName = "numeric(18,2)")]
    public decimal TotalAmount { get; set; }

    [Column("PAYMENT_TYPE")]
    public PaymentType PaymentType { get; set; } = PaymentType.Unknown;

    // What the cashier actually said - kept for audit / re-processing
    // if the AI extraction needs correction later.
    [Column("TRANSCRIPT_TEXT")]
    [MaxLength(1000)]
    public string TranscriptText { get; set; } = string.Empty;

    [Column("CREATED_AT")]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    // ---- Navigation properties (FK'lar uchun) ----
    [ForeignKey(nameof(ShopId))]
    public virtual Shop? Shop { get; set; }

    [ForeignKey(nameof(CashierId))]
    public virtual Cashier? Cashier { get; set; }

    public virtual ICollection<SaleItem> Items { get; set; } = new List<SaleItem>();
}
