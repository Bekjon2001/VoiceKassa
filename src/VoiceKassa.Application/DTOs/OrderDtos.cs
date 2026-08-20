using VoiceKassa.Domain.Enums;

namespace VoiceKassa.Application.DTOs;

// ---------- AI extraction (Gemini javobi) ----------

public class ExtractedItem
{
    public string Name { get; set; } = string.Empty;
    public decimal Quantity { get; set; }
    public string Unit { get; set; } = "dona";
    public decimal? Price { get; set; } // agar gapda alohida narx aytilgan bo'lsa
}

public class OrderExtractionResult
{
    public bool Success { get; set; }
    public string? ErrorMessage { get; set; }
    public List<ExtractedItem> Items { get; set; } = new();
    public decimal? Total { get; set; } // agar jami summa alohida aytilgan bo'lsa
    public string PaymentTypeRaw { get; set; } = "noaniq"; // "naqd" | "karta" | "onlayn" | "noaniq"
}

// ---------- Voice -> Order ----------

/// <summary>
/// TableId berilsa - restoran oqimi (shu stolning ochiq buyurtmasiga
/// qo'shiladi, yopilmagan holda qoladi). TableId null bo'lsa - do'kon
/// oqimi (to'g'ridan-to'g'ri yakunlangan sotuv sifatida yaratiladi).
/// </summary>
public class CreateOrderFromTextRequest
{
    public long BusinessId { get; set; }
    public long? TableId { get; set; }
    public long? StaffId { get; set; }
    public string TranscriptText { get; set; } = string.Empty;
}

public class CloseOrderRequest
{
    public PaymentType PaymentType { get; set; }
}

public class OrderItemResponse
{
    public long Id { get; set; }
    public long? ProductId { get; set; }
    public string ProductNameSpoken { get; set; } = string.Empty;
    public decimal Quantity { get; set; }
    public string Unit { get; set; } = string.Empty;
    public decimal LineTotal { get; set; }
}

public class OrderResponse
{
    public long Id { get; set; }
    public long BusinessId { get; set; }
    public long? TableId { get; set; }
    public long? StaffId { get; set; }
    public OrderStatus Status { get; set; }
    public decimal TotalAmount { get; set; }
    public PaymentType PaymentType { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? ClosedAt { get; set; }
    public List<OrderItemResponse> Items { get; set; } = new();
}
