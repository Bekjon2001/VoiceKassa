namespace VoiceKassa.Application.DTOs;

public class AskQuestionRequest
{
    public Guid ShopId { get; set; }
    public string Question { get; set; } = string.Empty;

    // Optional explicit range; if omitted, defaults to "today" in the
    // handler (kept here so the API can support "shu hafta", "avgust" etc.)
    public DateTime? FromDate { get; set; }
    public DateTime? ToDate { get; set; }
}

public class AskQuestionResponse
{
    public string Answer { get; set; } = string.Empty;
}

public class DailySummaryResponse
{
    public decimal TotalAmount { get; set; }
    public decimal CashAmount { get; set; }
    public decimal CardAmount { get; set; }
    public decimal OnlineAmount { get; set; }
    public int SaleCount { get; set; }
    public List<TopProductDto> TopProducts { get; set; } = new();
}

public class TopProductDto
{
    public string ProductName { get; set; } = string.Empty;
    public decimal QuantitySold { get; set; }
    public decimal Revenue { get; set; }
}
