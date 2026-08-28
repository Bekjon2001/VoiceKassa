using System.Text.Json;
using VoiceKassa.Application.DTOs;
using VoiceKassa.Application.Interfaces;
using VoiceKassa.Domain.Enums;

namespace VoiceKassa.Application.Services;

public class QueryService
{
    private readonly IOrderRepository _orderRepo;
    private readonly IAiQueryService _aiQuery;

    public QueryService(IOrderRepository orderRepo, IAiQueryService aiQuery)
    {
        _orderRepo = orderRepo;
        _aiQuery = aiQuery;
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
}
