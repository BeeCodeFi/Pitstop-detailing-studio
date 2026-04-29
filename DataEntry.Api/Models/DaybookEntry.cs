using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace DataEntry.Api.Models;

public class DaybookEntry
{
    public int Id { get; set; }

    public int EmployeeId { get; set; }
    public Employee Employee { get; set; } = null!;

    public DateOnly Date { get; set; }

    [Column(TypeName = "decimal(10,2)")]
    public decimal OpeningBalance { get; set; }

    [MaxLength(500)]
    public string? Notes { get; set; }

    public bool IsFinalized { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    // Navigation
    public List<SaleTransaction> Sales { get; set; } = new();
    public List<Expense> Expenses { get; set; } = new();

    // Computed properties (not stored in DB)
    [NotMapped]
    public decimal TotalSales => Sales.Sum(s => s.Amount);

    [NotMapped]
    public decimal TotalCashCollected => Sales.Where(s => s.PaymentMode == "Cash").Sum(s => s.Amount);

    [NotMapped]
    public decimal TotalCardCollected => Sales.Where(s => s.PaymentMode == "Card").Sum(s => s.Amount);

    [NotMapped]
    public decimal TotalUpiCollected => Sales.Where(s => s.PaymentMode == "UPI").Sum(s => s.Amount);

    [NotMapped]
    public decimal TotalExpenses => Expenses.Sum(e => e.Amount);

    [NotMapped]
    public decimal ClosingBalance => OpeningBalance + TotalCashCollected - TotalExpenses;
}
