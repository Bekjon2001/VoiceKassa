namespace VoiceKassa.Domain.Enums;

/// <summary>
/// Bron holati: Waiting (kutilmoqda) -> Arrived (keldi, sessiya ochildi)
/// -> Yopilgan (hisob yopilganda, token o'chiriladi) yoki Bekor.
/// </summary>
public enum ReservationStatus
{
    Waiting = 0,
    Arrived = 1,
    Closed = 2,
    Cancelled = 3,
}