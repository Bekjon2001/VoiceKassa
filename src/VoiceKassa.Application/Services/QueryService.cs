using System.Text.Json;
using VoiceKassa.Application.DTOs;
using VoiceKassa.Application.Interfaces;
using VoiceKassa.Domain.Enums;

namespace VoiceKassa.Application.Services;

public class QueryService
{
    private readonly IOrderRepository _orderRepo;
    private readonly IAiQueryService _aiQuery;
    private readonly IBusinessRepository _businessRepo;

    public QueryService(IOrderRepository orderRepo, IAiQueryService aiQuery, IBusinessRepository businessRepo)
    {
        _orderRepo = orderRepo;
        _aiQuery = aiQuery;
        _businessRepo = businessRepo;
    }

    public async Task<AskQuestionResponse> AskAsync(AskQuestionRequest request, CancellationToken ct = default)
    {
        var toUtc = DateTime.UtcNow;
        var fromUtc = toUtc.Date; // bugungi kun bo'yicha kontekst beriladi

        var summary = await GetSummaryAsync(request.BusinessId, fromUtc, toUtc, ct);
        var contextJson = JsonSerializer.Serialize(summary);

        var answer = await _aiQuery.AnswerAsync(request.Question, contextJson, ct);
        return new AskQuestionResponse { Answer = answer };
    }

    public async Task<DailySummaryResponse> GetSummaryAsync(
        long businessId, DateTime fromUtc, DateTime toUtc, CancellationToken ct = default)
    {
        var orders = await _orderRepo.GetByBusinessAndRangeAsync(businessId, fromUtc, toUtc, ct);
        var completed = orders.Where(o => o.Status == OrderStatus.Completed).ToList();

        var summary = new DailySummaryResponse
        {
            FromUtc = fromUtc,
            ToUtc = toUtc,
            OrderCount = completed.Count,
            TotalAmount = completed.Sum(o => o.TotalAmount),
            CashAmount = completed.Where(o => o.PaymentType == PaymentType.Cash).Sum(o => o.TotalAmount),
            CardAmount = completed.Where(o => o.PaymentType == PaymentType.Card).Sum(o => o.TotalAmount),
            OnlineAmount = completed.Where(o => o.PaymentType == PaymentType.Online).Sum(o => o.TotalAmount),
        };

        summary.TopProducts = completed
            .SelectMany(o => o.Items)
            .GroupBy(i => i.ProductNameSpoken)
            .Select(g => new TopProductDto
            {
                Name = g.Key,
                TotalQuantity = g.Sum(i => i.Quantity),
                TotalRevenue = g.Sum(i => i.LineTotal),
            })
            .OrderByDescending(p => p.TotalRevenue)
            .Take(10)
            .ToList();

        return summary;
    }

    /// <summary>
    /// Super Admin platforma darajasida tabiiy tilda savol beradi: barcha restoranlar,
    /// supermarketlar, obuna holati va egasi haqida. AI faqat haqiqiy bazadagi
    /// ma'lumotlar asosida javob beradi.
    /// </summary>
    public async Task<AskQuestionResponse> AskSuperAdminAsync(string question, CancellationToken ct = default)
    {
        var businesses = await _businessRepo.GetAllBusinessesAsync(ct);

        var payload = new List<object>();
        foreach (var business in businesses)
        {
            var owner = await _businessRepo.GetOwnerByBusinessIdAnyStateAsync(business.Id, ct);
            payload.Add(new
            {
                Id = business.Id,
                Name = business.Name,
                Type = business.Type.ToString(),
                IsActive = business.IsActive,
                Phone = business.PhoneNumber,
                OwnerFullName = owner?.FullName,
                OwnerPhone = owner?.PhoneNumber,
                SubscriptionEndsAt = owner?.SubscriptionEndsAt,
                SubscriptionActive = owner != null && owner.IsActive && owner.SubscriptionEndsAt > DateTime.UtcNow,
            });
        }

        var contextJson = JsonSerializer.Serialize(payload);
        var answer = await _aiQuery.AnswerPlatformAsync(question, contextJson, ct);
        return new AskQuestionResponse { Answer = answer };
    }
}
