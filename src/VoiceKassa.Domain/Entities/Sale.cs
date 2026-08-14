using VoiceKassa.Domain.Enums;

namespace VoiceKassa.Domain.Entities;

public class Sale
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid ShopId { get; set; }
    public Shop? Shop { get; set; }

    public Guid? CashierId { get; set; }
    public Cashier? Cashier { get; set; }

    public decimal TotalAmount { get; set; }
    public PaymentType PaymentType { get; set; } = PaymentType.Unknown;

    // What the cashier actually said - kept for audit / re-processing
    // if the AI extraction needs correction later.
    public string TranscriptText { get; set; } = string.Empty;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public ICollection<SaleItem> Items { get; set; } = new List<SaleItem>();
}
