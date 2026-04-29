using System.Security.Claims;
using DataEntry.Api.DTOs;
using DataEntry.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace DataEntry.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class DaybookController : ControllerBase
{
    private readonly DaybookService _daybookService;

    public DaybookController(DaybookService daybookService)
    {
        _daybookService = daybookService;
    }

    [HttpGet]
    public async Task<IActionResult> Get([FromQuery] DateOnly? date, [FromQuery] int? employeeId)
    {
        var targetDate = date ?? DateOnly.FromDateTime(DateTime.Today);
        var targetEmployee = employeeId ?? GetCurrentEmployeeId();

        // Non-admin users can only see their own daybook
        if (!IsAdmin() && targetEmployee != GetCurrentEmployeeId())
            return Forbid();

        var result = await _daybookService.GetOrCreateAsync(targetEmployee, targetDate);
        return Ok(result);
    }

    [HttpPut("{id}/opening-balance")]
    public async Task<IActionResult> UpdateOpeningBalance(int id, [FromBody] CreateDaybookRequest request)
    {
        if (request.OpeningBalance == null)
            return BadRequest(new { message = "Opening balance is required" });

        var result = await _daybookService.UpdateOpeningBalanceAsync(id, request.OpeningBalance.Value);
        if (result == null) return NotFound();
        return Ok(result);
    }

    [HttpPost("{id}/sales")]
    public async Task<IActionResult> AddSale(int id, [FromBody] AddSaleRequest request)
    {
        var result = await _daybookService.AddSaleAsync(id, request);
        if (result == null)
            return BadRequest(new { message = "Cannot add sale. Entry may be finalized or invalid data." });
        return Created($"api/daybook/sales/{result.Id}", result);
    }

    [HttpDelete("sales/{saleId}")]
    public async Task<IActionResult> DeleteSale(int saleId)
    {
        var success = await _daybookService.DeleteSaleAsync(saleId);
        if (!success) return BadRequest(new { message = "Cannot delete sale. Entry may be finalized." });
        return NoContent();
    }

    [HttpPost("{id}/expenses")]
    public async Task<IActionResult> AddExpense(int id, [FromBody] AddExpenseRequest request)
    {
        var result = await _daybookService.AddExpenseAsync(id, request);
        if (result == null)
            return BadRequest(new { message = "Cannot add expense. Entry may be finalized." });
        return Created($"api/daybook/expenses/{result.Id}", result);
    }

    [HttpDelete("expenses/{expenseId}")]
    public async Task<IActionResult> DeleteExpense(int expenseId)
    {
        var success = await _daybookService.DeleteExpenseAsync(expenseId);
        if (!success) return BadRequest(new { message = "Cannot delete expense. Entry may be finalized." });
        return NoContent();
    }

    [HttpPut("{id}/finalize")]
    public async Task<IActionResult> Finalize(int id)
    {
        var result = await _daybookService.FinalizeAsync(id);
        if (result == null) return NotFound();
        return Ok(result);
    }

    private int GetCurrentEmployeeId()
    {
        return int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
    }

    private bool IsAdmin()
    {
        return User.IsInRole("Admin");
    }
}
