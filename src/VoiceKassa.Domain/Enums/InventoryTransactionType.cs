namespace VoiceKassa.Domain.Enums;

/// <summary>Kirim/chiqim - ombordagi mahsulot harakati.</summary>
public enum InventoryTransactionType
{
    In = 0,   // Kirim (tovar keldi)
    Out = 1,  // Chiqim (sotildi, buzildi, hisobdan chiqarildi)
}
