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
public class EmployeeController : ControllerBase
{
    private readonly AppDbContext _db;

    public EmployeeController(AppDbContext db)
    {
        _db = db;
    }

    [HttpGet]
    public async Task<IActionResult> GetAll()
    {
        var employees = await _db.Employees
            .OrderBy(e => e.Name)
            .Select(e => new EmployeeDto(e.Id, e.Name, e.Username, e.Role, e.Phone, e.IsActive, e.CreatedAt))
            .ToListAsync();
        return Ok(employees);
    }

    [HttpGet("{id}")]
    public async Task<IActionResult> GetById(int id)
    {
        var e = await _db.Employees.FindAsync(id);
        if (e == null) return NotFound();
        return Ok(new EmployeeDto(e.Id, e.Name, e.Username, e.Role, e.Phone, e.IsActive, e.CreatedAt));
    }

    [HttpPut("{id}")]
    public async Task<IActionResult> Update(int id, [FromBody] UpdateEmployeeRequest request)
    {
        var employee = await _db.Employees.FindAsync(id);
        if (employee == null) return NotFound();

        if (request.Name != null) employee.Name = request.Name;
        if (request.Phone != null) employee.Phone = request.Phone;
        if (request.Role != null) employee.Role = request.Role;
        if (request.IsActive.HasValue) employee.IsActive = request.IsActive.Value;
        if (!string.IsNullOrWhiteSpace(request.NewPassword))
            employee.PasswordHash = BCrypt.Net.BCrypt.HashPassword(request.NewPassword);

        await _db.SaveChangesAsync();

        return Ok(new EmployeeDto(employee.Id, employee.Name, employee.Username,
            employee.Role, employee.Phone, employee.IsActive, employee.CreatedAt));
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> Delete(int id)
    {
        var currentUserId = int.Parse(User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)!.Value);
        if (id == currentUserId)
            return BadRequest(new { message = "You cannot delete your own account." });

        var employee = await _db.Employees.FindAsync(id);
        if (employee == null) return NotFound();

        var hasDaybookEntries = await _db.DaybookEntries.AnyAsync(d => d.EmployeeId == id);
        if (hasDaybookEntries)
            return BadRequest(new { message = "Cannot delete an employee who has daybook records. Deactivate them instead." });

        _db.Employees.Remove(employee);
        await _db.SaveChangesAsync();
        return NoContent();
    }
}
