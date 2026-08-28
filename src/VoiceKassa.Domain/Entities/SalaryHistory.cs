using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace VoiceKassa.Domain.Entities;

/// <summary>
/// Xodim oyligining o'zgarish tarixi. Har bir maosh o'zgarishi (ko'tarilish /
/// pasayish) uchun bitta yozuv saqlanadi: qachon, eski qiymat, yangi qiymat va izoh.
/// </summary>
[Table("STAFF_SALARY_HISTORY", Schema = "voicekassa")]
public class SalaryHistory
{
    [Key]
    [Column("ID")]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public long Id { get; set; }

    [Column("STAFF_ID")]
    public long StaffId { get; set; }

    [Column("CHANGED_AT")]
    public DateTime ChangedAt { get; set; } = DateTime.UtcNow;

    [Column("OLD_SALARY", TypeName = "numeric(18,2)")]
    public decimal OldSalary { get; set; }

    [Column("NEW_SALARY", TypeName = "numeric(18,2)")]
    public decimal NewSalary { get; set; }

    [Column("REASON")]
    [MaxLength(300)]
    public string? Reason { get; set; }

    [ForeignKey(nameof(StaffId))]
    public virtual Staff? Staff { get; set; }
}