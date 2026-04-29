using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using DataEntry.Api.Data;
using DataEntry.Api.DTOs;
using DataEntry.Api.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;

namespace DataEntry.Api.Services;

public class AuthService
{
    private readonly AppDbContext _db;
    private readonly IConfiguration _config;

    public AuthService(AppDbContext db, IConfiguration config)
    {
        _db = db;
        _config = config;
    }

    public async Task<LoginResponse?> LoginAsync(LoginRequest request)
    {
        var employee = await _db.Employees
            .FirstOrDefaultAsync(e => e.Username == request.Username && e.IsActive);

        if (employee == null || !BCrypt.Net.BCrypt.Verify(request.Password, employee.PasswordHash))
            return null;

        var token = GenerateToken(employee);
        return new LoginResponse(token, employee.Id, employee.Name, employee.Role);
    }

    public async Task<EmployeeDto?> RegisterAsync(RegisterRequest request)
    {
        if (await _db.Employees.AnyAsync(e => e.Username == request.Username))
            return null;

        var employee = new Employee
        {
            Name = request.Name,
            Username = request.Username,
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(request.Password),
            Role = request.Role,
            Phone = request.Phone
        };

        _db.Employees.Add(employee);
        await _db.SaveChangesAsync();

        return new EmployeeDto(employee.Id, employee.Name, employee.Username,
            employee.Role, employee.Phone, employee.IsActive, employee.CreatedAt);
    }

    private string GenerateToken(Employee employee)
    {
        var key = new SymmetricSecurityKey(
            Encoding.UTF8.GetBytes(_config["Jwt:Key"]!));
        var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var claims = new[]
        {
            new Claim(ClaimTypes.NameIdentifier, employee.Id.ToString()),
            new Claim(ClaimTypes.Name, employee.Name),
            new Claim(ClaimTypes.Role, employee.Role)
        };

        var token = new JwtSecurityToken(
            issuer: _config["Jwt:Issuer"],
            audience: _config["Jwt:Audience"],
            claims: claims,
            expires: DateTime.UtcNow.AddHours(12),
            signingCredentials: creds
        );

        return new JwtSecurityTokenHandler().WriteToken(token);
    }
}
