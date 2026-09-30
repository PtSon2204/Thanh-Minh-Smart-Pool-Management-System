using Microsoft.EntityFrameworkCore;
using Npgsql;
using SmartPool.Application.Features.ManageStaffs.Contracts;
using SmartPool.Infrastructure.Persistence.TempModels;

namespace SmartPool.Infrastructure.Persistence.Repositories;

public sealed partial class StaffOperations
{
    public async Task<OperationResult<PageResult<SalaryDto>>> GetSalariesAsync(SalaryListRequest request, CancellationToken cancellationToken)
    {
        var query = context.Salaries.AsNoTracking().AsQueryable();
        if (request.EmployeeId is not null) query = query.Where(item => item.EmployeeId == request.EmployeeId);
        if (request.Month is not null) query = query.Where(item => item.Month == request.Month);
        if (request.Year is not null) query = query.Where(item => item.Year == request.Year);
        var total = await query.CountAsync(cancellationToken); var page = Normalize(request.Page, request.PageSize);
        var rows = await query.OrderByDescending(item => item.Year).ThenByDescending(item => item.Month).Skip((page.Page - 1) * page.PageSize).Take(page.PageSize).ToListAsync(cancellationToken);
        return OperationResult<PageResult<SalaryDto>>.Success(new(rows.Select(ToDto).ToList(), total, page.Page, page.PageSize));
    }

    public async Task<OperationResult<SalaryDto>> SaveSalaryAsync(Guid userId, Guid? id, SaveSalaryRequest request, CancellationToken cancellationToken)
    {
        if (!await context.Employees.AnyAsync(item => item.UserId == userId, cancellationToken)) return OperationResult<SalaryDto>.Failure(StaffError.NotFound, "Staff member was not found.");
        var salary = id is null ? null : await context.Salaries.FirstOrDefaultAsync(item => item.Id == id && item.EmployeeId == userId, cancellationToken);
        if (id is not null && salary is null) return OperationResult<SalaryDto>.Failure(StaffError.NotFound, "Salary was not found.");
        var duplicate = await context.Salaries.AnyAsync(item => item.EmployeeId == userId && item.Month == request.Month && item.Year == request.Year && item.Id != id, cancellationToken);
        if (duplicate) return OperationResult<SalaryDto>.Failure(StaffError.Conflict, "Salary already exists for this month.");
        salary ??= new Salary { Id = Guid.NewGuid(), EmployeeId = userId, CreatedAt = DateTime.UtcNow };
        salary.Month = request.Month; salary.Year = request.Year; salary.TotalShifts = request.TotalShifts; salary.Bonus = request.Bonus; salary.Deduction = request.Deduction; salary.NetSalary = request.NetSalary;
        if (id is null) context.Salaries.Add(salary);
        try { await context.SaveChangesAsync(cancellationToken); }
        catch (DbUpdateException exception) when (exception.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation, ConstraintName: "ux_salaries_employee_id_month_year" })
        {
            return OperationResult<SalaryDto>.Failure(StaffError.Conflict, "Salary already exists for this month.");
        }
        return OperationResult<SalaryDto>.Success(ToDto(salary));
    }

    private static SalaryDto ToDto(Salary salary) => new(salary.Id, salary.EmployeeId, salary.Month, salary.Year, salary.TotalShifts ?? 0, salary.Bonus ?? 0, salary.Deduction ?? 0, salary.NetSalary);
}
