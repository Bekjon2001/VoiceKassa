namespace VoiceKassa.Domain.Entities;

public class SaleItem
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid SaleId { get; set; }
    public Sale? Sale { get; set; }

    // Nullable: AI may extract a product name that doesn't match any
    // known Product yet (new/unlisted item spoken by the cashier).
    public Guid? ProductId { get; set; }
    public Product? Product { get; set; }

    public string ProductNameSpoken { get; set; } = string.Empty;
    public decimal Quantity { get; set; }
    public string Unit { get; set; } = "dona";
    public decimal LineTotal { get; set; }
}
