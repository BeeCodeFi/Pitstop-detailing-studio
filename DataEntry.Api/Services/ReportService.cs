using System.Text;
using DataEntry.Api.Data;
using DataEntry.Api.DTOs;
using DataEntry.Api.Services;
using Microsoft.EntityFrameworkCore;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace DataEntry.Api.Services;

public class ReportService
{
    private readonly AppDbContext _db;

    public ReportService(AppDbContext db)
    {
        _db = db;
    }

    public async Task<DailySummaryDto> GetDailySummaryAsync(DateOnly date)
    {
        var entries = await _db.DaybookEntries
            .Include(d => d.Employee)
            .Include(d => d.Sales)
            .Include(d => d.Expenses)
            .Where(d => d.Date == date)
            .ToListAsync();

        var employeeSummaries = entries.Select(e => new EmployeeDaySummaryDto(
            e.EmployeeId, e.Employee.Name, e.OpeningBalance,
            e.TotalSales, e.TotalCashCollected, e.TotalCardCollected,
            e.TotalUpiCollected, e.TotalPendingCollected, e.TotalExpenses, e.ClosingBalance
        )).ToList();

        return new DailySummaryDto(
            date, employeeSummaries,
            employeeSummaries.Sum(s => s.TotalSales),
            employeeSummaries.Sum(s => s.TotalCash),
            employeeSummaries.Sum(s => s.TotalCard),
            employeeSummaries.Sum(s => s.TotalUpi),
            employeeSummaries.Sum(s => s.TotalPending),
            employeeSummaries.Sum(s => s.TotalExpenses)
        );
    }

    public async Task<MonthlySummaryDto> GetMonthlySummaryAsync(int year, int month)
    {
        var startDate = new DateOnly(year, month, 1);
        var endDate = startDate.AddMonths(1).AddDays(-1);

        var entries = await _db.DaybookEntries
            .Include(d => d.Sales)
            .Include(d => d.Expenses)
            .Where(d => d.Date >= startDate && d.Date <= endDate)
            .ToListAsync();

        var salaries = await _db.SalaryPayments
            .Where(s => s.Date >= startDate && s.Date <= endDate)
            .ToListAsync();

        var dailyTotals = entries
            .GroupBy(e => e.Date)
            .Select(g => new DayTotalDto(
                g.Key,
                g.Sum(e => e.TotalSales),
                g.Sum(e => e.TotalCashCollected),
                g.Sum(e => e.TotalExpenses),
                g.Sum(e => e.Sales.Count)
            ))
            .OrderBy(d => d.Date)
            .ToList();

        var grandTotalSales = dailyTotals.Sum(d => d.TotalSales);
        var grandTotalExpenses = dailyTotals.Sum(d => d.TotalExpenses);
        var grandTotalSalaries = salaries.Sum(s => s.Amount);
        var grandTotalPending = entries.SelectMany(e => e.Sales).Where(s => s.PaymentMode == "Pending").Sum(s => s.Amount);

        return new MonthlySummaryDto(
            year, month, dailyTotals,
            grandTotalSales,
            dailyTotals.Sum(d => d.TotalCash),
            grandTotalExpenses,
            grandTotalSalaries,
            grandTotalPending,
            grandTotalSales - grandTotalExpenses - grandTotalSalaries
        );
    }

    public async Task<EmployeeReportDto?> GetEmployeeReportAsync(int employeeId, DateOnly from, DateOnly to)
    {
        var employee = await _db.Employees.FindAsync(employeeId);
        if (employee == null) return null;

        var entries = await _db.DaybookEntries
            .Include(d => d.Sales)
            .Include(d => d.Expenses)
            .Where(d => d.EmployeeId == employeeId && d.Date >= from && d.Date <= to)
            .OrderBy(d => d.Date)
            .ToListAsync();

        var summaries = entries.Select(e => new DaybookEntrySummaryDto(
            e.Date, e.OpeningBalance, e.TotalSales, e.TotalCashCollected,
            e.TotalExpenses, e.ClosingBalance, e.Sales.Count
        )).ToList();

        return new EmployeeReportDto(
            employeeId, employee.Name, from, to, summaries,
            summaries.Sum(s => s.TotalSales),
            summaries.Sum(s => s.TotalCash),
            summaries.Sum(s => s.TotalExpenses)
        );
    }

