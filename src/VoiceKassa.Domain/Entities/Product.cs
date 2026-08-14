namespace VoiceKassa.Domain.Entities;

public class Product
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid ShopId { get; set; }
    public Shop? Shop { get; set; }

    public string Name { get; set; } = string.Empty;

    // A product may be known by several spoken aliases/synonyms so the
    // AI extraction step can match "pomidor" vs "tomat" to the same row.
    public List<string> Aliases { get; set; } = new();

    public string Unit { get; set; } = "dona"; // dona, kg, litr...
    public decimal? DefaultPrice { get; set; }
    public decimal StockQuantity { get; set; }
    public decimal LowStockThreshold { get; set; } = 0;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
