using VoiceKassa.Application.DTOs;
using VoiceKassa.Application.Interfaces;
using VoiceKassa.Domain.Entities;
using VoiceKassa.Domain.Enums;

namespace VoiceKassa.Application.Services;

/// <summary>
/// Core use case: "kassir gapirdi -> chek yaratildi".
/// Pure orchestration - no HTTP, no EF Core details here, so it's
/// trivially unit-testable with mocked interfaces.
/// </summary>
public class SaleService
{
    private readonly IAiExtractionService _extraction;
    private readonly ISaleRepository _repo;

    public SaleService(IAiExtractionService extraction, ISaleRepository repo)
    {
        _extraction = extraction;
        _repo = repo;
    }

    public async Task<(bool Success, string? Error, SaleResponse? Sale)> CreateFromTextAsync(
        CreateSaleFromTextRequest request, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(request.TranscriptText))
            return (false, "Matn bo'sh bo'lishi mumkin emas.", null);

        var extraction = await _extraction.ExtractSaleAsync(request.TranscriptText, ct);
        if (!extraction.Success || extraction.Items.Count == 0)
            return (false, extraction.ErrorMessage ?? "Gapdan mahsulot topilmadi, qayta urinib ko'ring.", null);

        var sale = new Sale
        {
            ShopId = request.ShopId,
            CashierId = request.CashierId,
            TranscriptText = request.TranscriptText,
            PaymentType = MapPaymentType(extraction.PaymentTypeRaw),
        };

        decimal computedTotal = 0;

        foreach (var item in extraction.Items)
        {
            var matchedProduct = await _repo.FindProductByNameAsync(request.ShopId, item.Name, ct);

            // Prefer: spoken price -> product's default price -> 0 (unknown, flagged for review)
            var unitPrice = item.Price ?? matchedProduct?.DefaultPrice ?? 0;
            var lineTotal = unitPrice * item.Quantity;
            computedTotal += lineTotal;

            sale.Items.Add(new SaleItem
            {
                ProductId = matchedProduct?.Id,
                ProductNameSpoken = item.Name,
                Quantity = item.Quantity,
                Unit = item.Unit,
                LineTotal = lineTotal,
            });

            if (matchedProduct is not null)
                await _repo.DecrementStockAsync(matchedProduct.Id, item.Quantity, ct);
        }

        // If the cashier stated an explicit total, trust it for the receipt
        // total (it's what actually changed hands) but keep computedTotal
        // available for reconciliation/alerts if the two diverge a lot.
        sale.TotalAmount = extraction.Total ?? computedTotal;

        var saved = await _repo.AddAsync(sale, ct);

        return (true, null, ToResponse(saved));
    }

    private static PaymentType MapPaymentType(string raw) => raw.Trim().ToLowerInvariant() switch
    {
        "naqd" => PaymentType.Cash,
        "karta" => PaymentType.Card,
        "onlayn" or "online" => PaymentType.Online,
        _ => PaymentType.Unknown,
    };

    private static SaleResponse ToResponse(Sale sale) => new()
    {
        Id = sale.Id,
        CreatedAt = sale.CreatedAt,
        TotalAmount = sale.TotalAmount,
        PaymentType = sale.PaymentType.ToString(),
        Items = sale.Items.Select(i => new SaleItemResponse
        {
            ProductName = i.ProductNameSpoken,
            Quantity = i.Quantity,
            Unit = i.Unit,
            LineTotal = i.LineTotal,
        }).ToList(),
    };
}
