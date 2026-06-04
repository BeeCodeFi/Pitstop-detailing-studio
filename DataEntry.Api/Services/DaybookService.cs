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
        else if (!entry.IsFinalized)
        {
            // Only recalculate opening balance for the current month's non-finalized entries.
            // Past month entries keep their stored opening balance — navigating back to a previous
            // month should show the accumulated values from that month, not reset them.
            var today = DateOnly.FromDateTime(DateTime.Today);
            bool isCurrentMonth = entry.Date.Year == today.Year && entry.Date.Month == today.Month;

            if (isCurrentMonth)
            {
                // Only update if a valid same-month carry-forward exists.
                // Returns null when no same-month previous entry is found, which means
                // there is no chain to carry forward from — preserve the existing balance.
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
        // Regular employees always start with 0 opening balance.
        var employee = await _db.Employees.FindAsync(employeeId);
        if (employee == null || employee.Role != "Admin")
            return null;

        // Find the admin's most recent entry before the requested date.
        // We look for the ADMIN's own entry specifically — not just any employee entry —
        // so that the accumulated opening balance is never lost across date gaps.
        var previousAdminEntry = await _db.DaybookEntries
            .Include(d => d.Employee)
            .Include(d => d.Sales)
            .Include(d => d.Expenses)
            .Where(d => d.Date < date && d.Employee.Role == "Admin")
            .OrderByDescending(d => d.Date)
            .FirstOrDefaultAsync();

        // No previous entry at all — nothing to carry forward.
        if (previousAdminEntry == null) return null;

        // No same-month previous entry — month boundary or gap in days.
        // Return null so the caller preserves the existing opening balance rather
        // than overwriting it with 0.
        if (previousAdminEntry.Date.Month != date.Month || previousAdminEntry.Date.Year != date.Year)
            return null;

        // Include all employees' sales and expenses from that same date
        var allEntriesForDate = await _db.DaybookEntries
            .Include(d => d.Sales)
            .Include(d => d.Expenses)
            .Where(d => d.Date == previousAdminEntry.Date)
            .ToListAsync();

        var totalSales = allEntriesForDate.Sum(e => e.TotalSales);
        var totalExpenses = allEntriesForDate.Sum(e => e.TotalExpenses);
        return previousAdminEntry.OpeningBalance + totalSales - totalExpenses;
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