    public async Task<byte[]> ExportCsvAsync(DateOnly from, DateOnly to, int? employeeId = null, string? type = null, string? paymentMode = null)
    {
        var includeSales = string.IsNullOrEmpty(type) || type == "All" || type == "Sales";
        var includeExpenses = string.IsNullOrEmpty(type) || type == "All" || type == "Expenses";

        var salesQuery = _db.SaleTransactions
            .Include(s => s.DaybookEntry).ThenInclude(d => d.Employee)
            .Include(s => s.Customer)
            .Include(s => s.ServiceType)
            .Where(s => s.DaybookEntry.Date >= from && s.DaybookEntry.Date <= to);

        if (employeeId.HasValue)
            salesQuery = salesQuery.Where(s => s.DaybookEntry.EmployeeId == employeeId.Value);
        if (!string.IsNullOrEmpty(paymentMode))
            salesQuery = salesQuery.Where(s => s.PaymentMode == paymentMode);

        var expensesQuery = _db.Expenses
            .Include(e => e.DaybookEntry).ThenInclude(d => d.Employee)
            .Where(e => e.DaybookEntry.Date >= from && e.DaybookEntry.Date <= to);

        if (employeeId.HasValue)
            expensesQuery = expensesQuery.Where(e => e.DaybookEntry.EmployeeId == employeeId.Value);

        var sales = includeSales
            ? await salesQuery.OrderBy(s => s.DaybookEntry.Date).ThenBy(s => s.CreatedAt).ToListAsync()
            : new List<Models.SaleTransaction>();

        var expenses = includeExpenses
            ? await expensesQuery.OrderBy(e => e.DaybookEntry.Date).ThenBy(e => e.CreatedAt).ToListAsync()
            : new List<Models.Expense>();

        var sb = new StringBuilder();

        // Report header
        sb.AppendLine($"Daybook Export Report");
        sb.AppendLine($"Period,{from:dd MMM yyyy} to {to:dd MMM yyyy}");
        sb.AppendLine($"Generated,{DateTime.Now:dd MMM yyyy HH:mm}");
        sb.AppendLine();

        // Transaction detail
        sb.AppendLine("--- TRANSACTIONS ---");
        sb.AppendLine("Type,Date,Day,Employee,Customer,Service,Vehicle No,Vehicle Type,Amount,Payment Mode,Notes");

        foreach (var s in sales)
        {
            sb.AppendLine(string.Join(",", new[]
            {
                "Sale",
                s.DaybookEntry.Date.ToString("yyyy-MM-dd"),
                s.DaybookEntry.Date.DayOfWeek.ToString(),
                CsvEscape(s.DaybookEntry.Employee.Name),
                CsvEscape(s.Customer?.Name ?? "Walk-in"),
                CsvEscape(s.ServiceType?.Name ?? ""),
                CsvEscape(s.VehicleNumber ?? ""),
                CsvEscape(s.VehicleType ?? ""),
                s.Amount.ToString("F2"),
                s.PaymentMode,
                CsvEscape(s.Notes ?? "")
            }));
        }

        foreach (var e in expenses)
        {
            sb.AppendLine(string.Join(",", new[]
            {
                "Expense",
                e.DaybookEntry.Date.ToString("yyyy-MM-dd"),
                e.DaybookEntry.Date.DayOfWeek.ToString(),
                CsvEscape(e.DaybookEntry.Employee.Name),
                "",
                CsvEscape(e.Description),
                "", "", 
                e.Amount.ToString("F2"),
                "Expense",
                ""
            }));
        }

        // Summary section
        sb.AppendLine();
        sb.AppendLine("--- SUMMARY ---");
        sb.AppendLine($"Total Sales,{sales.Sum(s => s.Amount):F2}");
        sb.AppendLine($"Total Transactions,{sales.Count}");
        sb.AppendLine($"Total Expenses,{expenses.Sum(e => e.Amount):F2}");
        sb.AppendLine($"Net (Sales - Expenses),{(sales.Sum(s => s.Amount) - expenses.Sum(e => e.Amount)):F2}");
        sb.AppendLine($"Pending Payments,{sales.Where(s => s.PaymentMode == "Pending").Sum(s => s.Amount):F2}");

        // By service
        if (sales.Any())
        {
            sb.AppendLine();
            sb.AppendLine("--- BY SERVICE ---");
            sb.AppendLine("Service,Revenue,Transactions,% Share");
            var totalSales = sales.Sum(s => s.Amount);
            var byService = sales
                .GroupBy(s => s.ServiceType?.Name ?? "Unknown")
                .Select(g => (Name: g.Key, Revenue: g.Sum(s => s.Amount), Count: g.Count()))
                .OrderByDescending(s => s.Revenue);
            foreach (var svc in byService)
                sb.AppendLine($"{CsvEscape(svc.Name)},{svc.Revenue:F2},{svc.Count},{(totalSales > 0 ? svc.Revenue / totalSales * 100 : 0):F1}%");

            // By payment mode
            sb.AppendLine();
            sb.AppendLine("--- BY PAYMENT MODE ---");
            sb.AppendLine("Payment Mode,Amount,Transactions,% Share");
            var byMode = sales
                .GroupBy(s => s.PaymentMode)
                .Select(g => (Mode: g.Key, Amount: g.Sum(s => s.Amount), Count: g.Count()))
                .OrderByDescending(s => s.Amount);
            foreach (var mode in byMode)
                sb.AppendLine($"{mode.Mode},{mode.Amount:F2},{mode.Count},{(totalSales > 0 ? mode.Amount / totalSales * 100 : 0):F1}%");

            // By day of week
            sb.AppendLine();
            sb.AppendLine("--- BY DAY OF WEEK ---");
            sb.AppendLine("Day,Total Sales,Transactions,Avg Sale");
            var byDay = sales
                .GroupBy(s => s.DaybookEntry.Date.DayOfWeek)
                .Select(g => (Day: g.Key.ToString(), Total: g.Sum(s => s.Amount), Count: g.Count()))
                .OrderByDescending(d => d.Total);
            foreach (var day in byDay)
                sb.AppendLine($"{day.Day},{day.Total:F2},{day.Count},{(day.Count > 0 ? day.Total / day.Count : 0):F2}");
        }

        return Encoding.UTF8.GetBytes(sb.ToString());
    }

