using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using VoiceKassa.Domain.Enums;

namespace VoiceKassa.Domain.Entities;

/// <summary>
/// Kirim/chiqim - do'kon/market uchun ombor harakati tarixi ("bugun qancha
/// tovar keldi", "necha dona buzilib chiqarildi"). Restoran uchun odatda
/// ishlatilmaydi (menu taomlari StockQuantity kuzatilmaydi), lekin agar
/// kelajakda oshxona xomashyosi kuzatilsa, shu yerga ulanadi.
/// </summary>
[Table("INVENTORY_TRANSACTIONS", Schema = "voicekassa")]
public class InventoryTransaction
{
    [Key]
    [Column("ID")]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public long Id { get; set; }

    [Column("BUSINESS_ID")]
    public long BusinessId { get; set; }

    [Column("PRODUCT_ID")]
    public long ProductId { get; set; }

    [Column("TYPE")]
    public InventoryTransactionType Type { get; set; }

    [Column("QUANTITY", TypeName = "numeric(18,3)")]
    public decimal Quantity { get; set; }

    [Column("REASON")]
    [MaxLength(300)]
    public string? Reason { get; set; } // "Yetkazib beruvchidan", "Muddati o'tgan", ...

    [Column("CREATED_AT")]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    // ---- Navigation properties ----
    [ForeignKey(nameof(BusinessId))]
    public virtual Business? Business { get; set; }

    [ForeignKey(nameof(ProductId))]
    public virtual Product? Product { get; set; }
}
