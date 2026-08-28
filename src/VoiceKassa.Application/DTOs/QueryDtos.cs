namespace VoiceKassa.Application.DTOs;

public class AskQuestionRequest
{
    public long BusinessId { get; set; }
    public string Question { get; set; } = string.Empty;
}

public class AskSuperAdminRequest
{
    public string Question { get; set; } = string.Empty;
}

public class AskQuestionResponse
{
    public string Answer { get; set; } = string.Empty;
}

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
    public decimal TotalAmount { get; set; }
    public decimal CashAmount { get; set; }
    public decimal CardAmount { get; set; }
    public decimal OnlineAmount { get; set; }
    public int OrderCount { get; set; }
    public List<TopProductDto> TopProducts { get; set; } = new();
}
