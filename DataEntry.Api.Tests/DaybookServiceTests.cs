using DataEntry.Api.Data;
using DataEntry.Api.DTOs;
using DataEntry.Api.Models;
using DataEntry.Api.Services;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;

namespace DataEntry.Api.Tests;

public class DaybookServiceTests : IDisposable
{
    private readonly AppDbContext _db;
    private readonly DaybookService _service;

    public DaybookServiceTests()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;
        _db = new AppDbContext(options);
        SeedTestData();
        _service = new DaybookService(_db);
    }

    private void SeedTestData()
    {
        _db.Employees.Add(new Employee
        {
            Id = 1, Name = "Test Employee", Username = "test",
            PasswordHash = "hash", Role = "Employee"
        });
        _db.Employees.Add(new Employee
        {
            Id = 2, Name = "Test Admin", Username = "admin",
            PasswordHash = "hash", Role = "Admin"
        });
        _db.ServiceTypes.Add(new ServiceType { Id = 1, Name = "Exterior Wash", DefaultPrice = 500 });
        _db.ServiceTypes.Add(new ServiceType { Id = 2, Name = "Full Detail", DefaultPrice = 2500 });
        _db.SaveChanges();
    }

    [Fact]
    public async Task GetOrCreate_CreatesNewEntry_WhenNoneExists()
    {
        var date = new DateOnly(2026, 4, 29);
        var result = await _service.GetOrCreateAsync(1, date);

        result.Should().NotBeNull();
        result.EmployeeId.Should().Be(1);
        result.Date.Should().Be(date);
        result.OpeningBalance.Should().Be(0);
        result.TotalSales.Should().Be(0);
        result.ClosingBalance.Should().Be(0);
    }

    [Fact]
    public async Task GetOrCreate_ReturnsSameEntry_WhenAlreadyExists()
    {
        var date = new DateOnly(2026, 4, 29);
        var first = await _service.GetOrCreateAsync(1, date);
        var second = await _service.GetOrCreateAsync(1, date);

        second.Id.Should().Be(first.Id);
    }

    [Fact]
    public async Task GetOrCreate_CarriesForwardBalance_FromPreviousDay()
    {
        var day1 = new DateOnly(2026, 4, 28);
        var day2 = new DateOnly(2026, 4, 29);

        // Create day 1 with opening 1000, add a cash sale of 500, expense of 200
        var entry1 = await _service.GetOrCreateAsync(1, day1);
        await _service.UpdateOpeningBalanceAsync(entry1.Id, 1000);
        await _service.AddSaleAsync(entry1.Id, new AddSaleRequest(null, 1, null, null, 500, "Cash", null));
        await _service.AddExpenseAsync(entry1.Id, new AddExpenseRequest("Supplies", 200));

        // Day 1 closing = 1000 + 500 - 200 = 1300
        var entry2 = await _service.GetOrCreateAsync(1, day2);
        entry2.OpeningBalance.Should().Be(1300);
    }

    [Fact]
    public async Task AddSale_CalculatesTotalsCorrectly()
    {
        var date = new DateOnly(2026, 4, 29);
        var entry = await _service.GetOrCreateAsync(1, date);

        await _service.AddSaleAsync(entry.Id, new AddSaleRequest(null, 1, "KA01AB1234", "Car", 500, "Cash", null));
        await _service.AddSaleAsync(entry.Id, new AddSaleRequest(null, 2, "KA01CD5678", "SUV", 2500, "Card", null));
        await _service.AddSaleAsync(entry.Id, new AddSaleRequest(null, 1, null, null, 500, "UPI", null));

        var result = await _service.GetOrCreateAsync(1, date);
        result.TotalSales.Should().Be(3500);
        result.TotalCashCollected.Should().Be(500);
        result.TotalCardCollected.Should().Be(2500);
        result.TotalUpiCollected.Should().Be(500);
        result.Sales.Should().HaveCount(3);
    }

    [Fact]
    public async Task AddExpense_ReducesClosingBalance()
    {
        var date = new DateOnly(2026, 4, 29);
        var entry = await _service.GetOrCreateAsync(1, date);
        await _service.UpdateOpeningBalanceAsync(entry.Id, 1000);
        await _service.AddSaleAsync(entry.Id, new AddSaleRequest(null, 1, null, null, 500, "Cash", null));
        await _service.AddExpenseAsync(entry.Id, new AddExpenseRequest("Cleaning supplies", 300));

        var result = await _service.GetOrCreateAsync(1, date);
        // Closing = 1000 + 500 (cash) - 300 = 1200
        result.ClosingBalance.Should().Be(1200);
        result.TotalExpenses.Should().Be(300);
    }

    [Fact]
    public async Task DeleteSale_RemovesSaleAndUpdatesTotals()
    {
        var date = new DateOnly(2026, 4, 29);
        var entry = await _service.GetOrCreateAsync(1, date);
        var sale = await _service.AddSaleAsync(entry.Id, new AddSaleRequest(null, 1, null, null, 500, "Cash", null));

        var deleted = await _service.DeleteSaleAsync(sale!.Id);
        deleted.Should().BeTrue();

        var result = await _service.GetOrCreateAsync(1, date);
        result.TotalSales.Should().Be(0);
        result.Sales.Should().BeEmpty();
    }

    [Fact]
    public async Task Finalize_PreventsModification()
    {
        var date = new DateOnly(2026, 4, 29);
        var entry = await _service.GetOrCreateAsync(1, date);
        await _service.FinalizeAsync(entry.Id);

        var sale = await _service.AddSaleAsync(entry.Id, new AddSaleRequest(null, 1, null, null, 500, "Cash", null));
        sale.Should().BeNull();

        var expense = await _service.AddExpenseAsync(entry.Id, new AddExpenseRequest("Test", 100));
        expense.Should().BeNull();
    }

    [Fact]
    public async Task AddSale_RejectsInvalidPaymentMode()
    {
        var date = new DateOnly(2026, 4, 29);
        var entry = await _service.GetOrCreateAsync(1, date);
        var sale = await _service.AddSaleAsync(entry.Id, new AddSaleRequest(null, 1, null, null, 500, "Bitcoin", null));
        sale.Should().BeNull();
    }

    [Fact]
    public async Task NonCashSales_DoNotAffectClosingBalance()
    {
        var date = new DateOnly(2026, 4, 29);
        var entry = await _service.GetOrCreateAsync(1, date);
        await _service.UpdateOpeningBalanceAsync(entry.Id, 1000);

        // Card and UPI sales should NOT affect cash closing balance
        await _service.AddSaleAsync(entry.Id, new AddSaleRequest(null, 1, null, null, 2000, "Card", null));
        await _service.AddSaleAsync(entry.Id, new AddSaleRequest(null, 1, null, null, 1500, "UPI", null));

        var result = await _service.GetOrCreateAsync(1, date);
        result.TotalSales.Should().Be(3500);
        result.ClosingBalance.Should().Be(1000); // Only opening, no cash sales
    }

    public void Dispose()
    {
        _db.Dispose();
    }
}
