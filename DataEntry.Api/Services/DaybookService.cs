using DataEntry.Api.Data;
using DataEntry.Api.DTOs;
using DataEntry.Api.Models;
using Microsoft.EntityFrameworkCore;

namespace DataEntry.Api.Services;

public class DaybookService
{
    private readonly AppDbContext _db;

    public DaybookService(AppDbContext db)
    {
        _db = db;
    }

    public async Task<DaybookEntryDto> GetOrCreateAsync(int employeeId, DateOnly date)
    {
        var entry = await _db.DaybookEntries
            .Include(d => d.Employee)
            .Include(d => d.Sales).ThenInclude(s => s.Customer)
            .Include(d => d.Sales).ThenInclude(s => s.ServiceType)
            .Include(d => d.Expenses)
            .FirstOrDefaultAsync(d => d.EmployeeId == employeeId && d.Date == date);

        if (entry == null)
        {
            var openingBalance = await GetCarryForwardBalance(employeeId, date) ?? 0m;
            entry = new DaybookEntry
            {
                EmployeeId = employeeId,
                Date = date,
                OpeningBalance = openingBalance
            };
            _db.DaybookEntries.Add(entry);
            await _db.SaveChangesAsync();

            if (entry.Id > 0)
            {
                // Reload with includes (normal path)
                entry = await _db.DaybookEntries
                    .Include(d => d.Employee)
                    .Include(d => d.Sales).ThenInclude(s => s.Customer)
                    .Include(d => d.Sales).ThenInclude(s => s.ServiceType)
                    .Include(d => d.Expenses)
                    .FirstAsync(d => d.Id == entry.Id);
            }
            else
            {
                // Explorer mode: save was skipped, populate Employee navigation property for DTO mapping
                entry.Employee = await _db.Employees.FindAsync(employeeId)
                    ?? new Employee { Id = employeeId, Name = "Explorer" };
            }
        }
        else
        {
            // Auto-correct the opening balance from raw transaction history so it can
            // never cascade-corrupt (does not rely on any stored intermediate balance).
            var today = DateOnly.FromDateTime(DateTime.Today);
            bool isCurrentMonth = entry.Date.Year == today.Year && entry.Date.Month == today.Month;

            if (isCurrentMonth)
            {
                var correctOpeningBalance = await GetCarryForwardBalance(employeeId, date);
                if (correctOpeningBalance.HasValue && entry.OpeningBalance != correctOpeningBalance.Value)
                {
                    entry.OpeningBalance = correctOpeningBalance.Value;
                    entry.UpdatedAt = DateTime.UtcNow;
                    await _db.SaveChangesAsync();
                }
            }
        }

        return MapToDto(entry, await GetVehicleVisitCounts(entry));
    }

    public async Task<DaybookEntryDto?> UpdateOpeningBalanceAsync(int daybookId, decimal openingBalance)
    {
        var entry = await LoadEntry(daybookId);
        if (entry == null || entry.IsFinalized) return null;

        entry.OpeningBalance = openingBalance;
        entry.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();
        return MapToDto(entry, await GetVehicleVisitCounts(entry));
    }

    public async Task<SaleTransactionDto?> AddSaleAsync(int daybookId, AddSaleRequest request, bool bypassFinalized = false)
    {
        var entry = await _db.DaybookEntries.FindAsync(daybookId);
        if (entry == null || (entry.IsFinalized && !bypassFinalized)) return null;

        var validModes = new[] { "Cash", "Card", "UPI", "Pending" };
        if (!validModes.Contains(request.PaymentMode)) return null;

        var sale = new SaleTransaction
        {
            DaybookEntryId = daybookId,
            CustomerId = request.CustomerId,
            ServiceTypeId = request.ServiceTypeId,
            VehicleNumber = request.VehicleNumber,
            VehicleType = request.VehicleType,
            Amount = request.Amount,
            PaymentMode = request.PaymentMode,
            Notes = request.Notes
        };

        _db.SaleTransactions.Add(sale);
        entry.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();

        if (sale.Id > 0)
        {
            // Reload with navigation (normal path)
            var loaded = await _db.SaleTransactions
                .Include(s => s.Customer)
                .Include(s => s.ServiceType)
                .FirstAsync(s => s.Id == sale.Id);
            return MapSaleToDto(loaded);
        }

        // Explorer mode: save was skipped, load navigations manually
        if (sale.CustomerId.HasValue)
            sale.Customer = await _db.Customers.FindAsync(sale.CustomerId.Value);
        sale.ServiceType = await _db.ServiceTypes.FindAsync(sale.ServiceTypeId)
            ?? new ServiceType { Id = sale.ServiceTypeId, Name = string.Empty };
        return MapSaleToDto(sale);
    }

