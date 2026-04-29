using DataEntry.Api.Data;
using DataEntry.Api.DTOs;
using DataEntry.Api.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace DataEntry.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class CustomerController : ControllerBase
{
    private readonly AppDbContext _db;

    public CustomerController(AppDbContext db)
    {
        _db = db;
    }

    [HttpGet]
    public async Task<IActionResult> GetAll([FromQuery] string? search)
    {
        var query = _db.Customers.AsQueryable();

        if (!string.IsNullOrWhiteSpace(search))
        {
            var s = search.ToLower();
            query = query.Where(c =>
                c.Name.ToLower().Contains(s) ||
                (c.Phone != null && c.Phone.Contains(s)) ||
                (c.VehicleNumber != null && c.VehicleNumber.ToLower().Contains(s)));
        }

        var customers = await query
            .OrderByDescending(c => c.CreatedAt)
            .Select(c => new CustomerDto(c.Id, c.Name, c.Phone, c.VehicleNumber, c.VehicleType, c.Notes, c.CreatedAt))
            .ToListAsync();

        return Ok(customers);
    }

    [HttpGet("{id}")]
    public async Task<IActionResult> GetById(int id)
    {
        var c = await _db.Customers.FindAsync(id);
        if (c == null) return NotFound();
        return Ok(new CustomerDto(c.Id, c.Name, c.Phone, c.VehicleNumber, c.VehicleType, c.Notes, c.CreatedAt));
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateCustomerRequest request)
    {
        var customer = new Customer
        {
            Name = request.Name,
            Phone = request.Phone,
            VehicleNumber = request.VehicleNumber,
            VehicleType = request.VehicleType,
            Notes = request.Notes
        };

        _db.Customers.Add(customer);
        await _db.SaveChangesAsync();

        var dto = new CustomerDto(customer.Id, customer.Name, customer.Phone,
            customer.VehicleNumber, customer.VehicleType, customer.Notes, customer.CreatedAt);
        return CreatedAtAction(nameof(GetById), new { id = customer.Id }, dto);
    }

    [HttpPut("{id}")]
    public async Task<IActionResult> Update(int id, [FromBody] UpdateCustomerRequest request)
    {
        var customer = await _db.Customers.FindAsync(id);
        if (customer == null) return NotFound();

        if (request.Name != null) customer.Name = request.Name;
        if (request.Phone != null) customer.Phone = request.Phone;
        if (request.VehicleNumber != null) customer.VehicleNumber = request.VehicleNumber;
        if (request.VehicleType != null) customer.VehicleType = request.VehicleType;
        if (request.Notes != null) customer.Notes = request.Notes;

        await _db.SaveChangesAsync();

        return Ok(new CustomerDto(customer.Id, customer.Name, customer.Phone,
            customer.VehicleNumber, customer.VehicleType, customer.Notes, customer.CreatedAt));
    }

    [HttpDelete("{id}")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> Delete(int id)
    {
        var customer = await _db.Customers.FindAsync(id);
        if (customer == null) return NotFound();
        _db.Customers.Remove(customer);
        await _db.SaveChangesAsync();
        return NoContent();
    }
}
