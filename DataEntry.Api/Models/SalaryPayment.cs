using System.ComponentModel.DataAnnotations;

namespace DataEntry.Api.Models;

public class SalaryPayment
{
    public int Id { get; set; }

    public int EmployeeId { get; set; }
    public Employee Employee { get; set; } = null!;

    [Required]
    public decimal Amount { get; set; }

    public DateOnly Date { get; set; } // Date the salary was paid

    [MaxLength(300)]
    public string? Notes { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
