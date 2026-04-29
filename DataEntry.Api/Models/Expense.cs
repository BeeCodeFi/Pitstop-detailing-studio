using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace DataEntry.Api.Models;

public class Expense
{
    public int Id { get; set; }

    public int DaybookEntryId { get; set; }
    public DaybookEntry DaybookEntry { get; set; } = null!;

    [Required, MaxLength(200)]
    public string Description { get; set; } = string.Empty;

    [Column(TypeName = "decimal(10,2)")]
    public decimal Amount { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
