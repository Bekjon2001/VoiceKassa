using System.Text.Json;
using VoiceKassa.Application.DTOs;
using VoiceKassa.Application.Interfaces;
<<<<<<< HEAD
=======
using VoiceKassa.Domain.Enums;
>>>>>>> main

namespace VoiceKassa.Application.Services;

public class QueryService
{
<<<<<<< HEAD
    private readonly ISaleRepository _repo;
    private readonly IAiQueryService _aiQuery;

    public QueryService(ISaleRepository repo, IAiQueryService aiQuery)
    {
        _repo = repo;
=======
    private readonly IOrderRepository _orderRepo;
    private readonly IAiQueryService _aiQuery;

    public QueryService(IOrderRepository orderRepo, IAiQueryService aiQuery)
    {
        _orderRepo = orderRepo;
>>>>>>> main
        _aiQuery = aiQuery;
    }

    public async Task<AskQuestionResponse> AskAsync(AskQuestionRequest request, CancellationToken ct = default)
    {
<<<<<<< HEAD
        var fromUtc = (request.FromDate ?? DateTime.UtcNow.Date).ToUniversalTime();
        var toUtc = (request.ToDate ?? DateTime.UtcNow.Date.AddDays(1)).ToUniversalTime();

        var sales = await _repo.GetByShopAndRangeAsync(request.ShopId, fromUtc, toUtc, ct);

        // Only real, already-computed facts go to the model - it answers
        // from this JSON, it never invents figures on its own.
        var context = sales.Select(s => new
        {
            vaqt = s.CreatedAt,
            summa = s.TotalAmount,
            tolov = s.PaymentType.ToString(),
            mahsulotlar = s.Items.Select(i => new { nomi = i.ProductNameSpoken, miqdor = i.Quantity, birlik = i.Unit, summa = i.LineTotal }),
        });

        var json = JsonSerializer.Serialize(context);
        var answer = await _aiQuery.AnswerAsync(request.Question, json, ct);

        return new AskQuestionResponse { Answer = answer };
    }

    public async Task<DailySummaryResponse> GetSummaryAsync(Guid shopId, DateTime fromUtc, DateTime toUtc, CancellationToken ct = default)
    {
        var sales = await _repo.GetByShopAndRangeAsync(shopId, fromUtc, toUtc, ct);

        var summary = new DailySummaryResponse
        {
            SaleCount = sales.Count,
            TotalAmount = sales.Sum(s => s.TotalAmount),
            CashAmount = sales.Where(s => s.PaymentType == Domain.Enums.PaymentType.Cash).Sum(s => s.TotalAmount),
            CardAmount = sales.Where(s => s.PaymentType == Domain.Enums.PaymentType.Card).Sum(s => s.TotalAmount),
            OnlineAmount = sales.Where(s => s.PaymentType == Domain.Enums.PaymentType.Online).Sum(s => s.TotalAmount),
        };

        summary.TopProducts = sales
            .SelectMany(s => s.Items)
            .GroupBy(i => i.ProductNameSpoken)
            .Select(g => new TopProductDto
            {
                ProductName = g.Key,
                QuantitySold = g.Sum(i => i.Quantity),
                Revenue = g.Sum(i => i.LineTotal),
            })
            .OrderByDescending(p => p.Revenue)
            .Take(5)
=======
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
>>>>>>> main
            .ToList();

        return summary;
    }
}
