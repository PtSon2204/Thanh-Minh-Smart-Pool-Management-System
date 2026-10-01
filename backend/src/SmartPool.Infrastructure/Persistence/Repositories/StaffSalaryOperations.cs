using Microsoft.EntityFrameworkCore;
using Npgsql;
using SmartPool.Application.Common.Models;
using SmartPool.Application.Features.ManageStaffs;
using SmartPool.Application.Features.ManageStaffs.Commands.SaveSalary;
using SmartPool.Application.Features.ManageStaffs.Queries.GetSalaries;
using SmartPool.Domain.Entities;

namespace SmartPool.Infrastructure.Persistence.Repositories
{

public sealed partial class StaffOperations
{
    public async Task<PagedResponse<GetSalariesResponse>> GetSalariesAsync(GetSalariesQuery request, CancellationToken cancellationToken)
    {
        var query = context.Salaries.AsNoTracking().AsQueryable();
        if (request.EmployeeId is not null) query = query.Where(item => item.EmployeeId == request.EmployeeId);
        if (request.Month is not null) query = query.Where(item => item.Month == request.Month);
        if (request.Year is not null) query = query.Where(item => item.Year == request.Year);
        var total = await query.CountAsync(cancellationToken); var page = Normalize(request.PageIndex, request.PageSize);
        var rows = await query.OrderByDescending(item => item.Year).ThenByDescending(item => item.Month).Skip((page.Page - 1) * page.PageSize).Take(page.PageSize).ToListAsync(cancellationToken);
        return new PagedResponse<GetSalariesResponse> { Items = mapper.Map<List<GetSalariesResponse>>(rows), TotalCount = total, PageIndex = page.Page, PageSize = page.PageSize };
    }

    public async Task<SaveSalaryResponse> SaveSalaryAsync(SaveSalaryCommand request, CancellationToken cancellationToken)
    {
        if (!await context.Employees.AnyAsync(item => item.UserId == request.UserId, cancellationToken)) throw new KeyNotFoundException("Không tìm thấy nhân viên.");
        var salary = request.Id is null ? null : await context.Salaries.FirstOrDefaultAsync(item => item.Id == request.Id && item.EmployeeId == request.UserId, cancellationToken);
        if (request.Id is not null && salary is null) throw new KeyNotFoundException("Không tìm thấy bảng lương.");
        var duplicate = await context.Salaries.AnyAsync(item => item.EmployeeId == request.UserId && item.Month == request.Month && item.Year == request.Year && item.Id != request.Id, cancellationToken);
        if (duplicate) throw new StaffConflictException("Bảng lương đã tồn tại cho tháng này.");
        salary ??= new Salary { Id = Guid.NewGuid(), EmployeeId = request.UserId, CreatedAt = DateTime.UtcNow };
        salary.Month = request.Month; salary.Year = request.Year; salary.TotalShifts = request.TotalShifts; salary.Bonus = request.Bonus; salary.Deduction = request.Deduction; salary.NetSalary = request.NetSalary;
        if (request.Id is null) context.Salaries.Add(salary);
        try { await context.SaveChangesAsync(cancellationToken); }
        catch (DbUpdateException exception) when (exception.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation, ConstraintName: "ux_salaries_employee_id_month_year" })
        {
            throw new StaffConflictException("Bảng lương đã tồn tại cho tháng này.");
        }
        return mapper.Map<SaveSalaryResponse>(salary);
    }

}
}
