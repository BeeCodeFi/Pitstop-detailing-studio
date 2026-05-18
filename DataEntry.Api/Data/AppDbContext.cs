using DataEntry.Api.Models;
using DataEntry.Api.Services;
using Microsoft.EntityFrameworkCore;

namespace DataEntry.Api.Data;

public class AppDbContext : DbContext
{
    private readonly IExplorerModeAccessor? _explorerMode;

    public AppDbContext(DbContextOptions<AppDbContext> options, IExplorerModeAccessor? explorerMode = null)
        : base(options)
    {
        _explorerMode = explorerMode;
    }

    // Explorer mode: skip all writes so no data is persisted to the database.
    public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        if (_explorerMode?.IsExplorer == true)
            return Task.FromResult(0);
        return base.SaveChangesAsync(cancellationToken);
    }

    public override int SaveChanges()
    {
        if (_explorerMode?.IsExplorer == true)
            return 0;
        return base.SaveChanges();
    }

    public DbSet<Employee> Employees => Set<Employee>();
    public DbSet<Customer> Customers => Set<Customer>();
    public DbSet<ServiceType> ServiceTypes => Set<ServiceType>();
    public DbSet<DaybookEntry> DaybookEntries => Set<DaybookEntry>();
    public DbSet<SaleTransaction> SaleTransactions => Set<SaleTransaction>();
    public DbSet<Expense> Expenses => Set<Expense>();
    public DbSet<SalaryPayment> SalaryPayments => Set<SalaryPayment>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // Unique constraint: one daybook entry per employee per day
        modelBuilder.Entity<DaybookEntry>()
            .HasIndex(d => new { d.EmployeeId, d.Date })
            .IsUnique();

        // Unique username
        modelBuilder.Entity<Employee>()
            .HasIndex(e => e.Username)
            .IsUnique();

        // Cascade deletes
        modelBuilder.Entity<DaybookEntry>()
            .HasMany(d => d.Sales)
            .WithOne(s => s.DaybookEntry)
            .HasForeignKey(s => s.DaybookEntryId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<DaybookEntry>()
            .HasMany(d => d.Expenses)
            .WithOne(e => e.DaybookEntry)
            .HasForeignKey(e => e.DaybookEntryId)
            .OnDelete(DeleteBehavior.Cascade);

        // Optional customer on sale
        modelBuilder.Entity<SaleTransaction>()
            .HasOne(s => s.Customer)
            .WithMany()
            .HasForeignKey(s => s.CustomerId)
            .OnDelete(DeleteBehavior.SetNull);

        // Salary payments belong to an employee
        modelBuilder.Entity<SalaryPayment>()
            .HasOne(sp => sp.Employee)
            .WithMany()
            .HasForeignKey(sp => sp.EmployeeId)
            .OnDelete(DeleteBehavior.Cascade);

        // Seed default service types
        modelBuilder.Entity<ServiceType>().HasData(
            new ServiceType { Id = 1, Name = "Exterior Wash", DefaultPrice = 500 },
            new ServiceType { Id = 2, Name = "Interior Cleaning", DefaultPrice = 800 },
            new ServiceType { Id = 3, Name = "Full Detailing", DefaultPrice = 2500 },
            new ServiceType { Id = 4, Name = "Polish & Wax", DefaultPrice = 1500 },
            new ServiceType { Id = 5, Name = "Ceramic Coating", DefaultPrice = 8000 },
            new ServiceType { Id = 6, Name = "Engine Bay Cleaning", DefaultPrice = 1000 },
            new ServiceType { Id = 7, Name = "Headlight Restoration", DefaultPrice = 600 },
            new ServiceType { Id = 8, Name = "Seat/Upholstery Cleaning", DefaultPrice = 1200 },
            new ServiceType { Id = 9, Name = "AC Vent Sanitization", DefaultPrice = 400 },
            new ServiceType { Id = 10, Name = "Tyre Dressing", DefaultPrice = 300 }
        );

        // Seed admin user (password: Admin@123)
        // IMPORTANT: use a fixed static hash so EF migrations do NOT reset the password on every deployment.
        // Hash was generated once with BCrypt.HashPassword("Admin@123", workFactor: 11).
        modelBuilder.Entity<Employee>().HasData(
            new Employee
            {
                Id = 1,
                Name = "Administrator",
                Username = "admin",
                PasswordHash = "$2a$11$mmKQ90xIFZIRuUgTf8ioKuxlPJX6Url5Ypz0Yt6zkFra8p83QvIoG",
                Role = "Admin",
                IsActive = true,
                CreatedAt = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc)
            }
        );
    }
}
