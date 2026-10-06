using AutoMapper;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using SmartPool.Application.Features.ManageStaffs.Commands.CreateStaff;
using SmartPool.Application.Features.ManageStaffs.Mapping;
using SmartPool.Domain.Entities;
using SmartPool.Domain.Enums;
using SmartPool.Infrastructure.Persistence.DbContext;
using SmartPool.Infrastructure.Persistence.Repositories;

namespace SmartPool.IntegrationTests;

internal sealed class StaffEmploymentFixture : IAsyncDisposable
{
    private readonly DbContextOptions<SmartPoolDbContext> options;
    private readonly SmartPoolDbContext context;
    private readonly IMapper mapper;
    private readonly Guid[] userIds;
    private bool setupComplete;
    private bool cleaned;

    private StaffEmploymentFixture(DbContextOptions<SmartPoolDbContext> options)
    {
        this.options = options;
        context = new SmartPoolDbContext(options);
        mapper = new MapperConfiguration(configuration => configuration.AddProfile<StaffMappingProfile>(), NullLoggerFactory.Instance).CreateMapper();
        EligibleStaffUserId = Guid.NewGuid();
        EligibleAdminUserId = Guid.NewGuid();
        CustomerUserId = Guid.NewGuid();
        SoftDeletedRoleUserId = Guid.NewGuid();
        SoftDeletedRoleId = Guid.NewGuid();
        DeletedStaffUserId = Guid.NewGuid();
        AttachedStaffUserId = Guid.NewGuid();
        ConcurrentStaffUserId = Guid.NewGuid();
        userIds = [EligibleStaffUserId, EligibleAdminUserId, CustomerUserId, SoftDeletedRoleUserId, DeletedStaffUserId, AttachedStaffUserId, ConcurrentStaffUserId];
    }

    public Guid EligibleStaffUserId { get; }
    public Guid EligibleAdminUserId { get; }
    public Guid CustomerUserId { get; }
    public Guid SoftDeletedRoleUserId { get; }
    public Guid SoftDeletedRoleId { get; }
    public Guid DeletedStaffUserId { get; }
    public Guid AttachedStaffUserId { get; }
    public Guid ConcurrentStaffUserId { get; }
    public IReadOnlyCollection<Guid> UserIds => userIds;
    public (int Employees, int Profiles, int Users)? CleanupCounts { get; private set; }

    public static async Task<StaffEmploymentFixture> CreateAsync()
    {
        var options = await IntegrationTestDatabase.CreateOptionsAsync();
        await using var setupContext = new SmartPoolDbContext(options);
        var roles = await setupContext.Roles.AsNoTracking()
            .Where(role => role.IsDeleted != true)
            .Select(role => new RoleLookup(role.Id, role.Name))
            .ToListAsync();
        var roleIds = new RoleIds(
            FindRoleId(roles, RoleEnum.STAFF),
            FindRoleId(roles, RoleEnum.ADMIN),
            FindRoleId(roles, RoleEnum.CUSTOMER));
        var fixture = new StaffEmploymentFixture(options);

        try
        {
            var now = DateTime.UtcNow;
            var users = new[]
            {
                fixture.CreateUser(fixture.EligibleStaffUserId, roleIds.Staff, false, "eligible-staff", now),
                fixture.CreateUser(fixture.EligibleAdminUserId, roleIds.Admin, false, "eligible-admin", now),
                fixture.CreateUser(fixture.CustomerUserId, roleIds.Customer, false, "customer", now),
                fixture.CreateUser(fixture.SoftDeletedRoleUserId, fixture.SoftDeletedRoleId, false, "soft-deleted-role", now),
                fixture.CreateUser(fixture.DeletedStaffUserId, roleIds.Staff, true, "deleted-staff", now),
                fixture.CreateUser(fixture.AttachedStaffUserId, roleIds.Staff, false, "attached-staff", now),
                fixture.CreateUser(fixture.ConcurrentStaffUserId, roleIds.Staff, false, "concurrent-staff", now)
            };
            setupContext.Roles.Add(new Role { Id = fixture.SoftDeletedRoleId, Name = RoleEnum.STAFF.ToString(), IsDeleted = true });
            setupContext.Users.AddRange(users);
            setupContext.UserProfiles.AddRange(users.Select(user => fixture.CreateProfile(user.Id, user.Username!, now)));
            setupContext.Employees.Add(new Employee
            {
                UserId = fixture.AttachedStaffUserId,
                JoinDate = new DateOnly(2025, 1, 1),
                BaseSalary = 9000000,
                Status = "Working",
                CreatedAt = now,
                UpdatedAt = now
            });
            await setupContext.SaveChangesAsync();
            fixture.setupComplete = true;
            return fixture;
        }
        catch
        {
            await fixture.CleanupAsync();
            throw;
        }
    }

    public SmartPoolDbContext CreateContext() => new(options);

