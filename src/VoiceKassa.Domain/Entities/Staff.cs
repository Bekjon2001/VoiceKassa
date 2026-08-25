using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using VoiceKassa.Domain.Enums;

namespace VoiceKassa.Domain.Entities;

/// <summary>
/// Eski "Cashier" o'rniga - kassir, ofitsiant, oshpaz, menejer va h.k.
/// hammasi shu bitta model, "Role" orqali farqlanadi.
/// </summary>
[Table("STAFF", Schema = "voicekassa")]
public class Staff
{
    [Key]
    [Column("ID")]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public long Id { get; set; }

    [Column("BUSINESS_ID")]
    public long BusinessId { get; set; }

    [Column("FULL_NAME")]
    [MaxLength(200)]
    public string FullName { get; set; } = string.Empty;

    [Column("PHONE_NUMBER")]
    [MaxLength(30)]
    public string? PhoneNumber { get; set; }

    [Column("ROLE")]
    public StaffRole Role { get; set; } = StaffRole.Cashier;

    [Column("IS_ACTIVE")]
    public bool IsActive { get; set; } = true;

    [Column("CREATED_AT")]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    // ---- Navigation properties ----
    [ForeignKey(nameof(BusinessId))]
    public virtual Business? Business { get; set; }
}
