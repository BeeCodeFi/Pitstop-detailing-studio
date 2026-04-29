using DataEntry.Api.Data;
using DataEntry.Api.DTOs;
using DataEntry.Api.Models;
using DataEntry.Api.Services;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;

namespace DataEntry.Api.Tests;

public class ReportServiceTests : IDisposable
{
    private readonly AppDbContext _db;
    private readonly DaybookService _daybookService;
    private readonly ReportService _reportService;

    public ReportServiceTests()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;
        _db = new AppDbContext(options);
        SeedTestData();
        _daybookService = new DaybookService(_db);
        _reportService = new ReportService(_db);
    }

    private void SeedTestData()
    {
        _db.Employees.AddRange(
            new Employee { Id = 1, Name = "Employee A", Username = "empA", PasswordHash = "h", Role = "Employee" },
            new Employee { Id = 2, Name = "Employee B", Username = "empB", PasswordHash = "h", Role = "Employee" }
        );
        _db.ServiceTypes.Add(new ServiceType { Id = 1, Name = "Wash", DefaultPrice = 500 });
        _db.SaveChanges();
    }

    [Fact]
    public async Task DailySummary_AggregatesAllEmployees()
    {
        var date = new DateOnly(2026, 4, 29);

        var e1 = await _daybookService.GetOrCreateAsync(1, date);
        await _daybookService.AddSaleAsync(e1.Id, new AddSaleRequest(null, 1, null, null, 500, "Cash", null));

        var e2 = await _daybookService.GetOrCreateAsync(2, date);
        await _daybookService.AddSaleAsync(e2.Id, new AddSaleRequest(null, 1, null, null, 800, "Card", null));

        var summary = await _reportService.GetDailySummaryAsync(date);
        summary.Date.Should().Be(date);
        summary.Employees.Should().HaveCount(2);
        summary.GrandTotalSales.Should().Be(1300);
        summary.GrandTotalCash.Should().Be(500);
        summary.GrandTotalCard.Should().Be(800);
    }

    [Fact]
    public async Task MonthlySummary_GroupsByDay()
    {
        var d1 = new DateOnly(2026, 4, 1);
        var d2 = new DateOnly(2026, 4, 15);

        var e1 = await _daybookService.GetOrCreateAsync(1, d1);
        await _daybookService.AddSaleAsync(e1.Id, new AddSaleRequest(null, 1, null, null, 500, "Cash", null));

        var e2 = await _daybookService.GetOrCreateAsync(1, d2);
        await _daybookService.AddSaleAsync(e2.Id, new AddSaleRequest(null, 1, null, null, 800, "Cash", null));

        var summary = await _reportService.GetMonthlySummaryAsync(2026, 4);
        summary.Year.Should().Be(2026);
        summary.Month.Should().Be(4);
        summary.DailyTotals.Should().HaveCount(2);
        summary.GrandTotalSales.Should().Be(1300);
    }

    [Fact]
    public async Task EmployeeReport_FiltersDateRange()
    {
        var d1 = new DateOnly(2026, 4, 1);
        var d2 = new DateOnly(2026, 4, 15);
        var d3 = new DateOnly(2026, 5, 1); // outside range

        foreach (var d in new[] { d1, d2, d3 })
        {
            var e = await _daybookService.GetOrCreateAsync(1, d);
            await _daybookService.AddSaleAsync(e.Id, new AddSaleRequest(null, 1, null, null, 500, "Cash", null));
        }

        var report = await _reportService.GetEmployeeReportAsync(1, new DateOnly(2026, 4, 1), new DateOnly(2026, 4, 30));
        report.Should().NotBeNull();
        report!.Entries.Should().HaveCount(2); // only April entries
        report.TotalSales.Should().Be(1000);
    }

    [Fact]
    public async Task EmployeeReport_ReturnsNull_ForInvalidEmployee()
    {
        var result = await _reportService.GetEmployeeReportAsync(999, new DateOnly(2026, 4, 1), new DateOnly(2026, 4, 30));
        result.Should().BeNull();
    }

    public void Dispose()
    {
        _db.Dispose();
    }
}
