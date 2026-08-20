using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using VoiceKassa.Domain.Enums;

namespace VoiceKassa.Domain.Entities;

/// <summary>
/// Tizimning eng yuqori (root) entity'si. Restoran, do'kon, supermarket,
/// ombor - hammasi shu bitta model orqali ifodalanadi, "Type" maydoni
/// orqali farqlanadi. Yangi biznes turi qo'shish uchun yangi loyiha EMAS,
/// faqat BusinessType enum'iga qo'shimcha va shu turga xos entity'lar kerak
/// (masalan Restaurant uchun Table, Market uchun kelajakda boshqa narsa).
/// </summary>
[Table("BUSINESSES", Schema = "voicekassa")]
public class Business
{
    [Key]
    [Column("ID")]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public long Id { get; set; }

    [Column("NAME")]
    [MaxLength(200)]
    public string Name { get; set; } = string.Empty;

    [Column("TYPE")]
    public BusinessType Type { get; set; }

    [Column("ADDRESS")]
    [MaxLength(300)]
    public string? Address { get; set; }

    [Column("PHONE_NUMBER")]
    [MaxLength(30)]
    public string? PhoneNumber { get; set; }

    [Column("IS_ACTIVE")]
    public bool IsActive { get; set; } = true;

    [Column("CREATED_AT")]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    // ---- Navigation properties ----
    public virtual ICollection<Staff> StaffMembers { get; set; } = new List<Staff>();
    public virtual ICollection<Category> Categories { get; set; } = new List<Category>();
    public virtual ICollection<Product> Products { get; set; } = new List<Product>();
    public virtual ICollection<Table> Tables { get; set; } = new List<Table>();
    public virtual ICollection<Order> Orders { get; set; } = new List<Order>();
    public virtual ICollection<InventoryTransaction> InventoryTransactions { get; set; } = new List<InventoryTransaction>();
}
