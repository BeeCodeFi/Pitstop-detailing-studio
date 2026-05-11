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
        var endDate   = startDate.AddMonths(1).AddDays(-1);
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

        var allSales      = entries.SelectMany(e => e.Sales).ToList();
        var totalSales    = allSales.Sum(s => s.Amount);
        var totalExpenses = entries.Sum(e => e.TotalExpenses);
        var totalSalaries = salaries.Sum(s => s.Amount);
        var totalPending  = allSales.Where(s => s.PaymentMode == "Pending").Sum(s => s.Amount);
        var totalCash     = allSales.Where(s => s.PaymentMode == "Cash").Sum(s => s.Amount);
        var totalCard     = allSales.Where(s => s.PaymentMode == "Card").Sum(s => s.Amount);
        var totalUpi      = allSales.Where(s => s.PaymentMode == "UPI").Sum(s => s.Amount);
        var netIncome     = totalSales - totalExpenses - totalSalaries;

        var dailyTotals = entries.GroupBy(e => e.Date)
            .Select(g => new
            {
                Date     = g.Key,
                Sales    = g.Sum(e => e.TotalSales),
                Cash     = g.Sum(e => e.TotalCashCollected),
                Card     = g.Sum(e => e.TotalCardCollected),
                Upi      = g.Sum(e => e.TotalUpiCollected),
                Pending  = g.Sum(e => e.TotalPendingCollected),
                Expenses = g.Sum(e => e.TotalExpenses),
                Txns     = g.Sum(e => e.Sales.Count)
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

        // format helper
        static string Amt(decimal v) => $"\u20B9{v:N0}";

        var document = Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(297, 210, Unit.Millimetre);
                page.Margin(1.5f, Unit.Centimetre);
                page.DefaultTextStyle(x => x.FontSize(8f).FontFamily("Arial"));

                page.Header().PaddingBottom(5).Column(col =>
                {
                    col.Item().Row(row =>
                    {
                        row.RelativeItem().Column(c =>
                        {
                            c.Item().Text("Monthly Business Report").FontSize(14).Bold().FontColor("#1a56db");
                            c.Item().Text(monthName).FontSize(9).SemiBold().FontColor("#374151");
                        });
                        row.ConstantItem(200).AlignRight().AlignBottom()
                            .Text($"Generated {DateTime.Now:dd MMM yyyy HH:mm}").FontSize(7).FontColor("#9ca3af");
                    });
                    col.Item().PaddingTop(4).LineHorizontal(1.5f).LineColor("#1a56db");
                });

                page.Content().PaddingTop(8).Column(col =>
                {
                    col.Item().Row(row =>
                    {
                        void Kpi(RowDescriptor r, string lbl, string val, string bg, string fg)
                        {
                            r.RelativeItem().Background(bg).Border(0.5f).BorderColor("#e5e7eb").Padding(7).Column(c =>
                            {
                                c.Item().Text(lbl).FontSize(7f).FontColor(fg);
                                c.Item().PaddingTop(2).Text(val).FontSize(11).Bold().FontColor(fg);
                            });
                        }
                        Kpi(row, "Total Revenue",      Amt(totalSales),    "#ecfdf5", "#065f46");
                        row.ConstantItem(5);
                        Kpi(row, "Total Expenses",     Amt(totalExpenses), "#fef2f2", "#7f1d1d");
                        row.ConstantItem(5);
                        Kpi(row, "Salaries Paid",      Amt(totalSalaries), "#fffbeb", "#78350f");
                        row.ConstantItem(5);
                        Kpi(row, "Net Income",         Amt(netIncome),
                            netIncome >= 0 ? "#eff6ff" : "#fef2f2",
                            netIncome >= 0 ? "#1e3a8a" : "#7f1d1d");
                        row.ConstantItem(5);
                        Kpi(row, "Pending (Unpaid)",   Amt(totalPending),  "#fff1f2", "#881337");
                        row.ConstantItem(5);
                        Kpi(row, "Total Transactions", dailyTotals.Sum(d => d.Txns).ToString(), "#f5f3ff", "#4c1d95");
                    });

                    col.Item().PaddingTop(10).Text("Daily Breakdown").FontSize(9.5f).Bold().FontColor("#111827");
                    col.Item().PaddingTop(4).Table(table =>
                    {
                        table.ColumnsDefinition(cols =>
                        {
                            cols.RelativeColumn(2);
                            cols.RelativeColumn(3);
                            cols.RelativeColumn(2);
                            cols.RelativeColumn(2);
                            cols.RelativeColumn(2);
                            cols.RelativeColumn(2);
                            cols.RelativeColumn(2);
                            cols.RelativeColumn(1);
                        });
                        table.Header(h =>
                        {
                            const string hb = "#1a56db";
                            h.Cell().Background(hb).PaddingVertical(5).PaddingHorizontal(4).Text("Date").FontSize(8).Bold().FontColor("#ffffff");
                            h.Cell().Background(hb).PaddingVertical(5).PaddingHorizontal(4).AlignRight().Text("Sales").FontSize(8).Bold().FontColor("#ffffff");
                            h.Cell().Background(hb).PaddingVertical(5).PaddingHorizontal(4).AlignRight().Text("Cash").FontSize(8).Bold().FontColor("#ffffff");
                            h.Cell().Background(hb).PaddingVertical(5).PaddingHorizontal(4).AlignRight().Text("Card").FontSize(8).Bold().FontColor("#ffffff");
                            h.Cell().Background(hb).PaddingVertical(5).PaddingHorizontal(4).AlignRight().Text("UPI").FontSize(8).Bold().FontColor("#ffffff");
                            h.Cell().Background(hb).PaddingVertical(5).PaddingHorizontal(4).AlignRight().Text("Pending").FontSize(8).Bold().FontColor("#ffffff");
                            h.Cell().Background(hb).PaddingVertical(5).PaddingHorizontal(4).AlignRight().Text("Expenses").FontSize(8).Bold().FontColor("#ffffff");
                            h.Cell().Background(hb).PaddingVertical(5).PaddingHorizontal(4).AlignRight().Text("#").FontSize(8).Bold().FontColor("#ffffff");
                        });
                        var ri = 0;
                        foreach (var d in dailyTotals)
                        {
                            var bg = ri++ % 2 == 0 ? "#ffffff" : "#f9fafb";
                            table.Cell().Background(bg).PaddingVertical(4).PaddingHorizontal(4).Text(d.Date.ToString("dd MMM")).FontSize(7.5f);
                            table.Cell().Background(bg).PaddingVertical(4).PaddingHorizontal(4).AlignRight().Text(Amt(d.Sales)).FontSize(7.5f).SemiBold();
                            table.Cell().Background(bg).PaddingVertical(4).PaddingHorizontal(4).AlignRight().Text(Amt(d.Cash)).FontSize(7.5f);
                            table.Cell().Background(bg).PaddingVertical(4).PaddingHorizontal(4).AlignRight().Text(Amt(d.Card)).FontSize(7.5f);
                            table.Cell().Background(bg).PaddingVertical(4).PaddingHorizontal(4).AlignRight().Text(Amt(d.Upi)).FontSize(7.5f);
                            table.Cell().Background(bg).PaddingVertical(4).PaddingHorizontal(4).AlignRight().Text(d.Pending > 0 ? Amt(d.Pending) : "-").FontSize(7.5f).FontColor(d.Pending > 0 ? "#dc2626" : "#9ca3af");
                            table.Cell().Background(bg).PaddingVertical(4).PaddingHorizontal(4).AlignRight().Text(Amt(d.Expenses)).FontSize(7.5f);
                            table.Cell().Background(bg).PaddingVertical(4).PaddingHorizontal(4).AlignRight().Text(d.Txns.ToString()).FontSize(7.5f);
                        }
                        const string tb = "#dbeafe";
                        table.Cell().Background(tb).PaddingVertical(5).PaddingHorizontal(4).Text("TOTAL").FontSize(7.5f).Bold();
                        table.Cell().Background(tb).PaddingVertical(5).PaddingHorizontal(4).AlignRight().Text(Amt(totalSales)).FontSize(7.5f).Bold();
                        table.Cell().Background(tb).PaddingVertical(5).PaddingHorizontal(4).AlignRight().Text(Amt(totalCash)).FontSize(7.5f).Bold();
                        table.Cell().Background(tb).PaddingVertical(5).PaddingHorizontal(4).AlignRight().Text(Amt(totalCard)).FontSize(7.5f).Bold();
                        table.Cell().Background(tb).PaddingVertical(5).PaddingHorizontal(4).AlignRight().Text(Amt(totalUpi)).FontSize(7.5f).Bold();
                        table.Cell().Background(tb).PaddingVertical(5).PaddingHorizontal(4).AlignRight().Text(totalPending > 0 ? Amt(totalPending) : "-").FontSize(7.5f).Bold().FontColor(totalPending > 0 ? "#dc2626" : "#9ca3af");
                        table.Cell().Background(tb).PaddingVertical(5).PaddingHorizontal(4).AlignRight().Text(Amt(totalExpenses)).FontSize(7.5f).Bold();
                        table.Cell().Background(tb).PaddingVertical(5).PaddingHorizontal(4).AlignRight().Text(dailyTotals.Sum(d => d.Txns).ToString()).FontSize(7.5f).Bold();
                    });

                    col.Item().PaddingTop(12).Row(row =>
                    {
                        row.RelativeItem(5).Column(c =>
                        {
                            c.Item().Text("Service-wise Revenue").FontSize(9.5f).Bold().FontColor("#111827");
                            c.Item().PaddingTop(4).Table(t =>
                            {
                                t.ColumnsDefinition(cols => { cols.RelativeColumn(4); cols.RelativeColumn(3); cols.RelativeColumn(1); cols.RelativeColumn(2); });
                                t.Header(h =>
                                {
                                    const string hb = "#059669";
                                    h.Cell().Background(hb).PaddingVertical(5).PaddingHorizontal(5).Text("Service").FontSize(7.5f).Bold().FontColor("#ffffff");
                                    h.Cell().Background(hb).PaddingVertical(5).PaddingHorizontal(5).AlignRight().Text("Revenue").FontSize(7.5f).Bold().FontColor("#ffffff");
                                    h.Cell().Background(hb).PaddingVertical(5).PaddingHorizontal(5).AlignRight().Text("Txns").FontSize(7.5f).Bold().FontColor("#ffffff");
                                    h.Cell().Background(hb).PaddingVertical(5).PaddingHorizontal(5).AlignRight().Text("% Share").FontSize(7.5f).Bold().FontColor("#ffffff");
                                });
                                var si = 0;
                                foreach (var s in serviceBreakdown)
                                {
                                    var bg = si++ % 2 == 0 ? "#ffffff" : "#f0fdf4";
                                    t.Cell().Background(bg).PaddingVertical(4).PaddingHorizontal(5).Text(s.Service).FontSize(7.5f);
                                    t.Cell().Background(bg).PaddingVertical(4).PaddingHorizontal(5).AlignRight().Text(Amt(s.Revenue)).FontSize(7.5f).SemiBold();
                                    t.Cell().Background(bg).PaddingVertical(4).PaddingHorizontal(5).AlignRight().Text(s.Count.ToString()).FontSize(7.5f);
                                    t.Cell().Background(bg).PaddingVertical(4).PaddingHorizontal(5).AlignRight().Text(totalSales > 0 ? $"{s.Revenue / totalSales * 100:F1}%" : "0%").FontSize(7.5f);
                                }
                            });
                        });
                        row.ConstantItem(12);
                        row.RelativeItem(3).Column(c =>
                        {
                            c.Item().Text("Payment Mode").FontSize(9.5f).Bold().FontColor("#111827");
                            c.Item().PaddingTop(4).Table(t =>
                            {
                                t.ColumnsDefinition(cols => { cols.RelativeColumn(2); cols.RelativeColumn(2); cols.RelativeColumn(1); });
                                t.Header(h =>
                                {
                                    const string hb = "#7c3aed";
                                    h.Cell().Background(hb).PaddingVertical(5).PaddingHorizontal(5).Text("Mode").FontSize(7.5f).Bold().FontColor("#ffffff");
                                    h.Cell().Background(hb).PaddingVertical(5).PaddingHorizontal(5).AlignRight().Text("Amount").FontSize(7.5f).Bold().FontColor("#ffffff");
                                    h.Cell().Background(hb).PaddingVertical(5).PaddingHorizontal(5).AlignRight().Text("%").FontSize(7.5f).Bold().FontColor("#ffffff");
                                });
                                var pi = 0;
                                foreach (var p in paymentBreakdown)
                                {
                                    var bg = pi++ % 2 == 0 ? "#ffffff" : "#f5f3ff";
                                    t.Cell().Background(bg).PaddingVertical(4).PaddingHorizontal(5).Text(p.Mode).FontSize(7.5f);
                                    t.Cell().Background(bg).PaddingVertical(4).PaddingHorizontal(5).AlignRight().Text(Amt(p.Amount)).FontSize(7.5f).SemiBold();
                                    t.Cell().Background(bg).PaddingVertical(4).PaddingHorizontal(5).AlignRight().Text(totalSales > 0 ? $"{p.Amount / totalSales * 100:F1}%" : "0%").FontSize(7.5f);
                                }
                            });
                        });
                        row.ConstantItem(12);
                        row.RelativeItem(3).Column(c =>
                        {
                            c.Item().Text("Financial Summary").FontSize(9.5f).Bold().FontColor("#111827");
                            c.Item().PaddingTop(4).Table(t =>
                            {
                                t.ColumnsDefinition(cols => { cols.RelativeColumn(3); cols.RelativeColumn(2); });
                                t.Header(h =>
                                {
                                    const string hb = "#dc2626";
                                    h.Cell().Background(hb).PaddingVertical(5).PaddingHorizontal(5).Text("Category").FontSize(7.5f).Bold().FontColor("#ffffff");
                                    h.Cell().Background(hb).PaddingVertical(5).PaddingHorizontal(5).AlignRight().Text("Amount").FontSize(7.5f).Bold().FontColor("#ffffff");
                                });
                                var fRows = new (string Lbl, decimal Val, bool Red)[]
                                {
                                    ("Total Revenue",    totalSales,    false),
                                    ("Total Expenses",   totalExpenses, false),
                                    ("Salaries Paid",    totalSalaries, false),
                                    ("Pending (Unpaid)", totalPending,  true),
                                    ("Net Income",       netIncome,     netIncome < 0),
                                };
                                var fi = 0;
                                foreach (var (lbl, val, red) in fRows)
                                {
                                    var bg = fi++ % 2 == 0 ? "#ffffff" : "#fef2f2";
                                    t.Cell().Background(bg).PaddingVertical(4).PaddingHorizontal(5).Text(lbl).FontSize(7.5f);
                                    t.Cell().Background(bg).PaddingVertical(4).PaddingHorizontal(5).AlignRight().Text(Amt(val)).FontSize(7.5f).SemiBold().FontColor(red ? "#dc2626" : "#111827");
                                }
                            });
                        });
                    });

                    if (insights != null)
                    {
                        col.Item().PaddingTop(12).LineHorizontal(0.5f).LineColor("#e5e7eb");
                        col.Item().PaddingTop(8).Text("AI Business Insights").FontSize(9.5f).Bold().FontColor("#1a56db");
                        if (!string.IsNullOrWhiteSpace(insights.AiSummary))
                            col.Item().PaddingTop(5).Background("#eff6ff").Border(0.5f).BorderColor("#bfdbfe").Padding(10).Text(insights.AiSummary).FontSize(8f).FontColor("#1e3a8a");
                        var iList = insights.AiInsights.ToList();
                        for (var k = 0; k < iList.Count; k += 2)
                        {
                            var first  = iList[k];
                            var second = k + 1 < iList.Count ? iList[k + 1] : null;
                            col.Item().PaddingTop(6).Row(row =>
                            {
                                void Card(RowDescriptor r, AiInsightItem item)
                                {
                                    var (bg, border, fg) = item.Type switch
                                    {
                                        "positive" => ("#f0fdf4", "#86efac", "#166534"),
                                        "negative" => ("#fef2f2", "#fca5a5", "#7f1d1d"),
                                        _          => ("#f9fafb", "#d1d5db", "#374151"),
                                    };
                                    r.RelativeItem().Background(bg).Border(0.5f).BorderColor(border).Padding(8).Column(c =>
                                    {
                                        c.Item().Text(item.Title).FontSize(8).Bold().FontColor(fg);
                                        c.Item().PaddingTop(3).Text(item.Description).FontSize(7.5f).FontColor(fg);
                                    });
                                }
                                Card(row, first);
                                row.ConstantItem(8);
                                if (second != null) Card(row, second); else row.RelativeItem();
                            });
                        }
                        if (insights.AiRecommendations.Any())
                        {
                            col.Item().PaddingTop(8).Text("Recommendations").FontSize(9).SemiBold().FontColor("#111827");
                            foreach (var rec in insights.AiRecommendations)
                                col.Item().PaddingTop(3).PaddingLeft(10).Text($"\u2022 {rec}").FontSize(8).FontColor("#374151");
                        }
                        if (insights.AiAlerts.Any())
                        {
                            col.Item().PaddingTop(6).Text("\u26A0 Alerts").FontSize(9).SemiBold().FontColor("#dc2626");
                            foreach (var alert in insights.AiAlerts)
                                col.Item().PaddingTop(3).PaddingLeft(10).Text($"\u2022 {alert}").FontSize(8).FontColor("#dc2626");
                        }
                    }
                });

                page.Footer().PaddingTop(4).Row(row =>
                {
                    row.RelativeItem().Text($"{monthName} Report  |  {DateTime.Now:dd MMM yyyy}").FontSize(7).FontColor("#9ca3af");
                    row.ConstantItem(70).AlignRight().Text(x =>
                    {
                        x.Span("Page ").FontSize(7).FontColor("#9ca3af");
                        x.CurrentPageNumber().FontSize(7).FontColor("#9ca3af");
                        x.Span(" / ").FontSize(7).FontColor("#9ca3af");
                        x.TotalPages().FontSize(7).FontColor("#9ca3af");
                    });
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
