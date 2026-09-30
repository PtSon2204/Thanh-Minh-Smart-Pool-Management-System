using Microsoft.EntityFrameworkCore;
using SmartPool.Application.Features.ManageStaffs.Contracts;
using SmartPool.Application.Interfaces.Repositories;
using SmartPool.Infrastructure.Persistence.DbContext;
using SmartPool.Infrastructure.Persistence.TempModels;

namespace SmartPool.Infrastructure.Persistence.Repositories;

public sealed partial class StaffOperations : IStaffOperations
{
    private readonly SmartPoolDbContext context;

    public StaffOperations(SmartPoolDbContext context) => this.context = context;
    public async Task<OperationResult<PageResult<StaffDto>>> GetStaffsAsync(StaffListRequest request, CancellationToken cancellationToken)
    {
        var query = context.Employees.AsNoTracking().Include(employee => employee.User).ThenInclude(user => user.Role)
            .Include(employee => employee.User.UserProfile).AsQueryable();
        if (!string.IsNullOrWhiteSpace(request.Search))
            query = query.Where(employee => employee.User.Username!.Contains(request.Search) || employee.User.Email!.Contains(request.Search) || employee.User.UserProfile!.FullName.Contains(request.Search));
        if (!string.IsNullOrWhiteSpace(request.Status)) query = query.Where(employee => employee.Status == request.Status);
        var total = await query.CountAsync(cancellationToken);
        var page = Normalize(request.Page, request.PageSize);
        var rows = await query.OrderBy(employee => employee.User.Username).Skip((page.Page - 1) * page.PageSize).Take(page.PageSize).ToListAsync(cancellationToken);
        return OperationResult<PageResult<StaffDto>>.Success(new(rows.Select(ToDto).ToList(), total, page.Page, page.PageSize));
    }

    public async Task<OperationResult<StaffOptionsDto>> GetOptionsAsync(CancellationToken cancellationToken)
    {
        var users = await context.Users.AsNoTracking().Where(user => user.IsDeleted != true && user.Employee == null)
            .Include(user => user.UserProfile).OrderBy(user => user.Username).ToListAsync(cancellationToken);
        var roles = await context.Roles.AsNoTracking().Where(role => role.IsDeleted != true).OrderBy(role => role.Name).ToListAsync(cancellationToken);
        return OperationResult<StaffOptionsDto>.Success(new(users.Select(user => new EligibleUserDto(user.Id, user.Username, user.Email, user.Phone, user.UserProfile?.FullName)).ToList(), roles.Select(role => new RoleDto(role.Id, role.Name)).ToList()));
    }

    public async Task<OperationResult<StaffDto>> CreateStaffAsync(CreateStaffRequest request, CancellationToken cancellationToken)
    {
        var user = await context.Users.Include(item => item.Employee).Include(item => item.UserProfile).FirstOrDefaultAsync(item => item.Id == request.UserId && item.IsDeleted != true, cancellationToken);
        if (user is null) return OperationResult<StaffDto>.Failure(StaffError.NotFound, "User was not found.");
        if (user.Employee is not null) return OperationResult<StaffDto>.Failure(StaffError.Conflict, "User is already an employee.");
        var role = await ActiveRoleAsync(request.RoleId, cancellationToken);
        if (role is null) return OperationResult<StaffDto>.Failure(StaffError.NotFound, "Role was not found.");
        user.RoleId = role.Id;
        user.UserProfile ??= new UserProfile { UserId = user.Id };
        ApplyProfile(user.UserProfile, request.FullName, request.AvatarUrl, request.Address, request.DateOfBirth);
        var employee = new Employee { UserId = user.Id, BaseSalary = request.BaseSalary, JoinDate = request.JoinDate, Status = request.Status, CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow, User = user };
        context.Employees.Add(employee);
        await context.SaveChangesAsync(cancellationToken);
        employee.User.Role = role;
        return OperationResult<StaffDto>.Success(ToDto(employee));
    }

