using DataEntry.Api.DTOs;
using DataEntry.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace DataEntry.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class ReportController : ControllerBase
{
    private readonly ReportService _reportService;

    public ReportController(ReportService reportService)
    {
        _reportService = reportService;
    }

    [HttpGet("daily")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> DailySummary([FromQuery] DateOnly? date)
    {
        var targetDate = date ?? DateOnly.FromDateTime(DateTime.Today);
        var result = await _reportService.GetDailySummaryAsync(targetDate);
        return Ok(result);
    }

    [HttpGet("monthly")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> MonthlySummary([FromQuery] int? year, [FromQuery] int? month)
    {
        var now = DateTime.Today;
        var y = year ?? now.Year;
        var m = month ?? now.Month;
        var result = await _reportService.GetMonthlySummaryAsync(y, m);
        return Ok(result);
    }

    [HttpGet("employee/{id}")]
    public async Task<IActionResult> EmployeeReport(int id, [FromQuery] DateOnly? from, [FromQuery] DateOnly? to)
    {
        var fromDate = from ?? DateOnly.FromDateTime(DateTime.Today.AddDays(-30));
        var toDate = to ?? DateOnly.FromDateTime(DateTime.Today);
        var result = await _reportService.GetEmployeeReportAsync(id, fromDate, toDate);
        if (result == null) return NotFound();
        return Ok(result);
    }

    [HttpGet("export")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> ExportCsv(
        [FromQuery] DateOnly from,
        [FromQuery] DateOnly to,
        [FromQuery] int? employeeId = null,
        [FromQuery] string? type = null,
        [FromQuery] string? paymentMode = null)
    {
        if (from > to) return BadRequest(new { message = "From date must be before to date." });
        var csv = await _reportService.ExportCsvAsync(from, to, employeeId, type, paymentMode);
        return File(csv, "text/csv", $"daybook_export_{from:yyyy-MM-dd}_{to:yyyy-MM-dd}.csv");
    }
}