    public async Task<byte[]> GenerateMonthlyPdfAsync(int year, int month, BusinessInsightsDto? insights = null)
    {
        QuestPDF.Settings.License = LicenseType.Community;

        var startDate = new DateOnly(year, month, 1);
        var endDate = startDate.AddMonths(1).AddDays(-1);
        var monthName = new DateTime(year, month, 1).ToString("MMMM yyyy");

        var entries = await _db.DaybookEntries
            .Include(d => d.Employee)
            .Include(d => d.Sales).ThenInclude(s => s.ServiceType)
            .Include(d => d.Expenses)
            .Where(d => d.Date >= startDate && d.Date <= endDate)
            .OrderBy(d => d.Date)
            .ToListAsync();

        var salaries = await _db.SalaryPayments
            .Include(s => s.Employee)
            .Where(s => s.Date >= startDate && s.Date <= endDate)
            .ToListAsync();

        var allSales = entries.SelectMany(e => e.Sales).ToList();
        var totalSales = allSales.Sum(s => s.Amount);
        var totalExpenses = entries.Sum(e => e.TotalExpenses);
        var totalSalaries = salaries.Sum(s => s.Amount);
        var totalPending = allSales.Where(s => s.PaymentMode == "Pending").Sum(s => s.Amount);
        var totalCash = allSales.Where(s => s.PaymentMode == "Cash").Sum(s => s.Amount);
        var netIncome = totalSales - totalExpenses - totalSalaries;

        var dailyTotals = entries.GroupBy(e => e.Date)
            .Select(g => new
            {
                Date = g.Key,
                Sales = g.Sum(e => e.TotalSales),
                Cash = g.Sum(e => e.TotalCashCollected),
                Card = g.Sum(e => e.TotalCardCollected),
                Upi = g.Sum(e => e.TotalUpiCollected),
                Pending = g.Sum(e => e.TotalPendingCollected),
                Expenses = g.Sum(e => e.TotalExpenses),
                Txns = g.Sum(e => e.Sales.Count)
            })
            .OrderBy(d => d.Date).ToList();

        var serviceBreakdown = allSales
            .Where(s => s.ServiceType != null)
            .GroupBy(s => s.ServiceType!.Name)
            .Select(g => new { Service = g.Key, Revenue = g.Sum(s => s.Amount), Count = g.Count() })
            .OrderByDescending(s => s.Revenue).ToList();

        var paymentBreakdown = allSales
            .GroupBy(s => s.PaymentMode)
            .Select(g => new { Mode = g.Key, Amount = g.Sum(s => s.Amount), Count = g.Count() })
            .OrderByDescending(s => s.Amount).ToList();

        var document = Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.A4);
                page.Margin(1.5f, Unit.Centimetre);
                page.DefaultTextStyle(x => x.FontSize(9).FontFamily("Arial"));

