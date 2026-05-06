using System.Text;
using DataEntry.Api.Data;
using DataEntry.Api.DTOs;
using DataEntry.Api.Services;
using Microsoft.EntityFrameworkCore;

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

    public async Task<byte[]> ExportCsvAsync(DateOnly from, DateOnly to)
    {
        var sales = await _db.SaleTransactions
            .Include(s => s.DaybookEntry).ThenInclude(d => d.Employee)
            .Include(s => s.Customer)
            .Include(s => s.ServiceType)
            .Where(s => s.DaybookEntry.Date >= from && s.DaybookEntry.Date <= to)
            .OrderBy(s => s.DaybookEntry.Date)
            .ToListAsync();

        var expenses = await _db.Expenses
            .Include(e => e.DaybookEntry).ThenInclude(d => d.Employee)
            .Where(e => e.DaybookEntry.Date >= from && e.DaybookEntry.Date <= to)
            .OrderBy(e => e.DaybookEntry.Date)
            .ToListAsync();

        var sb = new StringBuilder();
        sb.AppendLine("Type,Date,Employee,Customer,Service,Vehicle No,Vehicle Type,Amount,Payment Mode,Notes");

        foreach (var s in sales)
        {
            sb.AppendLine(string.Join(",", new[]
            {
                "Sale",
                s.DaybookEntry.Date.ToString("yyyy-MM-dd"),
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
                CsvEscape(e.DaybookEntry.Employee.Name),
                CsvEscape(e.Description),
                "", "", "",
                e.Amount.ToString("F2"),
                "", ""
            }));
        }

        return Encoding.UTF8.GetBytes(sb.ToString());
    }

    private static string CsvEscape(string value)
    {
        if (value.Contains(',') || value.Contains('"') || value.Contains('\n') || value.Contains('\r'))
            return $"\"{value.Replace("\"", "\"\"")}\"";
        return value;
    }
}
