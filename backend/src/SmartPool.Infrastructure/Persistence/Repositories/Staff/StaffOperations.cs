using Microsoft.EntityFrameworkCore;
using AutoMapper;
using SmartPool.Application.Common.Models;
using SmartPool.Application.Features.ManageStaffs;
using SmartPool.Application.Features.ManageStaffs.Commands.CreateStaff;
using SmartPool.Application.Features.ManageStaffs.Commands.SaveShift;
using SmartPool.Application.Features.ManageStaffs.Commands.UpdateStaff;
using SmartPool.Application.Features.ManageStaffs.Queries.GetShifts;
using SmartPool.Application.Features.ManageStaffs.Queries.GetStaffOptions;
using SmartPool.Application.Features.ManageStaffs.Queries.GetStaffs;
using SmartPool.Application.Interfaces.Repositories;
using SmartPool.Infrastructure.Persistence.DbContext;
using SmartPool.Domain.Entities;
using SmartPool.Domain.Enums;

namespace SmartPool.Infrastructure.Persistence.Repositories
{

public sealed partial class StaffOperations : IStaffOperations
{
    private readonly SmartPoolDbContext context;
    private readonly IMapper mapper;

    public StaffOperations(SmartPoolDbContext context, IMapper mapper)
    {
        this.context = context;
        this.mapper = mapper;
    }

    public async Task<PagedResponse<GetStaffsResponse>> GetStaffsAsync(GetStaffsQuery request, CancellationToken cancellationToken)
    {
        var query = context.Employees.AsNoTracking().Include(employee => employee.User).ThenInclude(user => user.Role)
            .Include(employee => employee.User.UserProfile).AsQueryable();
        if (!string.IsNullOrWhiteSpace(request.SearchTerm))
            query = query.Where(employee => employee.User.Username!.Contains(request.SearchTerm) || employee.User.Email!.Contains(request.SearchTerm) || employee.User.UserProfile!.FullName.Contains(request.SearchTerm));
        if (!string.IsNullOrWhiteSpace(request.Status)) query = query.Where(employee => employee.Status == request.Status);
        if (!string.IsNullOrWhiteSpace(request.RoleName)) query = query.Where(employee => employee.User.Role != null && EF.Functions.ILike(employee.User.Role.Name, request.RoleName));
        var total = await query.CountAsync(cancellationToken);
        var page = Normalize(request.PageIndex, request.PageSize);
        var rows = await query.OrderBy(employee => employee.User.Username).Skip((page.Page - 1) * page.PageSize).Take(page.PageSize).ToListAsync(cancellationToken);
        return new PagedResponse<GetStaffsResponse> { Items = mapper.Map<List<GetStaffsResponse>>(rows), TotalCount = total, PageIndex = page.Page, PageSize = page.PageSize };
    }

    public async Task<GetStaffOptionsResponse> GetOptionsAsync(CancellationToken cancellationToken)
    {
        var users = await context.Users.AsNoTracking()
            .Where(user => user.IsDeleted != true && user.Employee == null && user.Role != null && user.Role.IsDeleted != true &&
                (EF.Functions.ILike(user.Role.Name, RoleEnum.STAFF.ToString()) || EF.Functions.ILike(user.Role.Name, RoleEnum.ADMIN.ToString())))
            .Include(user => user.UserProfile).OrderBy(user => user.Username).ToListAsync(cancellationToken);
        return new GetStaffOptionsResponse { Users = mapper.Map<List<EligibleUserResponse>>(users) };
    }

    public async Task<CreateStaffResponse> CreateStaffAsync(CreateStaffCommand request, CancellationToken cancellationToken)
    {
        await using var transaction = await context.Database.BeginTransactionAsync(cancellationToken);
        await context.Database.ExecuteSqlInterpolatedAsync($"SELECT pg_advisory_xact_lock(hashtextextended({request.UserId.ToString()}, 0))", cancellationToken);
        var user = await context.Users.Include(item => item.Employee).Include(item => item.UserProfile).Include(item => item.Role).FirstOrDefaultAsync(item => item.Id == request.UserId && item.IsDeleted != true, cancellationToken);
        if (user is null) throw new KeyNotFoundException("Không tìm thấy tài khoản.");
        if (user.Employee is not null) throw new StaffConflictException("Tài khoản đã được gắn với nhân viên.");
        if (user.Role?.IsDeleted == true || !IsEligibleEmploymentRole(user.Role?.Name)) throw new StaffConflictException("Tài khoản phải có vai trò STAFF hoặc ADMIN.");
        var employee = new Employee { UserId = user.Id, BaseSalary = request.BaseSalary, JoinDate = request.JoinDate, Status = request.Status, CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow, User = user };
        context.Employees.Add(employee);
        await context.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return mapper.Map<CreateStaffResponse>(employee);
    }

    public async Task<UpdateStaffResponse> UpdateStaffAsync(UpdateStaffCommand request, CancellationToken cancellationToken)
    {
        var employee = await context.Employees.Include(item => item.User).ThenInclude(user => user.UserProfile).Include(item => item.User.Role).FirstOrDefaultAsync(item => item.UserId == request.UserId, cancellationToken);
        if (employee is null) throw new KeyNotFoundException("Không tìm thấy nhân viên.");
        employee.BaseSalary = request.BaseSalary; employee.Status = request.Status; employee.UpdatedAt = DateTime.UtcNow;
        await context.SaveChangesAsync(cancellationToken);
        return mapper.Map<UpdateStaffResponse>(employee);
    }

    public async Task<PagedResponse<GetShiftsResponse>> GetShiftsAsync(GetShiftsQuery request, CancellationToken cancellationToken)
    {
        var query = context.Shifts.AsNoTracking().AsQueryable();
        if (request.IsActive is not null) query = query.Where(shift => shift.IsActive == request.IsActive);
        var total = await query.CountAsync(cancellationToken); var page = Normalize(request.PageIndex, request.PageSize);
        var rows = await query.OrderBy(shift => shift.StartTime).Skip((page.Page - 1) * page.PageSize).Take(page.PageSize).ToListAsync(cancellationToken);
        return new PagedResponse<GetShiftsResponse> { Items = mapper.Map<List<GetShiftsResponse>>(rows), TotalCount = total, PageIndex = page.Page, PageSize = page.PageSize };
    }

    public async Task<SaveShiftResponse> SaveShiftAsync(SaveShiftCommand request, CancellationToken cancellationToken)
    {
        var shift = request.Id is null ? new Shift { Id = Guid.NewGuid() } : await context.Shifts.FindAsync([request.Id.Value], cancellationToken);
        if (shift is null) throw new KeyNotFoundException("Không tìm thấy ca làm việc.");
        shift.Name = request.Name; shift.StartTime = request.StartTime; shift.EndTime = request.EndTime; shift.IsActive = request.IsActive;
        if (request.Id is null) context.Shifts.Add(shift);
        await context.SaveChangesAsync(cancellationToken);
        return mapper.Map<SaveShiftResponse>(shift);
    }

    private static bool IsEligibleEmploymentRole(string? roleName) => string.Equals(roleName, RoleEnum.STAFF.ToString(), StringComparison.OrdinalIgnoreCase) || string.Equals(roleName, RoleEnum.ADMIN.ToString(), StringComparison.OrdinalIgnoreCase);
    private static (int Page, int PageSize) Normalize(int page, int pageSize) => (Math.Max(1, page), Math.Clamp(pageSize, 1, 100));
}
}
