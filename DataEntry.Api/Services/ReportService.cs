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

                    // ── Daily table rendered as explicit Rows (Row+RelativeItem is proven stable) ──
                    // Column weights: Date=2, Sales=3, Cash=2, Card=2, UPI=2, Pending=2, Expenses=2, #=1  total=16
                    col.Item().PaddingTop(4).Row(r =>
                    {
                        const string hb = "#1a56db";
                        r.RelativeItem(2).Background(hb).PaddingVertical(5).PaddingHorizontal(4).Text("Date").FontSize(8).Bold().FontColor("#ffffff");
                        r.RelativeItem(3).Background(hb).PaddingVertical(5).PaddingHorizontal(4).AlignRight().Text("Sales").FontSize(8).Bold().FontColor("#ffffff");
                        r.RelativeItem(2).Background(hb).PaddingVertical(5).PaddingHorizontal(4).AlignRight().Text("Cash").FontSize(8).Bold().FontColor("#ffffff");
                        r.RelativeItem(2).Background(hb).PaddingVertical(5).PaddingHorizontal(4).AlignRight().Text("Card").FontSize(8).Bold().FontColor("#ffffff");
                        r.RelativeItem(2).Background(hb).PaddingVertical(5).PaddingHorizontal(4).AlignRight().Text("UPI").FontSize(8).Bold().FontColor("#ffffff");
                        r.RelativeItem(2).Background(hb).PaddingVertical(5).PaddingHorizontal(4).AlignRight().Text("Pending").FontSize(8).Bold().FontColor("#ffffff");
                        r.RelativeItem(2).Background(hb).PaddingVertical(5).PaddingHorizontal(4).AlignRight().Text("Expenses").FontSize(8).Bold().FontColor("#ffffff");
                        r.RelativeItem(1).Background(hb).PaddingVertical(5).PaddingHorizontal(4).AlignRight().Text("#").FontSize(8).Bold().FontColor("#ffffff");
                    });

                    var rowIdx = 0;
                    foreach (var d in dailyTotals)
                    {
                        var bg = rowIdx++ % 2 == 0 ? "#ffffff" : "#f9fafb";
                        var pending = d.Pending;
                        col.Item().Row(r =>
                        {
                            r.RelativeItem(2).Background(bg).PaddingVertical(4).PaddingHorizontal(4).Text(d.Date.ToString("dd MMM")).FontSize(7.5f);
                            r.RelativeItem(3).Background(bg).PaddingVertical(4).PaddingHorizontal(4).AlignRight().Text(Amt(d.Sales)).FontSize(7.5f).SemiBold();
                            r.RelativeItem(2).Background(bg).PaddingVertical(4).PaddingHorizontal(4).AlignRight().Text(Amt(d.Cash)).FontSize(7.5f);
                            r.RelativeItem(2).Background(bg).PaddingVertical(4).PaddingHorizontal(4).AlignRight().Text(Amt(d.Card)).FontSize(7.5f);
                            r.RelativeItem(2).Background(bg).PaddingVertical(4).PaddingHorizontal(4).AlignRight().Text(Amt(d.Upi)).FontSize(7.5f);
                            r.RelativeItem(2).Background(bg).PaddingVertical(4).PaddingHorizontal(4).AlignRight()
                                .Text(pending > 0 ? Amt(pending) : "-").FontSize(7.5f).FontColor(pending > 0 ? "#dc2626" : "#9ca3af");
                            r.RelativeItem(2).Background(bg).PaddingVertical(4).PaddingHorizontal(4).AlignRight().Text(Amt(d.Expenses)).FontSize(7.5f);
                            r.RelativeItem(1).Background(bg).PaddingVertical(4).PaddingHorizontal(4).AlignRight().Text(d.Txns.ToString()).FontSize(7.5f);
                        });
                    }

                    col.Item().Row(r =>
                    {
                        const string tb = "#dbeafe";
                        r.RelativeItem(2).Background(tb).PaddingVertical(5).PaddingHorizontal(4).Text("TOTAL").FontSize(7.5f).Bold();
                        r.RelativeItem(3).Background(tb).PaddingVertical(5).PaddingHorizontal(4).AlignRight().Text(Amt(totalSales)).FontSize(7.5f).Bold();
                        r.RelativeItem(2).Background(tb).PaddingVertical(5).PaddingHorizontal(4).AlignRight().Text(Amt(totalCash)).FontSize(7.5f).Bold();
                        r.RelativeItem(2).Background(tb).PaddingVertical(5).PaddingHorizontal(4).AlignRight().Text(Amt(totalCard)).FontSize(7.5f).Bold();
                        r.RelativeItem(2).Background(tb).PaddingVertical(5).PaddingHorizontal(4).AlignRight().Text(Amt(totalUpi)).FontSize(7.5f).Bold();
                        r.RelativeItem(2).Background(tb).PaddingVertical(5).PaddingHorizontal(4).AlignRight()
                            .Text(totalPending > 0 ? Amt(totalPending) : "-").FontSize(7.5f).Bold().FontColor(totalPending > 0 ? "#dc2626" : "#9ca3af");
                        r.RelativeItem(2).Background(tb).PaddingVertical(5).PaddingHorizontal(4).AlignRight().Text(Amt(totalExpenses)).FontSize(7.5f).Bold();
                        r.RelativeItem(1).Background(tb).PaddingVertical(5).PaddingHorizontal(4).AlignRight().Text(dailyTotals.Sum(d => d.Txns).ToString()).FontSize(7.5f).Bold();
                    });

                    col.Item().PaddingTop(12).Row(row =>
                    {
                        // ── Service-wise Revenue (Row-based, weights 4:3:1:2) ──
                        row.RelativeItem(5).Column(c =>
                        {
                            c.Item().Text("Service-wise Revenue").FontSize(9.5f).Bold().FontColor("#111827");
                            c.Item().PaddingTop(4).Row(hr =>
                            {
                                const string hb = "#059669";
                                hr.RelativeItem(4).Background(hb).PaddingVertical(5).PaddingHorizontal(5).Text("Service").FontSize(7.5f).Bold().FontColor("#ffffff");
                                hr.RelativeItem(3).Background(hb).PaddingVertical(5).PaddingHorizontal(5).AlignRight().Text("Revenue").FontSize(7.5f).Bold().FontColor("#ffffff");
                                hr.RelativeItem(1).Background(hb).PaddingVertical(5).PaddingHorizontal(5).AlignRight().Text("Txns").FontSize(7.5f).Bold().FontColor("#ffffff");
                                hr.RelativeItem(2).Background(hb).PaddingVertical(5).PaddingHorizontal(5).AlignRight().Text("% Share").FontSize(7.5f).Bold().FontColor("#ffffff");
                            });
                            var si = 0;
                            foreach (var s in serviceBreakdown)
                            {
                                var svc = s;
                                var bg = si++ % 2 == 0 ? "#ffffff" : "#f0fdf4";
                                c.Item().Row(dr =>
                                {
                                    dr.RelativeItem(4).Background(bg).PaddingVertical(4).PaddingHorizontal(5).Text(svc.Service).FontSize(7.5f);
                                    dr.RelativeItem(3).Background(bg).PaddingVertical(4).PaddingHorizontal(5).AlignRight().Text(Amt(svc.Revenue)).FontSize(7.5f).SemiBold();
                                    dr.RelativeItem(1).Background(bg).PaddingVertical(4).PaddingHorizontal(5).AlignRight().Text(svc.Count.ToString()).FontSize(7.5f);
                                    dr.RelativeItem(2).Background(bg).PaddingVertical(4).PaddingHorizontal(5).AlignRight().Text(totalSales > 0 ? $"{svc.Revenue / totalSales * 100:F1}%" : "0%").FontSize(7.5f);
                                });
                            }
                        });

                        row.ConstantItem(12);

                        // ── Payment Mode (Row-based, weights 2:2:1) ──
                        row.RelativeItem(3).Column(c =>
                        {
                            c.Item().Text("Payment Mode").FontSize(9.5f).Bold().FontColor("#111827");
                            c.Item().PaddingTop(4).Row(hr =>
                            {
                                const string hb = "#7c3aed";
                                hr.RelativeItem(2).Background(hb).PaddingVertical(5).PaddingHorizontal(5).Text("Mode").FontSize(7.5f).Bold().FontColor("#ffffff");
                                hr.RelativeItem(2).Background(hb).PaddingVertical(5).PaddingHorizontal(5).AlignRight().Text("Amount").FontSize(7.5f).Bold().FontColor("#ffffff");
                                hr.RelativeItem(1).Background(hb).PaddingVertical(5).PaddingHorizontal(5).AlignRight().Text("%").FontSize(7.5f).Bold().FontColor("#ffffff");
                            });
                            var pi = 0;
                            foreach (var p in paymentBreakdown)
                            {
                                var pm = p;
                                var bg = pi++ % 2 == 0 ? "#ffffff" : "#f5f3ff";
                                c.Item().Row(dr =>
                                {
                                    dr.RelativeItem(2).Background(bg).PaddingVertical(4).PaddingHorizontal(5).Text(pm.Mode).FontSize(7.5f);
                                    dr.RelativeItem(2).Background(bg).PaddingVertical(4).PaddingHorizontal(5).AlignRight().Text(Amt(pm.Amount)).FontSize(7.5f).SemiBold();
                                    dr.RelativeItem(1).Background(bg).PaddingVertical(4).PaddingHorizontal(5).AlignRight().Text(totalSales > 0 ? $"{pm.Amount / totalSales * 100:F1}%" : "0%").FontSize(7.5f);
                                });
                            }
                        });

                        row.ConstantItem(12);

                        // ── Financial Summary (Row-based, weights 3:2) ──
                        row.RelativeItem(3).Column(c =>
                        {
                            c.Item().Text("Financial Summary").FontSize(9.5f).Bold().FontColor("#111827");
                            c.Item().PaddingTop(4).Row(hr =>
                            {
                                const string hb = "#dc2626";
                                hr.RelativeItem(3).Background(hb).PaddingVertical(5).PaddingHorizontal(5).Text("Category").FontSize(7.5f).Bold().FontColor("#ffffff");
                                hr.RelativeItem(2).Background(hb).PaddingVertical(5).PaddingHorizontal(5).AlignRight().Text("Amount").FontSize(7.5f).Bold().FontColor("#ffffff");
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
                                var label = lbl; var value = val; var isRed = red;
                                var bg = fi++ % 2 == 0 ? "#ffffff" : "#fef2f2";
                                c.Item().Row(dr =>
                                {
                                    dr.RelativeItem(3).Background(bg).PaddingVertical(4).PaddingHorizontal(5).Text(label).FontSize(7.5f);
                                    dr.RelativeItem(2).Background(bg).PaddingVertical(4).PaddingHorizontal(5).AlignRight().Text(Amt(value)).FontSize(7.5f).SemiBold().FontColor(isRed ? "#dc2626" : "#111827");
                                });
                            }
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

    // ── HTML Report ─────────────────────────────────────────────────────────
    public async Task<string> GenerateMonthlyHtmlAsync(int year, int month, BusinessInsightsDto? insights = null)
    {
        var startDate  = new DateOnly(year, month, 1);
        var endDate    = startDate.AddMonths(1).AddDays(-1);
        var monthName  = new DateTime(year, month, 1).ToString("MMMM yyyy");

        var entries = await _db.DaybookEntries
            .Include(d => d.Employee)
            .Include(d => d.Sales).ThenInclude(s => s.ServiceType)
            .Include(d => d.Expenses)
            .Where(d => d.Date >= startDate && d.Date <= endDate)
            .OrderBy(d => d.Date)
            .ToListAsync();

        var salaries = await _db.SalaryPayments
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

        static string Rs(decimal v) => $"&#x20B9;{v:N0}";
        static string H(string s)   => System.Web.HttpUtility.HtmlEncode(s);

        var sb = new StringBuilder();
        sb.Append(@"<!DOCTYPE html>
<html lang=""en"">
<head>
<meta charset=""UTF-8""/>
<meta name=""viewport"" content=""width=device-width,initial-scale=1""/>
<title>Monthly Business Report</title>
<style>
*{box-sizing:border-box;margin:0;padding:0}
body{font-family:-apple-system,BlinkMacSystemFont,'Segoe UI',Arial,sans-serif;font-size:13px;color:#111827;background:#fff;padding:24px}
h1{font-size:22px;font-weight:700;color:#1a56db}
h2{font-size:14px;font-weight:700;color:#111827;margin:20px 0 8px}
.sub{font-size:12px;color:#6b7280;margin-top:2px}
.meta{font-size:11px;color:#9ca3af;text-align:right}
hr{border:none;border-top:2px solid #1a56db;margin:10px 0 18px}
/* KPI grid */
.kpi{display:grid;grid-template-columns:repeat(6,1fr);gap:8px;margin-bottom:20px}
.kpi-card{border-radius:8px;padding:10px 12px;border:1px solid #e5e7eb}
.kpi-card .lbl{font-size:11px;font-weight:600;margin-bottom:4px}
.kpi-card .val{font-size:18px;font-weight:700}
/* Tables */
table{width:100%;border-collapse:collapse;margin-bottom:4px}
th{padding:7px 10px;font-size:12px;font-weight:700;text-align:left;white-space:nowrap}
th.r,td.r{text-align:right}
td{padding:6px 10px;font-size:12px;border-bottom:1px solid #f3f4f6;white-space:nowrap}
tr:nth-child(even) td{background:#f9fafb}
.tfoot td{font-weight:700;background:#dbeafe!important}
/* Section grid */
.sections{display:grid;grid-template-columns:5fr 3fr 3fr;gap:16px;margin-top:4px}
/* AI */
.ai-summary{background:#eff6ff;border:1px solid #bfdbfe;border-radius:8px;padding:12px;font-size:13px;color:#1e3a8a;margin-bottom:12px}
.insights{display:grid;grid-template-columns:repeat(2,1fr);gap:10px;margin-bottom:12px}
.insight{border-radius:8px;padding:10px 12px;border:1px solid}
.insight .title{font-size:12px;font-weight:700;margin-bottom:3px}
.insight .desc{font-size:11.5px}
.positive{background:#f0fdf4;border-color:#86efac;color:#166534}
.negative{background:#fef2f2;border-color:#fca5a5;color:#7f1d1d}
.neutral{background:#f9fafb;border-color:#d1d5db;color:#374151}
ul{padding-left:18px;margin:4px 0}
li{font-size:12px;margin-bottom:2px}
@media print{
  @page{size:A3 landscape;margin:15mm}
  body{padding:0}
  .no-print{display:none!important}
  h2{margin:12px 0 6px}
  .kpi{grid-template-columns:repeat(6,1fr)}
}
</style>
</head>
<body>
");

        // Header
        sb.Append($@"<div style=""display:flex;justify-content:space-between;align-items:flex-end"">
  <div>
    <h1>Monthly Business Report</h1>
    <div class=""sub"">{H(monthName)}</div>
  </div>
  <div class=""meta"">Generated {DateTime.Now:dd MMM yyyy HH:mm}
    <br/><button class=""no-print"" onclick=""window.print()"" style=""margin-top:6px;padding:6px 16px;background:#1a56db;color:#fff;border:none;border-radius:6px;cursor:pointer;font-size:12px"">&#128438; Save / Print PDF</button>
  </div>
</div>
<hr/>
");

        // KPI cards
        void KpiCard(string label, decimal value, string bg, string color)
            => sb.Append($@"<div class=""kpi-card"" style=""background:{bg}""><div class=""lbl"" style=""color:{color}"">{H(label)}</div><div class=""val"" style=""color:{color}"">{Rs(value)}</div></div>");
        void KpiCardN(string label, string value, string bg, string color)
            => sb.Append($@"<div class=""kpi-card"" style=""background:{bg}""><div class=""lbl"" style=""color:{color}"">{H(label)}</div><div class=""val"" style=""color:{color}"">{H(value)}</div></div>");

        sb.Append(@"<div class=""kpi"">");
        KpiCard("Total Revenue",      totalSales,    "#ecfdf5", "#065f46");
        KpiCard("Total Expenses",     totalExpenses, "#fef2f2", "#7f1d1d");
        KpiCard("Salaries Paid",      totalSalaries, "#fffbeb", "#78350f");
        KpiCard("Net Income",         netIncome,     netIncome >= 0 ? "#eff6ff" : "#fef2f2", netIncome >= 0 ? "#1e3a8a" : "#7f1d1d");
        KpiCard("Pending (Unpaid)",   totalPending,  "#fff1f2", "#881337");
        KpiCardN("Total Transactions", dailyTotals.Sum(d => d.Txns).ToString(), "#f5f3ff", "#4c1d95");
        sb.Append("</div>\n");

        // Daily breakdown table
        sb.Append(@"<h2>Daily Breakdown</h2>
<table>
<thead><tr style=""background:#1a56db;color:#fff"">
<th>Date</th><th class=""r"">Sales</th><th class=""r"">Cash</th><th class=""r"">Card</th><th class=""r"">UPI</th><th class=""r"">Pending</th><th class=""r"">Expenses</th><th class=""r"">#</th>
</tr></thead><tbody>
");
        foreach (var d in dailyTotals)
        {
            sb.Append($@"<tr>
<td>{d.Date:dd MMM}</td>
<td class=""r""><strong>{Rs(d.Sales)}</strong></td>
<td class=""r"">{Rs(d.Cash)}</td>
<td class=""r"">{Rs(d.Card)}</td>
<td class=""r"">{Rs(d.Upi)}</td>
<td class=""r"" style=""color:{(d.Pending > 0 ? "#dc2626" : "#9ca3af")}"">{(d.Pending > 0 ? Rs(d.Pending) : "-")}</td>
<td class=""r"">{Rs(d.Expenses)}</td>
<td class=""r"">{d.Txns}</td>
</tr>
");
        }
        sb.Append($@"</tbody>
<tfoot><tr class=""tfoot"">
<td>TOTAL</td>
<td class=""r"">{Rs(totalSales)}</td>
<td class=""r"">{Rs(totalCash)}</td>
<td class=""r"">{Rs(totalCard)}</td>
<td class=""r"">{Rs(totalUpi)}</td>
<td class=""r"" style=""color:{(totalPending > 0 ? "#dc2626" : "#9ca3af")}"">{(totalPending > 0 ? Rs(totalPending) : "-")}</td>
<td class=""r"">{Rs(totalExpenses)}</td>
<td class=""r"">{dailyTotals.Sum(d => d.Txns)}</td>
</tr></tfoot>
</table>
");

        // Three-column section
        sb.Append(@"<div class=""sections"">
");

        // Service-wise
        sb.Append(@"<div>
<h2>Service-wise Revenue</h2>
<table><thead><tr style=""background:#059669;color:#fff"">
<th>Service</th><th class=""r"">Revenue</th><th class=""r"">Txns</th><th class=""r"">% Share</th>
</tr></thead><tbody>
");
        foreach (var s in serviceBreakdown)
        {
            sb.Append($@"<tr>
<td>{H(s.Service)}</td>
<td class=""r""><strong>{Rs(s.Revenue)}</strong></td>
<td class=""r"">{s.Count}</td>
<td class=""r"">{(totalSales > 0 ? $"{s.Revenue / totalSales * 100:F1}%" : "0%")}</td>
</tr>
");
        }
        sb.Append("</tbody></table></div>\n");

        // Payment mode
        sb.Append(@"<div>
<h2>Payment Mode</h2>
<table><thead><tr style=""background:#7c3aed;color:#fff"">
<th>Mode</th><th class=""r"">Amount</th><th class=""r"">%</th>
</tr></thead><tbody>
");
        foreach (var p in paymentBreakdown)
        {
            sb.Append($@"<tr>
<td>{H(p.Mode)}</td>
<td class=""r""><strong>{Rs(p.Amount)}</strong></td>
<td class=""r"">{(totalSales > 0 ? $"{p.Amount / totalSales * 100:F1}%" : "0%")}</td>
</tr>
");
        }
        sb.Append("</tbody></table></div>\n");

        // Financial summary
        sb.Append(@"<div>
<h2>Financial Summary</h2>
<table><thead><tr style=""background:#dc2626;color:#fff"">
<th>Category</th><th class=""r"">Amount</th>
</tr></thead><tbody>
");
        var finRows = new (string Lbl, decimal Val, bool Red)[]
        {
            ("Total Revenue",    totalSales,    false),
            ("Total Expenses",   totalExpenses, false),
            ("Salaries Paid",    totalSalaries, false),
            ("Pending (Unpaid)", totalPending,  true),
            ("Net Income",       netIncome,     netIncome < 0),
        };
        foreach (var (lbl, val, red) in finRows)
            sb.Append($@"<tr><td>{H(lbl)}</td><td class=""r"" style=""color:{(red ? "#dc2626" : "#111827")}""><strong>{Rs(val)}</strong></td></tr>
");
        sb.Append("</tbody></table></div>\n");
        sb.Append("</div>\n"); // end .sections

        // AI Insights
        if (insights != null)
        {
            sb.Append(@"<hr style=""border-color:#e5e7eb;margin:20px 0 12px""/>
<h2 style=""color:#1a56db"">AI Business Insights</h2>
");
            if (!string.IsNullOrWhiteSpace(insights.AiSummary))
                sb.Append($@"<div class=""ai-summary"">{H(insights.AiSummary)}</div>
");

            if (insights.AiInsights.Any())
            {
                sb.Append(@"<div class=""insights"">");
                foreach (var item in insights.AiInsights)
                {
                    var cls = item.Type switch { "positive" => "positive", "negative" => "negative", _ => "neutral" };
                    sb.Append($@"<div class=""insight {cls}""><div class=""title"">{H(item.Title)}</div><div class=""desc"">{H(item.Description)}</div></div>
");
                }
                sb.Append("</div>\n");
            }

            if (insights.AiRecommendations.Any())
            {
                sb.Append("<h2>Recommendations</h2><ul>");
                foreach (var rec in insights.AiRecommendations)
                    sb.Append($"<li>{H(rec)}</li>");
                sb.Append("</ul>\n");
            }

            if (insights.AiAlerts.Any())
            {
                sb.Append(@"<h2 style=""color:#dc2626"">&#9888; Alerts</h2><ul>");
                foreach (var alert in insights.AiAlerts)
                    sb.Append($@"<li style=""color:#dc2626"">{H(alert)}</li>");
                sb.Append("</ul>\n");
            }
        }

        sb.Append("</body></html>");
        return sb.ToString();
    }
}
