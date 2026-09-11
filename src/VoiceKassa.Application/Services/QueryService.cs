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
        var contextJson = await BuildPlatformContextJsonAsync(ct);
        var answer = await _aiQuery.AnswerPlatformAsync(question, contextJson, ct);
        return new AskQuestionResponse { Answer = answer };
    }

    /// <summary>
    /// Jarvis (Super Admin ovozli yordamchi) buyruq/savolini AI orqali tahlil
    /// qiladi: buyruq bo'lsa amal (action), savol bo'lsa matn javobi qaytadi.
    /// </summary>
    public async Task<JarvisCommandResponse> JarvisCommandAsync(string text, CancellationToken ct = default)
    {
        var contextJson = await BuildPlatformContextJsonAsync(ct);
        return await _aiQuery.InterpretJarvisAsync(text, contextJson, ct);
    }

    /// <summary>
    /// Odiy Admin (biznes egasi) Jarvis buyrug'i: faqat o'z biznesining
    /// stollari, menyusi, xodimlari va bugungi savdosi konteksti beriladi.
    /// Buyruq faqat admin panel amallari bilan cheklanadi.
    /// </summary>
    public async Task<JarvisCommandResponse> OwnerJarvisCommandAsync(long businessId, string text, CancellationToken ct = default)
    {
        var contextJson = await BuildOwnerContextJsonAsync(businessId, ct);
        return await _aiQuery.InterpretOwnerJarvisAsync(text, contextJson, ct);
    }

    /// <summary>Bitta biznesning Jarvis kontekstini JSON qilib tayyorlaydi.</summary>
    private async Task<string> BuildOwnerContextJsonAsync(long businessId, CancellationToken ct)
    {
        var business = await _businessRepo.GetBusinessByIdAsync(businessId, ct);
        var now = DateTime.UtcNow;
        var fromUtc = now.Date;

        var tables = await _businessRepo.GetTablesByBusinessAsync(businessId, ct);
        var products = await _businessRepo.GetProductsByBusinessAsync(businessId, ct);
        var staff = await _businessRepo.GetStaffByBusinessAsync(businessId, ct);
        var orders = await _orderRepo.GetByBusinessAndRangeAsync(businessId, fromUtc, now, ct);
        var completed = orders.Where(o => o.Status == OrderStatus.Completed).ToList();

        var context = new
        {
            NowUtc = now,
            TodayFromUtc = fromUtc,
            Business = business == null ? null : new
            {
                business.Id,
                business.Name,
                Type = business.Type.ToString(),
                business.IsActive,
                business.PhoneNumber,
            },
            Tables = tables.Select(t => new { t.Id, t.Name, Status = t.Status.ToString() }),
            Products = products.Select(p => new { p.Id, p.Name, p.Price, p.StockQuantity }),
            Staff = staff.Select(s => new { s.Id, s.FullName, Role = s.Role.ToString(), s.IsActive }),
            TodayOrders = new
            {
                Count = completed.Count,
                TotalAmount = completed.Sum(o => o.TotalAmount),
                CashAmount = completed.Where(o => o.PaymentType == PaymentType.Cash).Sum(o => o.TotalAmount),
                CardAmount = completed.Where(o => o.PaymentType == PaymentType.Card).Sum(o => o.TotalAmount),
                OnlineAmount = completed.Where(o => o.PaymentType == PaymentType.Online).Sum(o => o.TotalAmount),
                TopProducts = completed
                    .SelectMany(o => o.Items)
                    .GroupBy(i => i.ProductNameSpoken)
                    .Select(g => new { Name = g.Key, Quantity = g.Sum(i => i.Quantity), Revenue = g.Sum(i => i.LineTotal) })
                    .OrderByDescending(p => p.Revenue)
                    .Take(10),
            },
        };

        return JsonSerializer.Serialize(context);
    }

    /// <summary>Platformadagi barcha bizneslar va obunalar kontekstini JSON qilib tayyorlaydi.</summary>
    private async Task<string> BuildPlatformContextJsonAsync(CancellationToken ct)
    {
        var businesses = await _businessRepo.GetAllBusinessesAsync(ct);
        var now = DateTime.UtcNow;

        var payloadItems = new List<object>();
        foreach (var business in businesses)
        {
            var owner = await _businessRepo.GetOwnerByBusinessIdAnyStateAsync(business.Id, ct);
            payloadItems.Add(new
            {
                Id = business.Id,
                Name = business.Name,
                Type = business.Type.ToString(),
                IsActive = business.IsActive,
                Phone = business.PhoneNumber,
                OwnerFullName = owner?.FullName,
                OwnerPhone = owner?.PhoneNumber,
                // To'lov / obuna ma'lumotlari — AI shu asosda "oxirgi 1 oy to'lovi" kabi
                // savollarga javob beradi.
                SubscriptionAmount = owner?.SubscriptionAmount ?? 0m,
                PaymentPaidAt = owner?.PaymentPaidAt,      // oxirgi to'lov sanasi (UTC)
                SubscriptionMonths = owner?.SubscriptionMonths ?? 0,
                SubscriptionEndsAt = owner?.SubscriptionEndsAt,
                SubscriptionActive = owner != null && owner.IsActive && owner.SubscriptionEndsAt > now,
            });
        }

        // AI hozirgi sana va "oxirgi 1 oy" chegarasini bilishi uchun metama'lumot beriladi.
        var context = new
        {
            NowUtc = now,
            LastMonthUtc = now.AddMonths(-1),
            Businesses = payloadItems,
        };

        return JsonSerializer.Serialize(context);
    }
}
