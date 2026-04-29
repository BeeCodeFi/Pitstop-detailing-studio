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
            var openingBalance = await GetCarryForwardBalance(employeeId, date);
            entry = new DaybookEntry
            {
                EmployeeId = employeeId,
                Date = date,
                OpeningBalance = openingBalance
            };
            _db.DaybookEntries.Add(entry);
            await _db.SaveChangesAsync();

            // Reload with includes
            entry = await _db.DaybookEntries
                .Include(d => d.Employee)
                .Include(d => d.Sales).ThenInclude(s => s.Customer)
                .Include(d => d.Sales).ThenInclude(s => s.ServiceType)
                .Include(d => d.Expenses)
                .FirstAsync(d => d.Id == entry.Id);
        }

        return MapToDto(entry);
    }

    public async Task<DaybookEntryDto?> UpdateOpeningBalanceAsync(int daybookId, decimal openingBalance)
    {
        var entry = await LoadEntry(daybookId);
        if (entry == null || entry.IsFinalized) return null;

        entry.OpeningBalance = openingBalance;
        entry.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();
        return MapToDto(entry);
    }

    public async Task<SaleTransactionDto?> AddSaleAsync(int daybookId, AddSaleRequest request)
    {
        var entry = await _db.DaybookEntries.FindAsync(daybookId);
        if (entry == null || entry.IsFinalized) return null;

        var validModes = new[] { "Cash", "Card", "UPI" };
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

        // Reload with navigation
        var loaded = await _db.SaleTransactions
            .Include(s => s.Customer)
            .Include(s => s.ServiceType)
            .FirstAsync(s => s.Id == sale.Id);

        return MapSaleToDto(loaded);
    }

    public async Task<bool> DeleteSaleAsync(int saleId)
    {
        var sale = await _db.SaleTransactions
            .Include(s => s.DaybookEntry)
            .FirstOrDefaultAsync(s => s.Id == saleId);

        if (sale == null || sale.DaybookEntry.IsFinalized) return false;

        _db.SaleTransactions.Remove(sale);
        sale.DaybookEntry.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();
        return true;
    }

    public async Task<ExpenseDto?> AddExpenseAsync(int daybookId, AddExpenseRequest request)
    {
        var entry = await _db.DaybookEntries.FindAsync(daybookId);
        if (entry == null || entry.IsFinalized) return null;

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

    public async Task<bool> DeleteExpenseAsync(int expenseId)
    {
        var expense = await _db.Expenses
            .Include(e => e.DaybookEntry)
            .FirstOrDefaultAsync(e => e.Id == expenseId);

        if (expense == null || expense.DaybookEntry.IsFinalized) return false;

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
        return MapToDto(entry);
    }

    private async Task<decimal> GetCarryForwardBalance(int employeeId, DateOnly date)
    {
        var previousEntry = await _db.DaybookEntries
            .Include(d => d.Sales)
            .Include(d => d.Expenses)
            .Where(d => d.EmployeeId == employeeId && d.Date < date)
            .OrderByDescending(d => d.Date)
            .FirstOrDefaultAsync();

        return previousEntry?.ClosingBalance ?? 0;
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

    public static DaybookEntryDto MapToDto(DaybookEntry entry)
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
            entry.TotalExpenses,
            entry.ClosingBalance,
            entry.Notes,
            entry.IsFinalized,
            entry.Sales.Select(MapSaleToDto).ToList(),
            entry.Expenses.Select(e => new ExpenseDto(e.Id, e.Description, e.Amount, e.CreatedAt)).ToList()
        );
    }

    private static SaleTransactionDto MapSaleToDto(SaleTransaction s)
    {
        return new SaleTransactionDto(
            s.Id, s.CustomerId, s.Customer?.Name, s.ServiceTypeId,
            s.ServiceType?.Name ?? "", s.VehicleNumber, s.VehicleType,
            s.Amount, s.PaymentMode, s.Notes, s.CreatedAt
        );
    }
}