    public async Task<bool> DeleteSaleAsync(int saleId, bool bypassFinalized = false)
    {
        var sale = await _db.SaleTransactions
            .Include(s => s.DaybookEntry)
            .FirstOrDefaultAsync(s => s.Id == saleId);

        if (sale == null || (sale.DaybookEntry.IsFinalized && !bypassFinalized)) return false;

        _db.SaleTransactions.Remove(sale);
        sale.DaybookEntry.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();
        return true;
    }

    public async Task<ExpenseDto?> AddExpenseAsync(int daybookId, AddExpenseRequest request, bool bypassFinalized = false)
    {
        var entry = await _db.DaybookEntries.FindAsync(daybookId);
        if (entry == null || (entry.IsFinalized && !bypassFinalized)) return null;

        var expense = new Expense
        {
            DaybookEntryId = daybookId,
            Description = request.Description,
            Amount = request.Amount
        };

        _db.Expenses.Add(expense);
        entry.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();

        return new ExpenseDto(expense.Id, expense.Description, expense.Amount, expense.CreatedAt);
    }

    public async Task<bool> DeleteExpenseAsync(int expenseId, bool bypassFinalized = false)
    {
        var expense = await _db.Expenses
            .Include(e => e.DaybookEntry)
            .FirstOrDefaultAsync(e => e.Id == expenseId);

        if (expense == null || (expense.DaybookEntry.IsFinalized && !bypassFinalized)) return false;

        _db.Expenses.Remove(expense);
        expense.DaybookEntry.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();
        return true;
    }

    /// <summary>
    /// Walks every admin entry for the given month in date order, re-derives each
    /// entry's opening balance from the previous day's actual closing, and saves
    /// corrections.  The very first admin entry of the month is the anchor — its
    /// stored opening balance is never changed.
    /// </summary>
    public async Task<object> RepairMonthChainAsync(int year, int month)
    {
        var monthStart = new DateOnly(year, month, 1);
        var monthEnd   = new DateOnly(year, month, DateTime.DaysInMonth(year, month));

        // All admin entries for this month, in date order.
        var adminEntries = await _db.DaybookEntries
            .Include(d => d.Employee)
            .Include(d => d.Sales)
            .Include(d => d.Expenses)
            .Where(d => d.Date >= monthStart && d.Date <= monthEnd && d.Employee.Role == "Admin")
            .OrderBy(d => d.Date)
            .ToListAsync();

        if (!adminEntries.Any())
            return new { message = "No admin entries found for this month.", corrections = 0, days = new List<object>() };

        // All employee entries for the month (for combined net per day).
        var allEntriesForMonth = await _db.DaybookEntries
            .Include(d => d.Sales)
            .Include(d => d.Expenses)
            .Where(d => d.Date >= monthStart && d.Date <= monthEnd)
            .ToListAsync();

        var entriesByDate = allEntriesForMonth
            .GroupBy(e => e.Date)
            .ToDictionary(g => g.Key, g => g.ToList());

        int corrections = 0;

        // The first admin entry is the anchor — its stored opening is never changed.
        decimal anchor = adminEntries[0].OpeningBalance;

        // For every admin entry after the first, compute opening balance directly as:
        //   anchor + Σ(net of every day from month-start up to but not including this day)
        // This reads raw transaction records only — no stored balances trusted.
        for (int i = 1; i < adminEntries.Count; i++)
        {
            var adminEntry = adminEntries[i];

            // Sum net for all days strictly before this entry's date.
            var netBeforeThisDay = allEntriesForMonth
                .Where(e => e.Date < adminEntry.Date)
                .Sum(e => e.TotalSales - e.TotalExpenses);

            var correctOpening = anchor + netBeforeThisDay;

            if (adminEntry.OpeningBalance != correctOpening)
            {
                adminEntry.OpeningBalance = correctOpening;
                adminEntry.UpdatedAt = DateTime.UtcNow;
                corrections++;
            }
        }

        if (corrections > 0)
            await _db.SaveChangesAsync();

        return new
        {
            message = $"Repaired {corrections} entr{(corrections == 1 ? "y" : "ies")} for {year}-{month:00}.",
            corrections,
            days = adminEntries.Select(e => new
            {
                date = e.Date.ToString("yyyy-MM-dd"),
                openingBalance = e.OpeningBalance,
                totalSales = entriesByDate.TryGetValue(e.Date, out var de) ? de.Sum(x => x.TotalSales) : 0,
                totalExpenses = entriesByDate.TryGetValue(e.Date, out var de2) ? de2.Sum(x => x.TotalExpenses) : 0,
                closingBalance = e.OpeningBalance + (entriesByDate.TryGetValue(e.Date, out var de3) ? de3.Sum(x => x.TotalSales) - de3.Sum(x => x.TotalExpenses) : 0)
            }).ToList()
        };
    }

