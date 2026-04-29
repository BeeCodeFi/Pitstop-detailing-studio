using DataEntry.Api.Data;
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

    public AdminController(AppDbContext db)
    {
        _db = db;
    }

    [HttpDelete("reset")]
    public async Task<IActionResult> ResetAllData()
    {
        // Cascade delete handles SaleTransactions and Expenses automatically
        await _db.DaybookEntries.ExecuteDeleteAsync();
        return Ok(new { message = "All daybook data has been reset successfully." });
    }
}
