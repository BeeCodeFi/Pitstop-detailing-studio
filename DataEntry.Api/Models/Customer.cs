using System.ComponentModel.DataAnnotations;

namespace DataEntry.Api.Models;

public class Customer
{
    public int Id { get; set; }

    [Required, MaxLength(100)]
    public string Name { get; set; } = string.Empty;

    [MaxLength(15)]
    public string? Phone { get; set; }

    [MaxLength(20)]
    public string? VehicleNumber { get; set; }

    [MaxLength(30)]
    public string? VehicleType { get; set; } // Hatchback, Sedan, SUV, MUV, Crossover, Convertible, Bike

    [MaxLength(500)]
    public string? Notes { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
