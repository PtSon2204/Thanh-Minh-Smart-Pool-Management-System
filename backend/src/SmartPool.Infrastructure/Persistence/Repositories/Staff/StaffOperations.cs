using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using AutoMapper;
using SmartPool.Application.Common.Models;
using SmartPool.Application.Features.ManageStaffs;
using SmartPool.Application.Features.ManageStaffs.AttendanceQr;
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

namespace SmartPool.Infrastructure.Persistence.Repositories.Staff
{

public sealed partial class StaffOperations : IStaffOperations
{
    private readonly SmartPoolDbContext context;
    private readonly IMapper mapper;
    private readonly TimeProvider timeProvider;
    private readonly StaffAttendanceQrSettings attendanceQrSettings;
    private readonly SmartPool.Infrastructure.StaffOnboarding.StaffOnboardingServices? onboardingServices;

    public StaffOperations(SmartPoolDbContext context, IMapper mapper, TimeProvider timeProvider, IOptions<StaffAttendanceQrSettings> attendanceQrSettings,
        SmartPool.Infrastructure.StaffOnboarding.StaffOnboardingServices? onboardingServices = null)
    {
        this.context = context;
        this.mapper = mapper;
        this.timeProvider = timeProvider;
        this.attendanceQrSettings = attendanceQrSettings.Value;
        this.onboardingServices = onboardingServices;
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
                (EF.Functions.ILike(user.Role.Name, RoleEnum.LIFEGUARD.ToString()) ||
                 EF.Functions.ILike(user.Role.Name, RoleEnum.SALES.ToString()) ||
                 EF.Functions.ILike(user.Role.Name, RoleEnum.COACH.ToString()) ||
                 EF.Functions.ILike(user.Role.Name, RoleEnum.ADMIN.ToString())))
            .Include(user => user.UserProfile).OrderBy(user => user.Username).ToListAsync(cancellationToken);
        return new GetStaffOptionsResponse { Users = mapper.Map<List<EligibleUserResponse>>(users) };
    }

    public Task<CreateStaffResponse> CreateStaffAsync(CreateStaffCommand request, CancellationToken cancellationToken) =>
        Task.FromException<CreateStaffResponse>(new SmartPool.Application.Features.ManageStaffs.Onboarding.StaffOnboardingException("ONBOARDING_REQUIRED"));

    public async Task<UpdateStaffResponse> UpdateStaffAsync(UpdateStaffCommand request, CancellationToken cancellationToken)
    {
        var employee = await context.Employees.Include(item => item.User).ThenInclude(user => user.UserProfile).Include(item => item.User.Role).FirstOrDefaultAsync(item => item.UserId == request.UserId, cancellationToken);
        if (employee is null) throw new KeyNotFoundException("Không tìm thấy nhân viên.");
        employee.Status = request.Status; employee.UpdatedAt = DateTime.UtcNow;
        await context.SaveChangesAsync(cancellationToken);
        return mapper.Map<UpdateStaffResponse>(employee);
    }

    public async Task<PagedResponse<GetShiftsResponse>> GetShiftsAsync(GetShiftsQuery request, CancellationToken cancellationToken)
    {
        var query = context.Shifts.AsNoTracking();
        if (request.IsActive is not null) query = query.Where(shift => shift.IsActive == request.IsActive);
        var total = await query.CountAsync(cancellationToken); var page = Normalize(request.PageIndex, request.PageSize);
        var rows = await query.OrderBy(shift => shift.StartTime).Skip((page.Page - 1) * page.PageSize).Take(page.PageSize).ToListAsync(cancellationToken);
        return new PagedResponse<GetShiftsResponse> { Items = mapper.Map<List<GetShiftsResponse>>(rows), TotalCount = total, PageIndex = page.Page, PageSize = page.PageSize };
    }

    public async Task<SaveShiftResponse> SaveShiftAsync(SaveShiftCommand request, CancellationToken cancellationToken)
    {
        await using var transaction = await context.Database.BeginTransactionAsync(cancellationToken);
        Shift shift;
        if (request.Id is null)
        {
            shift = new Shift { Id = Guid.NewGuid() };
            context.Shifts.Add(shift);
        }
        else
        {
            await LockShiftAsync(request.Id.Value, cancellationToken);
            shift = await context.Shifts.FirstOrDefaultAsync(item => item.Id == request.Id.Value, cancellationToken)
                ?? throw new KeyNotFoundException("Không tìm thấy ca làm việc.");
            if ((shift.StartTime != request.StartTime || shift.EndTime != request.EndTime) &&
                await context.EmployeeShifts.AnyAsync(item => item.ShiftId == shift.Id, cancellationToken))
                throw new StaffConflictException("Ca đã được phân công nên không thể đổi giờ. Hãy tạo ca mới.");
        }

        shift.Name = request.Name.Trim();
        shift.StartTime = request.StartTime;
        shift.EndTime = request.EndTime;
        shift.IsActive = request.IsActive;
        await context.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return mapper.Map<SaveShiftResponse>(shift);
    }

    private static bool IsEligibleEmploymentRole(string? roleName) =>
        new[] { RoleEnum.LIFEGUARD, RoleEnum.SALES, RoleEnum.COACH, RoleEnum.ADMIN }
            .Any(role => string.Equals(roleName, role.ToString(), StringComparison.OrdinalIgnoreCase));
    private static (int Page, int PageSize) Normalize(int page, int pageSize) => (Math.Max(1, page), Math.Clamp(pageSize, 1, 100));
}
}
