namespace VoiceKassa.Domain.Enums;

/// <summary>
/// Restoranda: Open (stol band, buyurtma yig'ilyapti) -> InProgress (oshxonaga
/// yuborilgan) -> Completed (to'langan, yopilgan). Do'konda odatda to'g'ridan-to'g'ri
/// Open -> Completed (bitta ovozli gap bilan).
/// </summary>
public enum OrderStatus
{
    Open = 0,
    InProgress = 1,
    Completed = 2,
    Cancelled = 3,
}
