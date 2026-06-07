using DataEntry.Api.Data;
using DataEntry.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace DataEntry.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize(Roles = "Admin")]
public class AdminController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly DaybookService _daybookService;

    public AdminController(AppDbContext db, DaybookService daybookService)
    {
        _db = db;
        _daybookService = daybookService;
    }

    [HttpDelete("reset")]
    public async Task<IActionResult> ResetAllData()
    {
        // Cascade delete handles SaleTransactions and Expenses automatically
        await _db.DaybookEntries.ExecuteDeleteAsync();
        return Ok(new { message = "All daybook data has been reset successfully." });
    }

    /// <summary>
    /// Repairs the opening-balance chain for the given month (defaults to current month).
    /// Walks admin entries in date order and re-derives each opening balance from the
    /// previous day's closing. The first admin entry of the month is the anchor.
    /// </summary>
    [HttpPost("repair-month")]
    public async Task<IActionResult> RepairMonth([FromQuery] int? year, [FromQuery] int? month)
    {
        var today = DateTime.Today;
        var y = year  ?? today.Year;
        var m = month ?? today.Month;
        var result = await _daybookService.RepairMonthChainAsync(y, m);
        return Ok(result);
    }
}
