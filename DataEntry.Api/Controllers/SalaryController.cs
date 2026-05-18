using DataEntry.Api.Data;
using DataEntry.Api.DTOs;
using DataEntry.Api.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace DataEntry.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize(Roles = "Admin,Explorer")]
public class SalaryController : ControllerBase
{
    private readonly AppDbContext _db;

    public SalaryController(AppDbContext db)
    {
        _db = db;
    }

    [HttpGet]
    public async Task<IActionResult> GetAll([FromQuery] int? year, [FromQuery] int? month)
    {
        var query = _db.SalaryPayments
            .Include(s => s.Employee)
            .AsQueryable();

        if (year.HasValue)
            query = query.Where(s => s.Date.Year == year.Value);
        if (month.HasValue)
            query = query.Where(s => s.Date.Month == month.Value);

        var payments = await query
            .OrderByDescending(s => s.Date)
            .Select(s => new SalaryPaymentDto(
                s.Id, s.EmployeeId, s.Employee.Name,
                s.Amount, s.Date, s.Notes, s.CreatedAt))
            .ToListAsync();

        return Ok(payments);
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateSalaryPaymentRequest request)
    {
        var employee = await _db.Employees.FindAsync(request.EmployeeId);
        if (employee == null)
            return BadRequest(new { message = "Employee not found." });

        var payment = new SalaryPayment
        {
            EmployeeId = request.EmployeeId,
            Amount = request.Amount,
            Date = request.Date,
            Notes = request.Notes
        };

        _db.SalaryPayments.Add(payment);
        await _db.SaveChangesAsync();

        await _db.Entry(payment).Reference(p => p.Employee).LoadAsync();

        return Ok(new SalaryPaymentDto(
            payment.Id, payment.EmployeeId, payment.Employee.Name,
            payment.Amount, payment.Date, payment.Notes, payment.CreatedAt));
    }

    [HttpPut("{id}")]
    public async Task<IActionResult> Update(int id, [FromBody] UpdateSalaryPaymentRequest request)
    {
        var payment = await _db.SalaryPayments
            .Include(s => s.Employee)
            .FirstOrDefaultAsync(s => s.Id == id);

        if (payment == null) return NotFound();

        if (request.Amount.HasValue) payment.Amount = request.Amount.Value;
        if (request.Date.HasValue) payment.Date = request.Date.Value;
        if (request.Notes != null) payment.Notes = request.Notes;

        await _db.SaveChangesAsync();

        return Ok(new SalaryPaymentDto(
            payment.Id, payment.EmployeeId, payment.Employee.Name,
            payment.Amount, payment.Date, payment.Notes, payment.CreatedAt));
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> Delete(int id)
    {
        var payment = await _db.SalaryPayments.FindAsync(id);
        if (payment == null) return NotFound();

        _db.SalaryPayments.Remove(payment);
        await _db.SaveChangesAsync();
        return NoContent();
    }
}
