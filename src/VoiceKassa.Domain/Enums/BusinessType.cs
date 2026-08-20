namespace VoiceKassa.Domain.Enums;

/// <summary>
/// Biznes turi. Barcha turlar bitta umumiy Business modeliga tayanadi -
/// alohida RestaurantSystem/MarketSystem loyihalar YO'Q. Yangi turlar
/// keyinchalik shu enum'ga qo'shiladi (masalan Pharmacy, Cafe).
/// </summary>
public enum BusinessType
{
    Restaurant = 0,
    Market = 1,
    Shop = 2,
    Warehouse = 3,
}
