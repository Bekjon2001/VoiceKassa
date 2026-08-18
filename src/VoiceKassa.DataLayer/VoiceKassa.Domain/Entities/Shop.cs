using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace VoiceKassa.Domain.Entities;

[Table("SHOPS", Schema = "voicekassa")]
public class Shop
{
    [Key]
    [Column("ID")]
    public Guid Id { get; set; } = Guid.NewGuid();

    [Column("NAME")]
    [MaxLength(200)]
    public string Name { get; set; } = string.Empty;

    [Column("ADDRESS")]
    [MaxLength(300)]
    public string? Address { get; set; }

    [Column("CREATED_AT")]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    // ---- Navigation properties ----
    public virtual ICollection<Cashier> Cashiers { get; set; } = new List<Cashier>();
    public virtual ICollection<Product> Products { get; set; } = new List<Product>();
    public virtual ICollection<Sale> Sales { get; set; } = new List<Sale>();
}
