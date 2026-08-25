namespace VoiceKassa.Application.DTOs;

public class AskQuestionRequest
{
<<<<<<< HEAD
    public Guid ShopId { get; set; }
    public string Question { get; set; } = string.Empty;

    // Optional explicit range; if omitted, defaults to "today" in the
    // handler (kept here so the API can support "shu hafta", "avgust" etc.)
    public DateTime? FromDate { get; set; }
    public DateTime? ToDate { get; set; }
=======
    public long BusinessId { get; set; }
    public string Question { get; set; } = string.Empty;
>>>>>>> main
}

public class AskQuestionResponse
{
    public string Answer { get; set; } = string.Empty;
}

<<<<<<< HEAD
public class DailySummaryResponse
{
=======
public class TopProductDto
{
    public string Name { get; set; } = string.Empty;
    public decimal TotalQuantity { get; set; }
    public decimal TotalRevenue { get; set; }
}

public class DailySummaryResponse
{
    public DateTime FromUtc { get; set; }
    public DateTime ToUtc { get; set; }
>>>>>>> main
    public decimal TotalAmount { get; set; }
    public decimal CashAmount { get; set; }
    public decimal CardAmount { get; set; }
    public decimal OnlineAmount { get; set; }
<<<<<<< HEAD
    public int SaleCount { get; set; }
    public List<TopProductDto> TopProducts { get; set; } = new();
}

public class TopProductDto
{
    public string ProductName { get; set; } = string.Empty;
    public decimal QuantitySold { get; set; }
    public decimal Revenue { get; set; }
}
=======
    public int OrderCount { get; set; }
    public List<TopProductDto> TopProducts { get; set; } = new();
}
>>>>>>> main
