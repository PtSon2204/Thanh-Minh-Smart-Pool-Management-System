namespace SmartPool.Application.Features.ManageStaffs.Contracts;

public sealed record SalaryDto(Guid Id, Guid EmployeeId, int Month, int Year, int TotalShifts,
    decimal Bonus, decimal Deduction, decimal NetSalary);
public sealed record SalaryListRequest(int Page = 1, int PageSize = 20, Guid? EmployeeId = null,
    int? Month = null, int? Year = null);
public sealed record SaveSalaryRequest(int Month, int Year, int TotalShifts, decimal Bonus, decimal Deduction,
    decimal NetSalary);
