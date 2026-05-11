using System.Collections.Concurrent;
using System.Net.Http.Json;
using System.Text.Json;
using DataEntry.Api.Data;
using DataEntry.Api.DTOs;
using Microsoft.EntityFrameworkCore;

namespace DataEntry.Api.Services;

public class InsightService
{
    private readonly AppDbContext _db;
    private readonly IConfiguration _config;
    private readonly HttpClient _httpClient;

    private static readonly ConcurrentDictionary<string, (BusinessInsightsDto Data, DateTime ExpiresAt)> _cache = new();
    private static readonly TimeSpan CacheTtl = TimeSpan.FromHours(1);

    public InsightService(AppDbContext db, IConfiguration config, HttpClient httpClient)
    {
        _db = db;
        _config = config;
        _httpClient = httpClient;
    }

    public async Task<BusinessInsightsDto> GetInsightsAsync(int year, int month)
    {
        var cacheKey = $"{year}-{month}";
        if (_cache.TryGetValue(cacheKey, out var cached) && cached.ExpiresAt > DateTime.UtcNow)
            return cached.Data;

        var startDate = new DateOnly(year, month, 1);
        var endDate = startDate.AddMonths(1).AddDays(-1);
        var prevStartDate = startDate.AddMonths(-1);
        var prevEndDate = startDate.AddDays(-1);

        var entries = await _db.DaybookEntries
            .Include(d => d.Sales).ThenInclude(s => s.ServiceType)
            .Include(d => d.Expenses)
            .Where(d => d.Date >= startDate && d.Date <= endDate)
            .ToListAsync();

        var prevEntries = await _db.DaybookEntries
            .Include(d => d.Sales)
            .Where(d => d.Date >= prevStartDate && d.Date <= prevEndDate)
            .ToListAsync();

        var allSales = entries.SelectMany(e => e.Sales).ToList();
        var totalSales = allSales.Sum(s => s.Amount);
        var totalExpenses = entries.Sum(e => e.TotalExpenses);
        var netIncome = totalSales - totalExpenses;
        var prevSales = prevEntries.SelectMany(e => e.Sales).Sum(s => s.Amount);
        var growthPercent = prevSales > 0
            ? Math.Round((totalSales - prevSales) / prevSales * 100, 1)
            : 0;
        var pendingAmount = allSales.Where(s => s.PaymentMode == "Pending").Sum(s => s.Amount);
        var activeDays = entries.Select(e => e.Date).Distinct().Count();
        var avgDailySales = activeDays > 0 ? Math.Round(totalSales / activeDays, 2) : 0;

        var serviceBreakdown = allSales
            .Where(s => s.ServiceType != null)
            .GroupBy(s => s.ServiceType!.Name)
            .Select(g => new ServiceBreakdownItem(
                g.Key,
                g.Sum(s => s.Amount),
                g.Count(),
                totalSales > 0 ? Math.Round(g.Sum(s => s.Amount) / totalSales * 100, 1) : 0
            ))
            .OrderByDescending(s => s.TotalRevenue)
            .ToList();

        var paymentBreakdown = allSales
            .GroupBy(s => s.PaymentMode)
            .Select(g => new PaymentModeBreakdownItem(
                g.Key,
                g.Sum(s => s.Amount),
                g.Count(),
                totalSales > 0 ? Math.Round(g.Sum(s => s.Amount) / totalSales * 100, 1) : 0
            ))
            .OrderByDescending(s => s.Amount)
            .ToList();

        var dayOfWeekBreakdown = entries
            .GroupBy(e => e.Date.DayOfWeek)
            .Select(g => new DayOfWeekBreakdownItem(
                g.Key.ToString(),
                g.Count() > 0 ? Math.Round(g.Sum(e => e.TotalSales) / g.Count(), 2) : 0,
                g.Sum(e => e.Sales.Count)
            ))
            .OrderByDescending(d => d.AverageSales)
            .ToList();

        var dailySales = entries
            .GroupBy(e => e.Date)
            .Select(g => (Date: g.Key, Sales: g.Sum(e => e.TotalSales)))
            .ToList();

        var bestDay = dailySales.Any()
            ? dailySales.OrderByDescending(d => d.Sales).First().Date.ToString("MMM dd")
            : "N/A";
        var worstDay = dailySales.Where(d => d.Sales > 0).Any()
            ? dailySales.Where(d => d.Sales > 0).OrderBy(d => d.Sales).First().Date.ToString("MMM dd")
            : "N/A";

        var topService = serviceBreakdown.FirstOrDefault()?.ServiceName ?? "N/A";
        var topPaymentMode = paymentBreakdown.FirstOrDefault()?.PaymentMode ?? "N/A";

        var (aiSummary, aiInsights, aiRecommendations, aiAlerts) = await GetGeminiInsightsAsync(
            year, month, totalSales, totalExpenses, netIncome, growthPercent,
            pendingAmount, avgDailySales, allSales.Count, serviceBreakdown, paymentBreakdown, dayOfWeekBreakdown);

        var result = new BusinessInsightsDto(
            year, month,
            totalSales, totalExpenses, netIncome,
            growthPercent, avgDailySales, pendingAmount, allSales.Count,
            topService, topPaymentMode, bestDay, worstDay,
            serviceBreakdown, paymentBreakdown, dayOfWeekBreakdown,
            aiSummary, aiInsights, aiRecommendations, aiAlerts
        );

        // Only cache if AI call succeeded (not a rate-limit/error response)
        if (!aiSummary.StartsWith("AI unavailable") && !aiSummary.StartsWith("AI insights"))
            _cache[cacheKey] = (result, DateTime.UtcNow.Add(CacheTtl));

        return result;
    }

