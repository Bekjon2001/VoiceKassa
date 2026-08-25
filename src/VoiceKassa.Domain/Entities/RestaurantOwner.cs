using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace VoiceKassa.Domain.Entities;

[Table("RESTAURANT_OWNERS", Schema = "voicekassa")]
public class RestaurantOwner
{
    [Key]
    [Column("ID")]
    public long Id { get; set; }

    [Column("BUSINESS_ID")]
    public long BusinessId { get; set; }

    [Column("FULL_NAME")]
    [MaxLength(200)]
    public string FullName { get; set; } = string.Empty;

    [Column("PHONE_NUMBER")]
    [MaxLength(30)]
    public string PhoneNumber { get; set; } = string.Empty;

    [Column("LOGIN")]
    [MaxLength(100)]
    public string Login { get; set; } = string.Empty;

    [Column("PASSWORD_HASH")]
    public string PasswordHash { get; set; } = string.Empty;

    [Column("SUBSCRIPTION_AMOUNT", TypeName = "numeric(18,2)")]
    public decimal SubscriptionAmount { get; set; }

    [Column("PAYMENT_PAID_AT")]
    public DateTime PaymentPaidAt { get; set; }

    [Column("SUBSCRIPTION_MONTHS")]
    public int SubscriptionMonths { get; set; }

    [Column("SUBSCRIPTION_ENDS_AT")]
    public DateTime SubscriptionEndsAt { get; set; }

    [Column("ACCESS_TOKEN")]
    [MaxLength(200)]
    public string AccessToken { get; set; } = string.Empty;

    [Column("IS_ACTIVE")]
    public bool IsActive { get; set; } = true;

    [Column("CREATED_AT")]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
