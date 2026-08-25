using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace VoiceKassa.Domain.Entities;

[Table("USER_ACCOUNTS", Schema = "voicekassa")]
public class UserAccount
{
    [Key]
    [Column("ID")]
    public long Id { get; set; }

    [Column("BUSINESS_ID")]
    public long? BusinessId { get; set; }

    [Column("FULL_NAME")]
    [MaxLength(200)]
    public string FullName { get; set; } = string.Empty;

    [Column("PHONE_NUMBER")]
    [MaxLength(30)]
    public string? PhoneNumber { get; set; }

    [Column("LOGIN")]
    [MaxLength(100)]
    public string Login { get; set; } = string.Empty;

    [Column("PASSWORD_HASH")]
    public string PasswordHash { get; set; } = string.Empty;

    [Column("IS_ACTIVE")]
    public bool IsActive { get; set; } = true;

    [Column("IS_SUPER_ADMIN")]
    public bool IsSuperAdmin { get; set; }

    [Column("ACCESS_TOKEN")]
    [MaxLength(200)]
    public string AccessToken { get; set; } = string.Empty;

    [Column("CREATED_AT")]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
