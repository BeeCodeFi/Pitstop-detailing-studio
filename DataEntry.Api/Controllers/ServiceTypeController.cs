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
public class ServiceTypeController : ControllerBase
{
    private readonly AppDbContext _db;

    public ServiceTypeController(AppDbContext db)
    {
        _db = db;
    }

    [HttpGet]
    public async Task<IActionResult> GetAll([FromQuery] bool includeInactive = false)
    {
        var query = _db.ServiceTypes.AsQueryable();
        if (!includeInactive)
            query = query.Where(s => s.IsActive);

        var services = await query
            .OrderBy(s => s.Name)
            .Select(s => new ServiceTypeDto(s.Id, s.Name, s.DefaultPrice, s.IsActive))
            .ToListAsync();

        return Ok(services);
    }

    [HttpPost]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> Create([FromBody] CreateServiceTypeRequest request)
    {
        var service = new ServiceType
        {
            Name = request.Name,
            DefaultPrice = request.DefaultPrice
        };

        _db.ServiceTypes.Add(service);
        await _db.SaveChangesAsync();

        return Created($"api/servicetype/{service.Id}",
            new ServiceTypeDto(service.Id, service.Name, service.DefaultPrice, service.IsActive));
    }

    [HttpPut("{id}")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> Update(int id, [FromBody] UpdateServiceTypeRequest request)
    {
        var service = await _db.ServiceTypes.FindAsync(id);
        if (service == null) return NotFound();

        if (request.Name != null) service.Name = request.Name;
        if (request.DefaultPrice.HasValue) service.DefaultPrice = request.DefaultPrice.Value;
        if (request.IsActive.HasValue) service.IsActive = request.IsActive.Value;

        await _db.SaveChangesAsync();

        return Ok(new ServiceTypeDto(service.Id, service.Name, service.DefaultPrice, service.IsActive));
    }

    [HttpDelete("{id}")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> Delete(int id)
    {
        var service = await _db.ServiceTypes.FindAsync(id);
        if (service == null) return NotFound();

        var hasLinkedSales = await _db.SaleTransactions.AnyAsync(s => s.ServiceTypeId == id);
        if (hasLinkedSales)
            return BadRequest(new { message = "Cannot delete a service that has existing sale records. Deactivate it instead." });

        _db.ServiceTypes.Remove(service);
        await _db.SaveChangesAsync();

        return NoContent();
    }
}
