using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace DataEntry.Api.Models;

public class SaleTransaction
{
    public int Id { get; set; }

    public int DaybookEntryId { get; set; }
    public DaybookEntry DaybookEntry { get; set; } = null!;

    public int? CustomerId { get; set; }
    public Customer? Customer { get; set; }

    public int ServiceTypeId { get; set; }
    public ServiceType ServiceType { get; set; } = null!;

    [MaxLength(20)]
    public string? VehicleNumber { get; set; }

    [MaxLength(30)]
    public string? VehicleType { get; set; }

    [Column(TypeName = "decimal(10,2)")]
    public decimal Amount { get; set; }

    [Required, MaxLength(10)]
    public string PaymentMode { get; set; } = "Cash"; // Cash, Card, UPI

    [MaxLength(500)]
    public string? Notes { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