    private async Task<(string Summary, List<AiInsightItem> Insights, List<string> Recommendations, List<string> Alerts)>
        GetGeminiInsightsAsync(
            int year, int month,
            decimal totalSales, decimal totalExpenses, decimal netIncome,
            decimal growthPercent, decimal pendingAmount, decimal avgDailySales, int txCount,
            List<ServiceBreakdownItem> services, List<PaymentModeBreakdownItem> payments,
            List<DayOfWeekBreakdownItem> dayBreakdown)
    {
        var apiKey = _config["Gemini:ApiKey"] ?? Environment.GetEnvironmentVariable("GEMINI_API_KEY");
        if (string.IsNullOrWhiteSpace(apiKey))
            return ("AI insights not available — configure GEMINI_API_KEY to enable.", new(), new(), new());

        var monthName = new DateTime(year, month, 1).ToString("MMMM yyyy");
        var topServices = string.Join(", ", services.Take(5).Select(s => $"{s.ServiceName} (₹{s.TotalRevenue:N0}, {s.Percentage}%)"));
        var paymentModes = string.Join(", ", payments.Select(p => $"{p.PaymentMode}: {p.Percentage}%"));
        var busyDays = string.Join(", ", dayBreakdown.Take(3).Select(d => $"{d.DayName} (avg ₹{d.AverageSales:N0})"));
        var growthLabel = growthPercent >= 0 ? $"+{growthPercent}%" : $"{growthPercent}%";

        var prompt = $@"You are a business analyst for a car service/wash shop in India. Analyze this monthly business data and respond with ONLY valid JSON — no markdown, no extra text.

Monthly Report: {monthName}
- Total Revenue: ₹{totalSales:N0}
- Total Expenses: ₹{totalExpenses:N0}
- Net Income: ₹{netIncome:N0}
- Revenue vs Last Month: {growthLabel}
- Average Daily Revenue: ₹{avgDailySales:N0}
- Total Transactions: {txCount}
- Pending (unpaid) Payments: ₹{pendingAmount:N0}
- Top Services: {(string.IsNullOrEmpty(topServices) ? "N/A" : topServices)}
- Payment Split: {(string.IsNullOrEmpty(paymentModes) ? "N/A" : paymentModes)}
- Busiest Days: {(string.IsNullOrEmpty(busyDays) ? "N/A" : busyDays)}

Respond with ONLY this JSON (3-5 insights, 3-4 recommendations, alerts only if critical):
{{
  ""summary"": ""2-3 sentence business performance summary"",
  ""insights"": [
    {{""title"": ""short title"", ""description"": ""1-2 sentences"", ""type"": ""positive""}}
  ],
  ""recommendations"": [""actionable tip 1"", ""actionable tip 2""],
  ""alerts"": [""critical issue if any""]
}}
insight types must be: positive, negative, or neutral.";

        try
        {
            var url = $"https://generativelanguage.googleapis.com/v1beta/models/gemini-2.0-flash:generateContent?key={apiKey}";
            var requestBody = new
            {
                contents = new[] { new { parts = new[] { new { text = prompt } } } },
                generationConfig = new { responseMimeType = "application/json", temperature = 0.4 }
            };

            var response = await _httpClient.PostAsJsonAsync(url, requestBody);
            if (!response.IsSuccessStatusCode)
            {
                var statusCode = (int)response.StatusCode;
                var msg = statusCode == 429
                    ? "AI insights rate limit reached — please try again in a minute."
                    : $"AI unavailable (HTTP {statusCode})."; 
                return (msg, new(), new(), new());
            }

            var responseJson = await response.Content.ReadAsStringAsync();
            using var doc = JsonDocument.Parse(responseJson);
            var text = doc.RootElement
                .GetProperty("candidates")[0]
                .GetProperty("content")
                .GetProperty("parts")[0]
                .GetProperty("text")
                .GetString() ?? "{}";

            using var aiDoc = JsonDocument.Parse(text);
            var root = aiDoc.RootElement;

            var summary = root.TryGetProperty("summary", out var s) ? s.GetString() ?? "" : "";

            var insights = new List<AiInsightItem>();
            if (root.TryGetProperty("insights", out var arr))
                foreach (var item in arr.EnumerateArray())
                    insights.Add(new AiInsightItem(
                        item.TryGetProperty("title", out var t) ? t.GetString() ?? "" : "",
                        item.TryGetProperty("description", out var d) ? d.GetString() ?? "" : "",
                        item.TryGetProperty("type", out var ty) ? ty.GetString() ?? "neutral" : "neutral"
                    ));

            var recommendations = new List<string>();
            if (root.TryGetProperty("recommendations", out var recsArr))
                foreach (var r in recsArr.EnumerateArray())
                    recommendations.Add(r.GetString() ?? "");

            var alerts = new List<string>();
            if (root.TryGetProperty("alerts", out var alertsArr))
                foreach (var a in alertsArr.EnumerateArray())
                    alerts.Add(a.GetString() ?? "");

            return (summary, insights, recommendations, alerts);
        }
        catch (Exception ex)
        {
            return ($"AI insights temporarily unavailable: {ex.Message}", new(), new(), new());
        }
    }
}
