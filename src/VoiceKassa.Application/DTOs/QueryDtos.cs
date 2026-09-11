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

public class JarvisCommandRequest
{
    public string Text { get; set; } = string.Empty;
}

/// <summary>
/// Odiy Admin (biznes egasi) Jarvis buyrug'i: token orqali biznes tekshiriladi,
/// AI faqat shu biznes konteksti bilan javob beradi.
/// </summary>
public class OwnerJarvisCommandRequest
{
    public long BusinessId { get; set; }
    public string Text { get; set; } = string.Empty;
}

/// <summary>
/// Jarvis (ovozli yordamchi) tahlili natijasi: Kind == "action" bo'lsa
/// Action/BusinessId/View to'ldiriladi (frontend bajaradi), Kind == "answer"
/// bo'lsa Answer to'ldiriladi (AI matn javobi).
/// </summary>
public class JarvisCommandResponse
{
    /// <summary>"action" yoki "answer"</summary>
    public string Kind { get; set; } = "answer";

    /// <summary>navigate | select_business | activate_business | deactivate_business</summary>
    public string? Action { get; set; }

    /// <summary>navigate uchun bo'lim: restaurants | markets | create | market-create | system | ai</summary>
    public string? View { get; set; }

    /// <summary>Biznesga tegishli amallar uchun (select/activate/deactivate)</summary>
    public long? BusinessId { get; set; }

    /// <summary>Kind == "answer" bo'lsa — AI javobi</summary>
    public string Answer { get; set; } = string.Empty;
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
