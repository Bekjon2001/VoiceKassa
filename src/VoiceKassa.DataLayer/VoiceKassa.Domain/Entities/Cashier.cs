using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace VoiceKassa.Domain.Entities;

[Table("CASHIERS", Schema = "voicekassa")]
public class Cashier
{
    [Key]
    [Column("ID")]
    public Guid Id { get; set; } = Guid.NewGuid();

    [Column("SHOP_ID")]
    public Guid ShopId { get; set; }

    [Column("FULL_NAME")]
    [MaxLength(200)]
    public string FullName { get; set; } = string.Empty;

    [Column("PHONE_NUMBER")]
    [MaxLength(30)]
    public string? PhoneNumber { get; set; }

    [Column("IS_ACTIVE")]
    public bool IsActive { get; set; } = true;

    [Column("CREATED_AT")]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    // ---- Navigation properties (FK'lar uchun) ----
    [ForeignKey(nameof(ShopId))]
    public virtual Shop? Shop { get; set; }
}