    public async Task<DaybookEntryDto?> FinalizeAsync(int daybookId)
    {
        var entry = await LoadEntry(daybookId);
        if (entry == null) return null;

        entry.IsFinalized = true;
        entry.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();
        return MapToDto(entry, await GetVehicleVisitCounts(entry));
    }

    private async Task<decimal?> GetCarryForwardBalance(int employeeId, DateOnly date)
    {
        // Only admin entries carry the shop's combined cash balance forward.
        var employee = await _db.Employees.FindAsync(employeeId);
        if (employee == null || employee.Role != "Admin")
            return null;

        var monthStart = new DateOnly(date.Year, date.Month, 1);

        // Find the first admin entry of this month — its stored opening is the anchor
        // (manually set by the admin, or 0 by default). This is the only stored value
        // we trust; everything else is derived from raw sales/expense records.
        var firstAdminEntry = await _db.DaybookEntries
            .Include(d => d.Employee)
            .Where(d => d.Date >= monthStart && d.Date < date && d.Employee.Role == "Admin")
            .OrderBy(d => d.Date)
            .FirstOrDefaultAsync();

        // No same-month admin entry before this date — this is the first day of the month.
        // Return null so the caller preserves whatever the admin set manually as the anchor.
        if (firstAdminEntry == null) return null;

        // Different month entirely — no cross-month carry-over.
        if (firstAdminEntry.Date.Month != date.Month || firstAdminEntry.Date.Year != date.Year)
            return null;

        // Sum ALL employees' sales and expenses for every day from monthStart up to
        // (but not including) the requested date. This is computed purely from raw
        // transaction records — never from any stored opening/closing balance —
        // so it cannot cascade-corrupt regardless of what is stored in prior entries.
        var allPreviousEntries = await _db.DaybookEntries
            .Include(d => d.Sales)
            .Include(d => d.Expenses)
            .Where(d => d.Date >= monthStart && d.Date < date)
            .ToListAsync();

        var totalPreviousNet = allPreviousEntries.Sum(e => e.TotalSales - e.TotalExpenses);

        return firstAdminEntry.OpeningBalance + totalPreviousNet;
    }