    public async Task<OperationResult<StaffDto>> UpdateStaffAsync(Guid userId, UpdateStaffRequest request, CancellationToken cancellationToken)
    {
        var employee = await context.Employees.Include(item => item.User).ThenInclude(user => user.UserProfile).Include(item => item.User.Role).FirstOrDefaultAsync(item => item.UserId == userId, cancellationToken);
        if (employee is null) return OperationResult<StaffDto>.Failure(StaffError.NotFound, "Staff member was not found.");
        var role = await ActiveRoleAsync(request.RoleId, cancellationToken);
        if (role is null) return OperationResult<StaffDto>.Failure(StaffError.NotFound, "Role was not found.");
        employee.User.RoleId = role.Id;
        employee.User.UserProfile ??= new UserProfile { UserId = employee.UserId };
        ApplyProfile(employee.User.UserProfile, request.FullName, request.AvatarUrl, request.Address, request.DateOfBirth);
        employee.BaseSalary = request.BaseSalary; employee.Status = request.Status; employee.UpdatedAt = DateTime.UtcNow; employee.User.Role = role;
        await context.SaveChangesAsync(cancellationToken);
        return OperationResult<StaffDto>.Success(ToDto(employee));
    }

    public async Task<OperationResult<PageResult<ShiftDto>>> GetShiftsAsync(ShiftListRequest request, CancellationToken cancellationToken)
    {
        var query = context.Shifts.AsNoTracking().AsQueryable();
        if (request.Active is not null) query = query.Where(shift => shift.IsActive == request.Active);
        var total = await query.CountAsync(cancellationToken); var page = Normalize(request.Page, request.PageSize);
        var rows = await query.OrderBy(shift => shift.StartTime).Skip((page.Page - 1) * page.PageSize).Take(page.PageSize).ToListAsync(cancellationToken);
        return OperationResult<PageResult<ShiftDto>>.Success(new(rows.Select(ToDto).ToList(), total, page.Page, page.PageSize));
    }

    public async Task<OperationResult<ShiftDto>> SaveShiftAsync(Guid? id, SaveShiftRequest request, CancellationToken cancellationToken)
    {
        var shift = id is null ? new Shift { Id = Guid.NewGuid() } : await context.Shifts.FindAsync([id.Value], cancellationToken);
        if (shift is null) return OperationResult<ShiftDto>.Failure(StaffError.NotFound, "Shift was not found.");
        shift.Name = request.Name; shift.StartTime = request.StartTime; shift.EndTime = request.EndTime; shift.IsActive = request.IsActive;
        if (id is null) context.Shifts.Add(shift);
        await context.SaveChangesAsync(cancellationToken);
        return OperationResult<ShiftDto>.Success(ToDto(shift));
    }

    private async Task<Role?> ActiveRoleAsync(Guid id, CancellationToken cancellationToken) => await context.Roles.FirstOrDefaultAsync(role => role.Id == id && role.IsDeleted != true, cancellationToken);
    private static void ApplyProfile(UserProfile profile, string fullName, string? avatarUrl, string? address, DateOnly? birthDate) { profile.FullName = fullName; profile.AvatarUrl = avatarUrl; profile.Address = address; profile.DateOfBirth = birthDate; profile.UpdatedAt = DateTime.UtcNow; }
    private static (int Page, int PageSize) Normalize(int page, int pageSize) => (Math.Max(1, page), Math.Clamp(pageSize, 1, 100));
    private static StaffDto ToDto(Employee employee) => new(employee.UserId, employee.User.Username, employee.User.Email, employee.User.Phone, employee.User.UserProfile?.FullName ?? string.Empty, employee.User.UserProfile?.AvatarUrl, employee.User.UserProfile?.Address, employee.User.UserProfile?.DateOfBirth, employee.User.RoleId ?? Guid.Empty, employee.User.Role?.Name ?? string.Empty, employee.JoinDate, employee.BaseSalary, employee.Status ?? string.Empty);
    private static ShiftDto ToDto(Shift shift) => new(shift.Id, shift.Name, shift.StartTime, shift.EndTime, shift.IsActive == true);
}
