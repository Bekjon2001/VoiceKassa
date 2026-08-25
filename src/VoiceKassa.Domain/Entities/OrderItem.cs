using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace VoiceKassa.Domain.Entities;

/// <summary>Eski "SaleItem" o'rniga - buyurtmadagi har bir mahsulot qatori.</summary>
[Table("ORDER_ITEMS", Schema = "voicekassa")]
public class OrderItem
{
    [Key]
    [Column("ID")]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public long Id { get; set; }

    [Column("ORDER_ID")]
    public long OrderId { get; set; }

    // Nullable: AI ovozdan tanigan nom bazadagi hech qanday Product'ga
    // to'g'ri kelmasa ham, chekni saqlab qolish uchun.
    [Column("PRODUCT_ID")]
    public long? ProductId { get; set; }

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

    // ---- Navigation properties ----
    [ForeignKey(nameof(OrderId))]
    public virtual Order? Order { get; set; }

    [ForeignKey(nameof(ProductId))]
    public virtual Product? Product { get; set; }
}