    public StaffOperations CreateOperations(SmartPoolDbContext? operationContext = null) => new(operationContext ?? context, mapper);

    public CreateStaffCommand CreateCommand(Guid userId) => new()
    {
        UserId = userId,
        JoinDate = new DateOnly(2026, 10, 5),
        BaseSalary = 10000000,
        Status = "Working"
    };

    public async Task<UserSnapshot> ReadUserSnapshotAsync(Guid userId)
    {
        await using var readContext = CreateContext();
        return await readContext.Users.AsNoTracking()
            .Where(user => user.Id == userId)
            .Select(user => new UserSnapshot(
                user.RoleId,
                user.Username,
                user.Email,
                user.Phone,
                user.Status,
                user.IsDeleted,
                user.UserProfile!.FullName,
                user.UserProfile.AvatarUrl,
                user.UserProfile.Address,
                user.UserProfile.DateOfBirth,
                user.UserProfile.UpdatedAt))
            .SingleAsync();
    }

    public async Task<Employee> ReadEmployeeAsync(Guid userId)
    {
        await using var readContext = CreateContext();
        return await readContext.Employees.AsNoTracking().SingleAsync(employee => employee.UserId == userId);
    }

    public async Task<bool> EmployeeExistsAsync(Guid userId)
    {
        await using var readContext = CreateContext();
        return await readContext.Employees.AnyAsync(employee => employee.UserId == userId);
    }

    public async Task<int> EmployeeCountAsync(Guid userId)
    {
        await using var readContext = CreateContext();
        return await readContext.Employees.CountAsync(employee => employee.UserId == userId);
    }

    public async Task CleanupAsync()
    {
        if (cleaned) return;

        await using var cleanupContext = CreateContext();
        var employees = await cleanupContext.Employees.Where(employee => userIds.Contains(employee.UserId)).ExecuteDeleteAsync();
        var profiles = await cleanupContext.UserProfiles.Where(profile => userIds.Contains(profile.UserId)).ExecuteDeleteAsync();
        var users = await cleanupContext.Users.Where(user => userIds.Contains(user.Id)).ExecuteDeleteAsync();
        var roles = await cleanupContext.Roles.Where(role => role.Id == SoftDeletedRoleId).ExecuteDeleteAsync();
        if (setupComplete && (profiles != userIds.Length || users != userIds.Length))
            throw new InvalidOperationException($"Staff fixture cleanup count mismatch: {employees}/{profiles}/{users}.");
        if (await cleanupContext.Employees.AnyAsync(employee => userIds.Contains(employee.UserId))
            || await cleanupContext.UserProfiles.AnyAsync(profile => userIds.Contains(profile.UserId))
            || await cleanupContext.Users.AnyAsync(user => userIds.Contains(user.Id)))
            throw new InvalidOperationException("Staff fixture rows remain after cleanup.");
        if (roles != (setupComplete ? 1 : 0))
            throw new InvalidOperationException($"Staff role fixture cleanup count mismatch: {roles}.");

        CleanupCounts = (employees, profiles, users);
        cleaned = true;
    }

    public async ValueTask DisposeAsync()
    {
        await CleanupAsync();
        await context.DisposeAsync();
    }

    private User CreateUser(Guid id, Guid roleId, bool isDeleted, string label, DateTime now) => new()
    {
        Id = id,
        RoleId = roleId,
        Username = $"qa-staff-{label}-{id:N}",
        Email = $"qa-staff-{label}-{id:N}@example.test",
        Phone = $"09{id.ToString("N")[..8]}",
        PasswordHash = "integration-test-only",
        Status = "Active",
        IsDeleted = isDeleted,
        CreatedAt = now,
        UpdatedAt = now
    };

    private UserProfile CreateProfile(Guid userId, string username, DateTime now) => new()
    {
        UserId = userId,
        FullName = $"QA {username}",
        AvatarUrl = $"https://example.test/{userId:N}.png",
        Address = "QA address",
        DateOfBirth = new DateOnly(2000, 1, 1),
        UpdatedAt = now
    };

    private static Guid FindRoleId(IEnumerable<RoleLookup> roles, RoleEnum role)
    {
        var found = roles.SingleOrDefault(item => string.Equals(item.Name, role.ToString(), StringComparison.OrdinalIgnoreCase));
        return found?.Id ?? throw new InvalidOperationException($"Guarded database requires the {role} role.");
    }
}

internal sealed record RoleIds(Guid Staff, Guid Admin, Guid Customer);

internal sealed record RoleLookup(Guid Id, string Name);

internal sealed record UserSnapshot(
    Guid? RoleId,
    string? Username,
    string? Email,
    string? Phone,
    string? Status,
    bool? IsDeleted,
    string FullName,
    string? AvatarUrl,
    string? Address,
    DateOnly? DateOfBirth,
    DateTime? ProfileUpdatedAt);
