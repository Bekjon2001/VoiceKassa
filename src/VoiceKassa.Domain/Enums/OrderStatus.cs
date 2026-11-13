namespace VoiceKassa.Domain.Enums;

/// <summary>
/// Restoranda: Open (stol band) -> InProgress (oshxonada) -> Ready (tayyor)
/// -> Completed (to'langan, yopilgan). Do'konda odatda Open -> Completed.
/// </summary>
public enum OrderStatus
{
    Open = 0,
    InProgress = 1,
    Completed = 2,
    Cancelled = 3,
    Ready = 4,
}