                // Header
                page.Header().PaddingBottom(8).Column(col =>
                {
                    col.Item().Row(row =>
                    {
                        row.RelativeItem().Text("Monthly Business Report").FontSize(18).Bold().FontColor("#1a56db");
                        row.ConstantItem(120).AlignRight().Column(c =>
                        {
                            c.Item().AlignRight().Text(monthName).FontSize(11).SemiBold().FontColor("#374151");
                            c.Item().AlignRight().Text($"Generated {DateTime.Now:dd MMM yyyy}").FontSize(7).FontColor("#9ca3af");
                        });
                    });
                    col.Item().PaddingTop(4).LineHorizontal(1.5f).LineColor("#1a56db");
                });

                page.Content().Column(col =>
                {
                    // KPI cards
                    col.Item().PaddingTop(10).Row(row =>
                    {
                        void KpiCard(RowDescriptor r, string label, string value, string bg, string textColor)
                        {
                            r.RelativeItem().Background(bg).Padding(8).Column(c =>
                            {
                                c.Item().Text(label).FontSize(7.5f).FontColor(textColor).SemiBold();
                                c.Item().PaddingTop(2).Text(value).FontSize(14).Bold().FontColor(textColor);
                            });
                        }

                        KpiCard(row, "Total Revenue", $"₹{totalSales:N0}", "#ecfdf5", "#065f46");
                        row.ConstantItem(6);
                        KpiCard(row, "Total Expenses", $"₹{totalExpenses:N0}", "#fef2f2", "#7f1d1d");
                        row.ConstantItem(6);
                        KpiCard(row, "Salaries Paid", $"₹{totalSalaries:N0}", "#fffbeb", "#78350f");
                        row.ConstantItem(6);
                        KpiCard(row, "Net Income", $"₹{netIncome:N0}", "#eff6ff", "#1e3a8a");
                        row.ConstantItem(6);
                        KpiCard(row, "Pending (Unpaid)", $"₹{totalPending:N0}", "#fff1f2", "#881337");
                    });

                    // Daily breakdown
                    col.Item().PaddingTop(14).Text("Daily Breakdown").FontSize(11).Bold().FontColor("#111827");
                    col.Item().PaddingTop(5).Table(table =>
                    {
                        table.ColumnsDefinition(cols =>
                        {
                            cols.ConstantColumn(48);
                            cols.RelativeColumn();
                            cols.RelativeColumn();
                            cols.RelativeColumn();
                            cols.RelativeColumn();
                            cols.RelativeColumn();
                            cols.RelativeColumn();
                            cols.ConstantColumn(28);
                        });

                        table.Header(h =>
                        {
                            static IContainer Hdr(IContainer c) => c.Background("#1a56db").Padding(5).AlignCenter();
                            foreach (var lbl in new[] { "Date", "Sales", "Cash", "Card", "UPI", "Pending", "Expenses", "Txns" })
                                h.Cell().Element(Hdr).Text(lbl).FontSize(7.5f).Bold().FontColor(Colors.White);
                        });

                        var i = 0;
                        foreach (var d in dailyTotals)
                        {
                            var bg = i++ % 2 == 0 ? "#ffffff" : "#f9fafb";
                            IContainer Cell(IContainer c) => c.Background(bg).PaddingVertical(4).PaddingHorizontal(5);
                            IContainer CellR(IContainer c) => c.Background(bg).PaddingVertical(4).PaddingHorizontal(5).AlignRight();

                            table.Cell().Element(Cell).Text(d.Date.ToString("dd MMM")).FontSize(7.5f);
                            table.Cell().Element(CellR).Text($"₹{d.Sales:N0}").FontSize(7.5f).SemiBold();
                            table.Cell().Element(CellR).Text($"₹{d.Cash:N0}").FontSize(7.5f);
                            table.Cell().Element(CellR).Text($"₹{d.Card:N0}").FontSize(7.5f);
                            table.Cell().Element(CellR).Text($"₹{d.Upi:N0}").FontSize(7.5f);
                            table.Cell().Element(CellR).Text(d.Pending > 0 ? $"₹{d.Pending:N0}" : "-")
                                .FontSize(7.5f).FontColor(d.Pending > 0 ? "#dc2626" : "#9ca3af");
                            table.Cell().Element(CellR).Text($"₹{d.Expenses:N0}").FontSize(7.5f);
                            table.Cell().Element(CellR).Text(d.Txns.ToString()).FontSize(7.5f);
                        }

                        // Totals row
                        IContainer Tot(IContainer c) => c.Background("#dbeafe").PaddingVertical(5).PaddingHorizontal(5);
                        IContainer TotR(IContainer c) => c.Background("#dbeafe").PaddingVertical(5).PaddingHorizontal(5).AlignRight();
                        table.Cell().Element(Tot).Text("TOTAL").FontSize(7.5f).Bold();
                        table.Cell().Element(TotR).Text($"₹{totalSales:N0}").FontSize(7.5f).Bold();
                        table.Cell().Element(TotR).Text($"₹{totalCash:N0}").FontSize(7.5f).Bold();
                        table.Cell().Element(TotR).Text($"₹{allSales.Where(s => s.PaymentMode == "Card").Sum(s => s.Amount):N0}").FontSize(7.5f).Bold();
                        table.Cell().Element(TotR).Text($"₹{allSales.Where(s => s.PaymentMode == "UPI").Sum(s => s.Amount):N0}").FontSize(7.5f).Bold();
                        table.Cell().Element(TotR).Text(totalPending > 0 ? $"₹{totalPending:N0}" : "-").FontSize(7.5f).Bold().FontColor(totalPending > 0 ? "#dc2626" : "#9ca3af");
                        table.Cell().Element(TotR).Text($"₹{totalExpenses:N0}").FontSize(7.5f).Bold();
                        table.Cell().Element(TotR).Text($"{dailyTotals.Sum(d => d.Txns)}").FontSize(7.5f).Bold();
                    });

                    // Service breakdown + payment mode side by side
                    col.Item().PaddingTop(14).Row(row =>
                    {
                        // Service breakdown
                        row.RelativeItem(3).Column(c =>
                        {
                            c.Item().Text("Service-wise Revenue").FontSize(11).Bold().FontColor("#111827");
                            c.Item().PaddingTop(5).Table(t =>
                            {
                                t.ColumnsDefinition(cols =>
                                {
                                    cols.RelativeColumn(3);
                                    cols.RelativeColumn(2);
                                    cols.RelativeColumn();
                                    cols.RelativeColumn();
                                });
                                t.Header(h =>
                                {
                                    static IContainer Hdr(IContainer ct) => ct.Background("#059669").Padding(5);
                                    foreach (var lbl in new[] { "Service", "Revenue", "Txns", "%" })
                                        h.Cell().Element(Hdr).Text(lbl).FontSize(7.5f).Bold().FontColor(Colors.White);
                                });
                                var si = 0;
                                foreach (var s in serviceBreakdown)
                                {
                                    var bg = si++ % 2 == 0 ? "#ffffff" : "#f0fdf4";
                                    t.Cell().Background(bg).Padding(4).Text(s.Service).FontSize(7.5f);
                                    t.Cell().Background(bg).Padding(4).AlignRight().Text($"₹{s.Revenue:N0}").FontSize(7.5f).SemiBold();
                                    t.Cell().Background(bg).Padding(4).AlignRight().Text(s.Count.ToString()).FontSize(7.5f);
                                    t.Cell().Background(bg).Padding(4).AlignRight().Text(totalSales > 0 ? $"{s.Revenue / totalSales * 100:F1}%" : "0%").FontSize(7.5f);
                                }
                            });
                        });

                        row.ConstantItem(12);

                        // Payment mode + Financial summary stacked
                        row.RelativeItem(2).Column(c =>
                        {
                            c.Item().Text("Payment Mode").FontSize(11).Bold().FontColor("#111827");
                            c.Item().PaddingTop(5).Table(t =>
                            {
                                t.ColumnsDefinition(cols => { cols.RelativeColumn(2); cols.RelativeColumn(2); cols.RelativeColumn(); });
                                t.Header(h =>
                                {
                                    static IContainer Hdr(IContainer ct) => ct.Background("#7c3aed").Padding(5);
                                    foreach (var lbl in new[] { "Mode", "Amount", "%" })
                                        h.Cell().Element(Hdr).Text(lbl).FontSize(7.5f).Bold().FontColor(Colors.White);
                                });
                                var pi = 0;
                                foreach (var p in paymentBreakdown)
                                {
                                    var bg = pi++ % 2 == 0 ? "#ffffff" : "#f5f3ff";
                                    t.Cell().Background(bg).Padding(4).Text(p.Mode).FontSize(7.5f);
                                    t.Cell().Background(bg).Padding(4).AlignRight().Text($"₹{p.Amount:N0}").FontSize(7.5f).SemiBold();
                                    t.Cell().Background(bg).Padding(4).AlignRight().Text(totalSales > 0 ? $"{p.Amount / totalSales * 100:F1}%" : "0%").FontSize(7.5f);
                                }
                            });

                            c.Item().PaddingTop(10).Text("Financial Summary").FontSize(11).Bold().FontColor("#111827");
                            c.Item().PaddingTop(5).Table(t =>
                            {
                                t.ColumnsDefinition(cols => { cols.RelativeColumn(2); cols.RelativeColumn(2); });
                                t.Header(h =>
                                {
                                    static IContainer Hdr(IContainer ct) => ct.Background("#dc2626").Padding(5);
                                    h.Cell().Element(Hdr).Text("Category").FontSize(7.5f).Bold().FontColor(Colors.White);
                                    h.Cell().Element(Hdr).AlignRight().Text("Amount").FontSize(7.5f).Bold().FontColor(Colors.White);
                                });
                                var rows = new (string, decimal)[]
                                {
                                    ("Total Sales", totalSales),
                                    ("Total Expenses", totalExpenses),
                                    ("Salaries Paid", totalSalaries),
                                    ("Net Income", netIncome),
                                };
                                var ri = 0;
                                foreach (var (label, val) in rows)
                                {
                                    var bg = ri++ % 2 == 0 ? "#ffffff" : "#fef2f2";
                                    t.Cell().Background(bg).Padding(4).Text(label).FontSize(7.5f);
                                    t.Cell().Background(bg).Padding(4).AlignRight().Text($"₹{val:N0}").FontSize(7.5f).SemiBold().FontColor(val < 0 ? "#dc2626" : "#111827");
                                }
                            });
                        });
                    });

                    // AI Insights section
                    if (insights != null && insights.AiInsights.Count > 0)
                    {
                        col.Item().PaddingTop(16).Text("AI Business Insights").FontSize(11).Bold().FontColor("#1a56db");
                        if (!string.IsNullOrWhiteSpace(insights.AiSummary))
                            col.Item().PaddingTop(5).Background("#eff6ff").Border(0.5f).BorderColor("#bfdbfe").Padding(8).Text(insights.AiSummary).FontSize(8.5f).FontColor("#1e3a8a");

                        if (insights.AiInsights.Any())
                        {
                            col.Item().PaddingTop(8).Row(row =>
                            {
                                foreach (var insight in insights.AiInsights.Take(4))
                                {
                                    var (bg, border, fg) = insight.Type switch
                                    {
                                        "positive" => ("#f0fdf4", "#86efac", "#166534"),
                                        "negative" => ("#fef2f2", "#fca5a5", "#7f1d1d"),
                                        _ => ("#f9fafb", "#d1d5db", "#374151")
                                    };
                                    row.RelativeItem().Background(bg).Border(0.5f).BorderColor(border).Padding(7).Column(c =>
                                    {
                                        c.Item().Text(insight.Title).FontSize(8).Bold().FontColor(fg);
                                        c.Item().PaddingTop(2).Text(insight.Description).FontSize(7.5f).FontColor(fg);
                                    });
                                    row.ConstantItem(6);
                                }
                            });
                        }

                        if (insights.AiRecommendations.Any())
                        {
                            col.Item().PaddingTop(8).Text("Recommendations").FontSize(9).SemiBold().FontColor("#111827");
                            foreach (var rec in insights.AiRecommendations)
                                col.Item().PaddingTop(2).PaddingLeft(8).Text($"• {rec}").FontSize(8f).FontColor("#374151");
                        }

                        if (insights.AiAlerts.Any())
                        {
                            col.Item().PaddingTop(6).Text("⚠ Alerts").FontSize(9).SemiBold().FontColor("#dc2626");
                            foreach (var alert in insights.AiAlerts)
                                col.Item().PaddingTop(2).PaddingLeft(8).Text($"• {alert}").FontSize(8f).FontColor("#dc2626");
                        }
                    }
                });

                page.Footer().AlignRight().Text(x =>
                {
                    x.Span("Page ").FontSize(7.5f).FontColor("#9ca3af");
                    x.CurrentPageNumber().FontSize(7.5f).FontColor("#9ca3af");
                    x.Span(" of ").FontSize(7.5f).FontColor("#9ca3af");
                    x.TotalPages().FontSize(7.5f).FontColor("#9ca3af");
                    x.Span($" | {monthName} Report | {DateTime.Now:dd MMM yyyy}").FontSize(7.5f).FontColor("#9ca3af");
                });
            });
        });

        return document.GeneratePdf();
    }

    private static string CsvEscape(string value)
    {
        if (value.Contains(',') || value.Contains('"') || value.Contains('\n') || value.Contains('\r'))
            return $"\"{value.Replace("\"", "\"\"")}\"";
        return value;
    }
}
