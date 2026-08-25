using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace VoiceKassa.Domain.Entities;

<<<<<<< HEAD
=======
/// <summary>
/// Universal: restoranda bu "menu taomi" (StockQuantity odatda kuzatilmaydi,
/// null qoladi), do'konda "sotiladigan tovar" (StockQuantity kuzatiladi).
/// Ikkalasi ham bitta jadval - biznes turi kelib chiqishi Business.Type orqali
/// aniqlanadi, alohida "MenuItem" jadval qilinmadi (ortiqcha murakkablik).
/// </summary>
>>>>>>> main
[Table("PRODUCTS", Schema = "voicekassa")]
public class Product
{
    [Key]
    [Column("ID")]
<<<<<<< HEAD
    public Guid Id { get; set; } = Guid.NewGuid();

    [Column("SHOP_ID")]
    public Guid ShopId { get; set; }
=======
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public long Id { get; set; }

    [Column("BUSINESS_ID")]
    public long BusinessId { get; set; }

    [Column("CATEGORY_ID")]
    public long? CategoryId { get; set; }
>>>>>>> main

    [Column("NAME")]
    [MaxLength(200)]
    public string Name { get; set; } = string.Empty;

<<<<<<< HEAD
    // A product may be known by several spoken aliases/synonyms so the
    // AI extraction step can match "pomidor" vs "tomat" to the same row.
    // Ombor (Infrastructure) qatlamida vergul bilan ajratilgan matn
    // sifatida saqlanadi (AppDbContext'dagi HasConversion'ga qarang).
=======
    // Ovozli tanib olish uchun bir nechta nom bilan ataladigan mahsulotlar
    // ("pomidor" = "tomat"). Vergul bilan ajratilgan matn sifatida saqlanadi.
>>>>>>> main
    [Column("ALIASES")]
    public List<string> Aliases { get; set; } = new();

    [Column("UNIT")]
    [MaxLength(20)]
<<<<<<< HEAD
    public string Unit { get; set; } = "dona"; // dona, kg, litr...

    [Column("DEFAULT_PRICE", TypeName = "numeric(18,2)")]
    public decimal? DefaultPrice { get; set; }

    [Column("STOCK_QUANTITY", TypeName = "numeric(18,3)")]
    public decimal StockQuantity { get; set; }
=======
    public string Unit { get; set; } = "dona"; // dona, kg, litr, porsiya...

    [Column("PRICE", TypeName = "numeric(18,2)")]
    public decimal Price { get; set; }

    // Restoran menu taomlari uchun odatda null (ombor kuzatilmaydi).
    // Do'kon/market mahsulotlari uchun haqiqiy qoldiq.
    [Column("STOCK_QUANTITY", TypeName = "numeric(18,3)")]
    public decimal? StockQuantity { get; set; }
>>>>>>> main

    [Column("LOW_STOCK_THRESHOLD", TypeName = "numeric(18,3)")]
    public decimal LowStockThreshold { get; set; } = 0;

<<<<<<< HEAD
=======
    [Column("IS_AVAILABLE")]
    public bool IsAvailable { get; set; } = true;

>>>>>>> main
    [Column("CREATED_AT")]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    [Column("UPDATED_AT")]
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

<<<<<<< HEAD
    // ---- Navigation properties (FK'lar uchun) ----
    [ForeignKey(nameof(ShopId))]
    public virtual Shop? Shop { get; set; }
=======
    // ---- Navigation properties ----
    [ForeignKey(nameof(BusinessId))]
    public virtual Business? Business { get; set; }

    [ForeignKey(nameof(CategoryId))]
    public virtual Category? Category { get; set; }
>>>>>>> main
}
