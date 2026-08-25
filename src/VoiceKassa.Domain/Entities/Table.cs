using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using VoiceKassa.Domain.Enums;

namespace VoiceKassa.Domain.Entities;

/// <summary>
/// Faqat restoran/kafe uchun dolzarb (Business.Type == Restaurant). Do'kon
/// turidagi biznesda bu jadval shunchaki bo'sh qoladi - alohida schema
/// yaratilmaydi, chunki bitta umumiy model prinsipiga zid bo'lardi.
/// </summary>
[Table("TABLES", Schema = "voicekassa")]
public class Table
{
    [Key]
    [Column("ID")]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public long Id { get; set; }

    [Column("BUSINESS_ID")]
    public long BusinessId { get; set; }

    [Column("NAME")]
    [MaxLength(50)]
    public string Name { get; set; } = string.Empty; // masalan "1-stol", "VIP-2"

    [Column("CAPACITY")]
    public int Capacity { get; set; } = 4;

    [Column("STATUS")]
    public TableStatus Status { get; set; } = TableStatus.Free;

    // ---- Navigation properties ----
    [ForeignKey(nameof(BusinessId))]
    public virtual Business? Business { get; set; }

    public virtual ICollection<Order> Orders { get; set; } = new List<Order>();
}
