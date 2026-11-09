using AutoMapper;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using SmartPool.Application.Common.Time;
using SmartPool.Application.Features.ManageStaffs.AttendanceQr;
using SmartPool.Application.Features.ManageStaffs.Mapping;
using SmartPool.Application.Features.ManageStaffs.WorkPatterns;
using SmartPool.Application.Interfaces.Services;
using SmartPool.Domain.Entities;
using SmartPool.Infrastructure.Persistence.DbContext;
using SmartPool.Infrastructure.Persistence.Repositories.Staff;
using SmartPool.Infrastructure.Services;
using SmartPool.Infrastructure.StaffOnboarding;

namespace SmartPool.IntegrationTests;

internal sealed class StaffWorkPatternTestFixture : IAsyncDisposable
{
    private StaffWorkPatternTestFixture(SmartPoolDbContext context, StaffOperations operations, FakeClock clock,
        Guid employeeId, Guid adminId,
        Guid morningShiftId, Guid afternoonShiftId, Guid nightShiftId)
    {
        Context = context;
        Operations = operations;
        Clock = clock;
        EmployeeId = employeeId;
        AdminId = adminId;
        MorningShiftId = morningShiftId;
        AfternoonShiftId = afternoonShiftId;
        NightShiftId = nightShiftId;
    }

    public SmartPoolDbContext Context { get; }
    public StaffOperations Operations { get; }
    public FakeClock Clock { get; }
    public Guid EmployeeId { get; }
    public Guid AdminId { get; }
    public Guid MorningShiftId { get; }
    public Guid AfternoonShiftId { get; }
    public Guid NightShiftId { get; }
    public DateOnly Today => VietnamTimeBoundary.GetDate(Clock);

    public static async Task<StaffWorkPatternTestFixture> CreateAsync(DateTimeOffset? utcNow = null)
    {
        var options = await IntegrationTestDatabase.CreateOptionsAsync();
        var context = new SmartPoolDbContext(options);
        var adminId = await context.Users.Where(item => item.Role != null && item.Role.Name == "ADMIN")
            .Select(item => item.Id).FirstAsync();
        var salesRoleId = await context.Roles.Where(item => item.Name == "SALES" && item.IsDeleted != true)
            .Select(item => item.Id).FirstAsync();
        var clock = new FakeClock(utcNow ?? new DateTimeOffset(2026, 11, 6, 8, 0, 0, TimeSpan.Zero));
        var employeeId = Guid.NewGuid();
        var morningShiftId = Guid.NewGuid();
        var afternoonShiftId = Guid.NewGuid();
        var nightShiftId = Guid.NewGuid();
        var user = new User { Id = employeeId, Username = $"pattern-{Guid.NewGuid():N}",
            Email = $"pattern-{Guid.NewGuid():N}@example.test", Phone = $"09{Random.Shared.Next(10_000_000, 99_999_999)}",
            RoleId = salesRoleId, Status = "Active", IsDeleted = false, PasswordHash = "synthetic-test-only",
            CreatedAt = clock.GetUtcNow().UtcDateTime, UpdatedAt = clock.GetUtcNow().UtcDateTime };
        context.Users.Add(user);
        context.UserProfiles.Add(new UserProfile { UserId = employeeId, FullName = "Synthetic schedule employee", UpdatedAt = clock.GetUtcNow().UtcDateTime });
        context.Employees.Add(new Employee { UserId = employeeId, User = user, Status = "Working", JoinDate = VietnamTimeBoundary.GetDate(clock),
            CreatedAt = clock.GetUtcNow().UtcDateTime, UpdatedAt = clock.GetUtcNow().UtcDateTime });
        context.Shifts.AddRange(
            new Shift { Id = morningShiftId, Name = "Synthetic morning", StartTime = new(6, 0), EndTime = new(12, 0), IsActive = true },
            new Shift { Id = afternoonShiftId, Name = "Synthetic afternoon", StartTime = new(13, 0), EndTime = new(19, 0), IsActive = true },
            new Shift { Id = nightShiftId, Name = "Synthetic overnight", StartTime = new(22, 0), EndTime = new(2, 0), IsActive = true });
        await context.SaveChangesAsync();

        var storage = new FakeStorage();
        var mail = new FakeMail();
        var cleanup = new StaffDocumentCleanup(context, storage, clock);
        var optionsForServices = Options.Create(new StaffOnboardingOptions
        {
            EncryptionKey = Convert.ToBase64String(System.Security.Cryptography.RandomNumberGenerator.GetBytes(32)),
            IdentityKey = Convert.ToBase64String(System.Security.Cryptography.RandomNumberGenerator.GetBytes(32)),
            FrontendOrigin = "https://example.test"
        });
        var onboarding = new StaffOnboardingServices(optionsForServices, new PasswordHasherService(), mail, storage, cleanup);
        var mapper = new MapperConfiguration(config => config.AddProfile<StaffMappingProfile>(), NullLoggerFactory.Instance).CreateMapper();
        var operations = new StaffOperations(context, mapper, clock, Options.Create(new StaffAttendanceQrSettings()), onboarding);
        return new(context, operations, clock, employeeId, adminId, morningShiftId, afternoonShiftId, nightShiftId);
    }

