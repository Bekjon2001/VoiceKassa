using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace VoiceKassa.Domain.Entities;

/// <summary>
/// Universal: restoranda bu "menu taomi" (StockQuantity odatda kuzatilmaydi,
/// null qoladi), do'konda "sotiladigan tovar" (StockQuantity kuzatiladi).
/// Ikkalasi ham bitta jadval - biznes turi kelib chiqishi Business.Type orqali
/// aniqlanadi, alohida "MenuItem" jadval qilinmadi (ortiqcha murakkablik).
/// </summary>
[Table("PRODUCTS", Schema = "voicekassa")]
public class Product
{
    [Key]
    [Column("ID")]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public long Id { get; set; }

    [Column("BUSINESS_ID")]
    public long BusinessId { get; set; }

    [Column("CATEGORY_ID")]
    public long? CategoryId { get; set; }

    [Column("NAME")]
    [MaxLength(200)]
    public string Name { get; set; } = string.Empty;

    // Ovozli tanib olish uchun bir nechta nom bilan ataladigan mahsulotlar
    // ("pomidor" = "tomat"). Vergul bilan ajratilgan matn sifatida saqlanadi.
    [Column("ALIASES")]
    public List<string> Aliases { get; set; } = new();

    [Column("UNIT")]
    [MaxLength(20)]
    public string Unit { get; set; } = "dona"; // dona, kg, litr, porsiya...

    [Column("PRICE", TypeName = "numeric(18,2)")]
    public decimal Price { get; set; }

    // Restoran menu taomlari uchun odatda null (ombor kuzatilmaydi).
    // Do'kon/market mahsulotlari uchun haqiqiy qoldiq.
    [Column("STOCK_QUANTITY", TypeName = "numeric(18,3)")]
    public decimal? StockQuantity { get; set; }

    [Column("LOW_STOCK_THRESHOLD", TypeName = "numeric(18,3)")]
    public decimal LowStockThreshold { get; set; } = 0;

    [Column("IS_AVAILABLE")]
    public bool IsAvailable { get; set; } = true;

    [Column("CREATED_AT")]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    [Column("UPDATED_AT")]
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    // ---- Navigation properties ----
    [ForeignKey(nameof(BusinessId))]
    public virtual Business? Business { get; set; }

    [ForeignKey(nameof(CategoryId))]
    public virtual Category? Category { get; set; }
}
