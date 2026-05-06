using System.ComponentModel.DataAnnotations;

namespace DataEntry.Api.DTOs;

// === Auth ===
public record LoginRequest(
    [Required] string Username,
    [Required] string Password
);

public record LoginResponse(
    string Token,
    int EmployeeId,
    string Name,
    string Role
);

public record RegisterRequest(
    [Required, MaxLength(100)] string Name,
    [Required, MaxLength(50)] string Username,
    [Required, MinLength(6)] string Password,
    [Required] string Role,
    string? Phone
);

// === Employee ===
public record EmployeeDto(
    int Id,
    string Name,
    string Username,
    string Role,
    string? Phone,
    bool IsActive,
    DateTime CreatedAt
);

public record UpdateEmployeeRequest(
    string? Name,
    string? Phone,
    string? Role,
    bool? IsActive,
    string? NewPassword
);

// === Customer ===
public record CustomerDto(
    int Id,
    string Name,
    string? Phone,
    string? VehicleNumber,
    string? VehicleType,
    string? Notes,
    DateTime CreatedAt
);

public record CreateCustomerRequest(
    [Required, MaxLength(100)] string Name,
    string? Phone,
    string? VehicleNumber,
    string? VehicleType,
    string? Notes
);

public record UpdateCustomerRequest(
    string? Name,
    string? Phone,
    string? VehicleNumber,
    string? VehicleType,
    string? Notes
);

// === ServiceType ===
public record ServiceTypeDto(
    int Id,
    string Name,
    decimal DefaultPrice,
    bool IsActive
);

public record CreateServiceTypeRequest(
    [Required, MaxLength(100)] string Name,
    decimal DefaultPrice
);

public record UpdateServiceTypeRequest(
    string? Name,
    decimal? DefaultPrice,
    bool? IsActive
);

// === Daybook ===
public record DaybookEntryDto(
    int Id,
    int EmployeeId,
    string EmployeeName,
    DateOnly Date,
    decimal OpeningBalance,
    decimal TotalSales,
    decimal TotalCashCollected,
    decimal TotalCardCollected,
    decimal TotalUpiCollected,
    decimal TotalExpenses,
    decimal ClosingBalance,
    string? Notes,
    bool IsFinalized,
    List<SaleTransactionDto> Sales,
    List<ExpenseDto> Expenses
);

public record CreateDaybookRequest(
    decimal? OpeningBalance,
    string? Notes
);

// === Sale Transaction ===
public record SaleTransactionDto(
    int Id,
    int? CustomerId,
    string? CustomerName,
    int ServiceTypeId,
    string ServiceTypeName,
    string? VehicleNumber,
    string? VehicleType,
    decimal Amount,
    string PaymentMode,
    string? Notes,
    DateTime CreatedAt
);

public record AddSaleRequest(
    int? CustomerId,
    [Required] int ServiceTypeId,
    string? VehicleNumber,
    string? VehicleType,
    [Required] decimal Amount,
    [Required] string PaymentMode,
    string? Notes
);

// === Expense ===
public record ExpenseDto(
    int Id,
    string Description,
    decimal Amount,
    DateTime CreatedAt
);

public record AddExpenseRequest(
    [Required, MaxLength(200)] string Description,
    [Required] decimal Amount
);

// === Reports ===
public record DailySummaryDto(
    DateOnly Date,
    List<EmployeeDaySummaryDto> Employees,
    decimal GrandTotalSales,
    decimal GrandTotalCash,
    decimal GrandTotalCard,
    decimal GrandTotalUpi,
    decimal GrandTotalExpenses
);

public record EmployeeDaySummaryDto(
    int EmployeeId,
    string EmployeeName,
    decimal OpeningBalance,
    decimal TotalSales,
    decimal TotalCash,
    decimal TotalCard,
    decimal TotalUpi,
    decimal TotalExpenses,
    decimal ClosingBalance
);

public record MonthlySummaryDto(
    int Year,
    int Month,
    List<DayTotalDto> DailyTotals,
    decimal GrandTotalSales,
    decimal GrandTotalCash,
    decimal GrandTotalExpenses,
    decimal GrandTotalSalaries,
    decimal NetIncome
);

public record DayTotalDto(
    DateOnly Date,
    decimal TotalSales,
    decimal TotalCash,
    decimal TotalExpenses,
    int TransactionCount
);

public record EmployeeReportDto(
    int EmployeeId,
    string EmployeeName,
    DateOnly From,
    DateOnly To,
    List<DaybookEntrySummaryDto> Entries,
    decimal TotalSales,
    decimal TotalCash,
    decimal TotalExpenses
);

public record DaybookEntrySummaryDto(
    DateOnly Date,
    decimal OpeningBalance,
    decimal TotalSales,
    decimal TotalCash,
    decimal TotalExpenses,
    decimal ClosingBalance,
    int TransactionCount
);

// === Salary ===
public record SalaryPaymentDto(
    int Id,
    int EmployeeId,
    string EmployeeName,
    decimal Amount,
    DateOnly Date,
    string? Notes,
    DateTime CreatedAt
);

public record CreateSalaryPaymentRequest(
    [Required] int EmployeeId,
    [Required] decimal Amount,
    [Required] DateOnly Date,
    string? Notes
);

public record UpdateSalaryPaymentRequest(
    decimal? Amount,
    DateOnly? Date,
    string? Notes
);

// === Combined Daily Sales (Admin view) ===
public record SaleWithEmployeeDto(
    int Id,
    int EmployeeId,
    string EmployeeName,
    int? CustomerId,
    string? CustomerName,
    int ServiceTypeId,
    string ServiceTypeName,
    string? VehicleNumber,
    string? VehicleType,
    decimal Amount,
    string PaymentMode,
    string? Notes,
    DateTime CreatedAt
);

public record DailyCombinedSalesDto(
    DateOnly Date,
    List<SaleWithEmployeeDto> Sales,
    decimal TotalSales,
    decimal TotalCash,
    decimal TotalCard,
    decimal TotalUpi,
    int TransactionCount
);