    private async Task<Dictionary<string, int>> GetVehicleVisitCounts(DaybookEntry entry)
    {
        var vehicleNumbers = entry.Sales
            .Where(s => !string.IsNullOrWhiteSpace(s.VehicleNumber))
            .Select(s => s.VehicleNumber!.Trim().ToUpper())
            .Distinct()
            .ToList();

        if (!vehicleNumbers.Any()) return new Dictionary<string, int>();

        var counts = await _db.SaleTransactions
            .Where(s => s.VehicleNumber != null && vehicleNumbers.Contains(s.VehicleNumber.Trim().ToUpper()))
            .Join(_db.DaybookEntries, s => s.DaybookEntryId, d => d.Id, (s, d) => new { VehicleNumber = s.VehicleNumber!.Trim().ToUpper(), d.Date })
            .GroupBy(x => x.VehicleNumber)
            .Select(g => new { Vehicle = g.Key, Count = g.Select(x => x.Date).Distinct().Count() })
            .ToDictionaryAsync(x => x.Vehicle, x => x.Count);

        return counts;
    }

    private async Task<DaybookEntry?> LoadEntry(int daybookId)
    {
        return await _db.DaybookEntries
            .Include(d => d.Employee)
            .Include(d => d.Sales).ThenInclude(s => s.Customer)
            .Include(d => d.Sales).ThenInclude(s => s.ServiceType)
            .Include(d => d.Expenses)
            .FirstOrDefaultAsync(d => d.Id == daybookId);
    }

    public static DaybookEntryDto MapToDto(DaybookEntry entry, Dictionary<string, int>? vehicleVisitCounts = null)
    {
        return new DaybookEntryDto(
            entry.Id,
            entry.EmployeeId,
            entry.Employee.Name,
            entry.Date,
            entry.OpeningBalance,
            entry.TotalSales,
            entry.TotalCashCollected,
            entry.TotalCardCollected,
            entry.TotalUpiCollected,
            entry.TotalPendingCollected,
            entry.TotalExpenses,
            entry.ClosingBalance,
            entry.Notes,
            entry.IsFinalized,
            entry.Sales.Select(s => MapSaleToDto(s, GetVisitCount(s.VehicleNumber, vehicleVisitCounts))).ToList(),
            entry.Expenses.Select(e => new ExpenseDto(e.Id, e.Description, e.Amount, e.CreatedAt)).ToList()
        );
    }

    private static int GetVisitCount(string? vehicleNumber, Dictionary<string, int>? counts)
    {
        if (string.IsNullOrWhiteSpace(vehicleNumber) || counts == null) return 0;
        return counts.TryGetValue(vehicleNumber.Trim().ToUpper(), out var count) ? count : 0;
    }

    private static SaleTransactionDto MapSaleToDto(SaleTransaction s, int visitCount = 0)
    {
        return new SaleTransactionDto(
            s.Id, s.CustomerId, s.Customer?.Name, s.ServiceTypeId,
            s.ServiceType?.Name ?? "", s.VehicleNumber, s.VehicleType,
            s.Amount, s.PaymentMode, s.Notes, s.CreatedAt, visitCount
        );
    }

    public async Task<SaleTransactionDto?> UpdateSaleAsync(int saleId, UpdateSaleRequest request, bool bypassFinalized = false)
    {
        var sale = await _db.SaleTransactions
            .Include(s => s.DaybookEntry)
            .Include(s => s.Customer)
            .Include(s => s.ServiceType)
            .FirstOrDefaultAsync(s => s.Id == saleId);

        if (sale == null || (sale.DaybookEntry.IsFinalized && !bypassFinalized)) return null;

        var validModes = new[] { "Cash", "Card", "UPI", "Pending" };
        if (request.PaymentMode != null && !validModes.Contains(request.PaymentMode)) return null;

        if (request.CustomerId.HasValue) sale.CustomerId = request.CustomerId == 0 ? null : request.CustomerId;
        if (request.ServiceTypeId.HasValue) sale.ServiceTypeId = request.ServiceTypeId.Value;
        if (request.VehicleNumber != null) sale.VehicleNumber = string.IsNullOrEmpty(request.VehicleNumber) ? null : request.VehicleNumber;
        if (request.VehicleType != null) sale.VehicleType = string.IsNullOrEmpty(request.VehicleType) ? null : request.VehicleType;
        if (request.Amount.HasValue) sale.Amount = request.Amount.Value;
        if (request.PaymentMode != null) sale.PaymentMode = request.PaymentMode;
        if (request.Notes != null) sale.Notes = string.IsNullOrEmpty(request.Notes) ? null : request.Notes;

        sale.DaybookEntry.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();

        // Reload with navigation
        var loaded = await _db.SaleTransactions
            .Include(s => s.Customer)
            .Include(s => s.ServiceType)
            .FirstAsync(s => s.Id == saleId);

        return MapSaleToDto(loaded);
    }

