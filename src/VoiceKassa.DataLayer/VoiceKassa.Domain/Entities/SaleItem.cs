using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace VoiceKassa.Domain.Entities;

[Table("SALE_ITEMS", Schema = "voicekassa")]
public class SaleItem
{
    [Key]
    [Column("ID")]
    public Guid Id { get; set; } = Guid.NewGuid();

    [Column("SALE_ID")]
    public Guid SaleId { get; set; }

    // Nullable: AI may extract a product name that doesn't match any
    // known Product yet (new/unlisted item spoken by the cashier).
    [Column("PRODUCT_ID")]
    public Guid? ProductId { get; set; }

    [Column("PRODUCT_NAME_SPOKEN")]
    [MaxLength(200)]
    public string ProductNameSpoken { get; set; } = string.Empty;

    [Column("QUANTITY", TypeName = "numeric(18,3)")]
    public decimal Quantity { get; set; }

    [Column("UNIT")]
    [MaxLength(20)]
    public string Unit { get; set; } = "dona";

    [Column("LINE_TOTAL", TypeName = "numeric(18,2)")]
    public decimal LineTotal { get; set; }

    // ---- Navigation properties (FK'lar uchun) ----
    [ForeignKey(nameof(SaleId))]
    public virtual Sale? Sale { get; set; }

    [ForeignKey(nameof(ProductId))]
    public virtual Product? Product { get; set; }
}
