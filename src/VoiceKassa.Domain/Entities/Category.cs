using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace VoiceKassa.Domain.Entities;

/// <summary>
/// Menu bo'limi (restoran: "Salatlar", "Ichimliklar") yoki mahsulot
/// guruhi (do'kon: "Non mahsulotlari", "Sut mahsulotlari"). Ixtiyoriy -
/// Product.CategoryId null bo'lishi mumkin.
/// </summary>
[Table("CATEGORIES", Schema = "voicekassa")]
public class Category
{
    [Key]
    [Column("ID")]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public long Id { get; set; }

    [Column("BUSINESS_ID")]
    public long BusinessId { get; set; }

    [Column("NAME")]
    [MaxLength(150)]
    public string Name { get; set; } = string.Empty;

    [Column("SORT_ORDER")]
    public int SortOrder { get; set; } = 0;

    // ---- Navigation properties ----
    [ForeignKey(nameof(BusinessId))]
    public virtual Business? Business { get; set; }

    public virtual ICollection<Product> Products { get; set; } = new List<Product>();
}
