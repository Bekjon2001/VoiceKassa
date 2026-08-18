using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace VoiceKassa.Domain.Entities;

[Table("PRODUCTS", Schema = "voicekassa")]
public class Product
{
    [Key]
    [Column("ID")]
    public Guid Id { get; set; } = Guid.NewGuid();

    [Column("SHOP_ID")]
    public Guid ShopId { get; set; }

    [Column("NAME")]
    [MaxLength(200)]
    public string Name { get; set; } = string.Empty;

    // A product may be known by several spoken aliases/synonyms so the
    // AI extraction step can match "pomidor" vs "tomat" to the same row.
    // Ombor (Infrastructure) qatlamida vergul bilan ajratilgan matn
    // sifatida saqlanadi (AppDbContext'dagi HasConversion'ga qarang).
    [Column("ALIASES")]
    public List<string> Aliases { get; set; } = new();

    [Column("UNIT")]
    [MaxLength(20)]
    public string Unit { get; set; } = "dona"; // dona, kg, litr...

    [Column("DEFAULT_PRICE", TypeName = "numeric(18,2)")]
    public decimal? DefaultPrice { get; set; }

    [Column("STOCK_QUANTITY", TypeName = "numeric(18,3)")]
    public decimal StockQuantity { get; set; }

    [Column("LOW_STOCK_THRESHOLD", TypeName = "numeric(18,3)")]
    public decimal LowStockThreshold { get; set; } = 0;

    [Column("CREATED_AT")]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    [Column("UPDATED_AT")]
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    // ---- Navigation properties (FK'lar uchun) ----
    [ForeignKey(nameof(ShopId))]
    public virtual Shop? Shop { get; set; }
}
