namespace VoiceKassa.Application.DTOs;

/// <summary>
/// Structured result the AI extraction service returns after parsing a
/// cashier's spoken (or typed) sentence, e.g.
/// "non 2 ta 6 ming, sut 1 litr 12 ming, jami 18 ming naqd".
/// </summary>
public class SaleExtractionResult
{
    public bool Success { get; set; }
    public string? ErrorMessage { get; set; }

    public List<ExtractedItem> Items { get; set; } = new();
    public decimal? Total { get; set; }
    public string PaymentTypeRaw { get; set; } = "noaniq"; // naqd | karta | onlayn | noaniq
}

public class ExtractedItem
{
    public string Name { get; set; } = string.Empty;
    public decimal Quantity { get; set; }
    public string Unit { get; set; } = "dona";
    public decimal? Price { get; set; }
}

/// <summary>Request body for POST /api/sales/voice</summary>
public class CreateSaleFromTextRequest
{
    public Guid ShopId { get; set; }
    public Guid? CashierId { get; set; }
    public string TranscriptText { get; set; } = string.Empty;
}

public class SaleResponse
{
    public Guid Id { get; set; }
    public DateTime CreatedAt { get; set; }
    public decimal TotalAmount { get; set; }
    public string PaymentType { get; set; } = string.Empty;
    public List<SaleItemResponse> Items { get; set; } = new();
}

public class SaleItemResponse
{
    public string ProductName { get; set; } = string.Empty;
    public decimal Quantity { get; set; }
    public string Unit { get; set; } = string.Empty;
    public decimal LineTotal { get; set; }
}
