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
        var total = await query.CountAsync(cancellationToken);
        var page = Normalize(request.PageIndex, request.PageSize);
        var rows = await query.OrderBy(employee => employee.User.Username).Skip((page.Page - 1) * page.PageSize).Take(page.PageSize).ToListAsync(cancellationToken);
        return new PagedResponse<GetStaffsResponse> { Items = mapper.Map<List<GetStaffsResponse>>(rows), TotalCount = total, PageIndex = page.Page, PageSize = page.PageSize };
    }

    public async Task<GetStaffOptionsResponse> GetOptionsAsync(CancellationToken cancellationToken)
    {
        var users = await context.Users.AsNoTracking().Where(user => user.IsDeleted != true && user.Employee == null)
            .Include(user => user.UserProfile).OrderBy(user => user.Username).ToListAsync(cancellationToken);
        var roles = await context.Roles.AsNoTracking().Where(role => role.IsDeleted != true).OrderBy(role => role.Name).ToListAsync(cancellationToken);
        return new GetStaffOptionsResponse { Users = mapper.Map<List<EligibleUserResponse>>(users), Roles = mapper.Map<List<RoleResponse>>(roles) };
    }

    public async Task<CreateStaffResponse> CreateStaffAsync(CreateStaffCommand request, CancellationToken cancellationToken)
    {
        var user = await context.Users.Include(item => item.Employee).Include(item => item.UserProfile).FirstOrDefaultAsync(item => item.Id == request.UserId && item.IsDeleted != true, cancellationToken);
        if (user is null) throw new KeyNotFoundException("Không tìm thấy tài khoản.");
        if (user.Employee is not null) throw new StaffConflictException("Tài khoản đã được gắn với nhân viên.");
        var role = await ActiveRoleAsync(request.RoleId, cancellationToken);
        if (role is null) throw new KeyNotFoundException("Không tìm thấy vai trò.");
        user.RoleId = role.Id;
        user.UserProfile ??= new UserProfile { UserId = user.Id };
        ApplyProfile(user.UserProfile, request.FullName, request.AvatarUrl, request.Address, request.DateOfBirth);
        var employee = new Employee { UserId = user.Id, BaseSalary = request.BaseSalary, JoinDate = request.JoinDate, Status = request.Status, CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow, User = user };
        context.Employees.Add(employee);
        await context.SaveChangesAsync(cancellationToken);
        employee.User.Role = role;
        return mapper.Map<CreateStaffResponse>(employee);
    }

    public async Task<UpdateStaffResponse> UpdateStaffAsync(UpdateStaffCommand request, CancellationToken cancellationToken)
    {
        var employee = await context.Employees.Include(item => item.User).ThenInclude(user => user.UserProfile).Include(item => item.User.Role).FirstOrDefaultAsync(item => item.UserId == request.UserId, cancellationToken);
        if (employee is null) throw new KeyNotFoundException("Không tìm thấy nhân viên.");
        var role = await ActiveRoleAsync(request.RoleId, cancellationToken);
        if (role is null) throw new KeyNotFoundException("Không tìm thấy vai trò.");
        employee.User.RoleId = role.Id;
        employee.User.UserProfile ??= new UserProfile { UserId = employee.UserId };
        ApplyProfile(employee.User.UserProfile, request.FullName, request.AvatarUrl, request.Address, request.DateOfBirth);
        employee.BaseSalary = request.BaseSalary; employee.Status = request.Status; employee.UpdatedAt = DateTime.UtcNow; employee.User.Role = role;
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

    private async Task<Role?> ActiveRoleAsync(Guid id, CancellationToken cancellationToken) => await context.Roles.FirstOrDefaultAsync(role => role.Id == id && role.IsDeleted != true, cancellationToken);
    private static void ApplyProfile(UserProfile profile, string fullName, string? avatarUrl, string? address, DateOnly? birthDate) { profile.FullName = fullName; profile.AvatarUrl = avatarUrl; profile.Address = address; profile.DateOfBirth = birthDate; profile.UpdatedAt = DateTime.UtcNow; }
    private static (int Page, int PageSize) Normalize(int page, int pageSize) => (Math.Max(1, page), Math.Clamp(pageSize, 1, 100));
}
}
