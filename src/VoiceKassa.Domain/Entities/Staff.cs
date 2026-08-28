using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using VoiceKassa.Domain.Enums;

namespace VoiceKassa.Domain.Entities;

/// <summary>
/// Eski "Cashier" o'rniga - kassir, ofitsiant, oshpaz, menejer va h.k.
/// Hammasi shu bitta model, "Role" orqali farqlanadi.
/// Yoshi, oyligi, ishga kirgan/boshatilgan sanasi va maosh tarixi
/// (SalaryHistory) ham shu yerda.
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

    [Column("FIRST_NAME")]
    [MaxLength(200)]
    public string? FirstName { get; set; }

    [Column("LAST_NAME")]
    [MaxLength(200)]
    public string? LastName { get; set; }

    /// <summary>
    /// "Ism + Familiya" birikmasi — imkon yaratilganda matnli ko'rinish.
    /// </summary>
    [Column("FULL_NAME")]
    [MaxLength(200)]
    public string? FullName { get; set; }

    [Column("PHONE_NUMBER")]
    [MaxLength(30)]
    public string? PhoneNumber { get; set; }

    [Column("ROLE")]
    public StaffRole Role { get; set; } = StaffRole.Cashier;

    [Column("AGE")]
    public int? Age { get; set; }

    [Column("MONTHLY_SALARY", TypeName = "numeric(18,2)")]
    public decimal MonthlySalary { get; set; }

    [Column("HIRE_DATE")]
    public DateTime? HireDate { get; set; }

    [Column("IS_ACTIVE")]
    public bool IsActive { get; set; } = true;

    [Column("FIRED_AT")]
    public DateTime? FiredAt { get; set; }

    [Column("CREATED_AT")]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    // ---- Navigation properties ----
    [ForeignKey(nameof(BusinessId))]
    public virtual Business? Business { get; set; }

    public virtual ICollection<SalaryHistory> SalaryHistory { get; set; } = new List<SalaryHistory>();
}