    public ContractTerms Contract(DateOnly endDate, IReadOnlyList<Guid>? shifts = null) => new("FULL_TIME", Today, endDate,
        Enumerable.Range(1, 7).Select(day => new WeeklyPatternDay(day, shifts ?? [MorningShiftId])).ToList());

    public WorkPatternCommand PatternCommand(ContractTerms contract, DateOnly effectiveFrom, long expectedVersion) =>
        new() { EmployeeId = EmployeeId, ActorId = AdminId, Contract = contract, EffectiveFrom = effectiveFrom, ExpectedVersion = expectedVersion };

    public async Task<ScheduleResult> PreviewAsync(WorkPatternCommand command)
    {
        command.Action = WorkPatternAction.Preview;
        return Assert.IsType<ScheduleResult>(await Operations.ExecuteWorkPatternAsync(command, default));
    }

    public async Task<WorkPatternResponse> ConfirmAsync(WorkPatternCommand command, ScheduleResult preview, Guid operationId)
    {
        command.Action = WorkPatternAction.Confirm;
        command.PreviewToken = preview.PreviewToken;
        command.OperationId = operationId;
        return Assert.IsType<WorkPatternResponse>(await Operations.ExecuteWorkPatternAsync(command, default));
    }

    public async Task<EmployeeShift> AddAssignmentAsync(DateOnly date, Guid shiftId, string status = "Scheduled",
        DateTime? checkIn = null, Guid? employeeId = null)
    {
        var assignment = new EmployeeShift { Id = Guid.NewGuid(), EmployeeId = employeeId ?? EmployeeId, ShiftId = shiftId,
            WorkDate = date, Status = status, CheckInTime = checkIn, CheckInMethod = checkIn.HasValue ? "Manual" : null,
            CreatedAt = Clock.GetUtcNow().UtcDateTime, UpdatedAt = Clock.GetUtcNow().UtcDateTime };
        Context.EmployeeShifts.Add(assignment);
        await Context.SaveChangesAsync();
        return assignment;
    }

    public async Task<Guid> AddReplacementEmployeeAsync()
    {
        var roleId = await Context.Roles.Where(item => item.Name == "SALES" && item.IsDeleted != true)
            .Select(item => item.Id).FirstAsync();
        var now = Clock.GetUtcNow().UtcDateTime;
        var employeeId = Guid.NewGuid();
        var user = new User { Id = employeeId, Username = $"replacement-{Guid.NewGuid():N}",
            Email = $"replacement-{Guid.NewGuid():N}@example.test", Phone = $"09{Random.Shared.Next(10_000_000, 100_000_000)}",
            RoleId = roleId, Status = "Active", IsDeleted = false, PasswordHash = "synthetic-test-only",
            CreatedAt = now, UpdatedAt = now };
        Context.Users.Add(user);
        Context.UserProfiles.Add(new UserProfile { UserId = employeeId, FullName = "Synthetic replacement", UpdatedAt = now });
        Context.Employees.Add(new Employee { UserId = employeeId, User = user, Status = "Working",
            JoinDate = Today, CreatedAt = now, UpdatedAt = now });
        await Context.SaveChangesAsync();
        return employeeId;
    }

    public async Task<PoolDutySchedule> AddDutyAsync(DateOnly date, Guid shiftId)
    {
        var duty = new PoolDutySchedule { Id = Guid.NewGuid(), DutyDate = date, ShiftId = shiftId,
            AssignedEmployeeId = EmployeeId, Status = "Scheduled", AssignmentType = "Auto",
            CreatedAt = Clock.GetUtcNow().UtcDateTime, UpdatedAt = Clock.GetUtcNow().UtcDateTime };
        Context.PoolDutySchedules.Add(duty);
        await Context.SaveChangesAsync();
        return duty;
    }

    public ValueTask DisposeAsync() => Context.DisposeAsync();

    public sealed class FakeClock(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }

    private sealed class FakeMail : IEmailService
    {
        public Task SendAsync(string to, string subject, string htmlBody, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task SendAsync(IEnumerable<string> recipients, string subject, string htmlBody, CancellationToken cancellationToken = default) => Task.CompletedTask;
    }

    private sealed class FakeStorage : IStaffDocumentStorage
    {
        public Task<StaffStoredAsset> UploadAsync(StaffOnboardingDocument document, byte[] bytes, CancellationToken cancellationToken) =>
            throw new NotSupportedException();
        public Task<byte[]> ReadAsync(StaffOnboardingDocument document, CancellationToken cancellationToken) =>
            throw new NotSupportedException();
        public Task DeleteAsync(StaffOnboardingDocument document, CancellationToken cancellationToken) => Task.CompletedTask;
        public string AvatarUrl(StaffOnboardingDocument document) => "https://example.test/synthetic.png";
    }
}