    public async Task<DailyCombinedSalesDto> GetAllSalesForDateAsync(DateOnly date)
    {
        var entries = await _db.DaybookEntries
            .Include(d => d.Employee)
            .Include(d => d.Sales).ThenInclude(s => s.Customer)
            .Include(d => d.Sales).ThenInclude(s => s.ServiceType)
            .Include(d => d.Expenses)
            .Where(d => d.Date == date)
            .ToListAsync();

        var allSalesEntries = entries.SelectMany(e => e.Sales).ToList();
        var vehicleNumbers = allSalesEntries
            .Where(s => !string.IsNullOrWhiteSpace(s.VehicleNumber))
            .Select(s => s.VehicleNumber!.Trim().ToUpper())
            .Distinct()
            .ToList();

        var vehicleVisitCounts = new Dictionary<string, int>();
        if (vehicleNumbers.Any())
        {
            vehicleVisitCounts = await _db.SaleTransactions
                .Where(s => s.VehicleNumber != null && vehicleNumbers.Contains(s.VehicleNumber.Trim().ToUpper()))
                .Join(_db.DaybookEntries, s => s.DaybookEntryId, d => d.Id, (s, d) => new { VehicleNumber = s.VehicleNumber!.Trim().ToUpper(), d.Date })
                .GroupBy(x => x.VehicleNumber)
                .Select(g => new { Vehicle = g.Key, Count = g.Select(x => x.Date).Distinct().Count() })
                .ToDictionaryAsync(x => x.Vehicle, x => x.Count);
        }

        var sales = entries
            .SelectMany(e => e.Sales.Select(s => new SaleWithEmployeeDto(
                s.Id, e.EmployeeId, e.Employee.Name,
                s.CustomerId, s.Customer?.Name,
                s.ServiceTypeId, s.ServiceType?.Name ?? "",
                s.VehicleNumber, s.VehicleType,
                s.Amount, s.PaymentMode, s.Notes, s.CreatedAt,
                GetVisitCount(s.VehicleNumber, vehicleVisitCounts)
            )))
            .OrderBy(s => s.CreatedAt)
            .ToList();

        // Combined closing balance: use the admin's opening balance (employees always have 0)
        var combinedOpening = entries.Any()
            ? entries.Where(e => e.Employee.Role == "Admin").Select(e => e.OpeningBalance).FirstOrDefault()
            : 0;
        var combinedExpenses = entries.Sum(e => e.Expenses.Sum(ex => ex.Amount));
        var totalSalesAmount = sales.Sum(s => s.Amount);
        var combinedClosing = combinedOpening + totalSalesAmount - combinedExpenses;
        var isFinalized = entries.Any() && entries.Any(e => e.IsFinalized);

        return new DailyCombinedSalesDto(
            date, sales,
            totalSalesAmount,
            sales.Where(s => s.PaymentMode == "Cash").Sum(s => s.Amount),
            sales.Where(s => s.PaymentMode == "Card").Sum(s => s.Amount),
            sales.Where(s => s.PaymentMode == "UPI").Sum(s => s.Amount),
            sales.Where(s => s.PaymentMode == "Pending").Sum(s => s.Amount),
            sales.Count,
            combinedOpening,
            combinedExpenses,
            combinedClosing,
            isFinalized
        );
    }
}
