using System.Net;
using System.Security.Cryptography;
using AutoMapper;
using FluentValidation;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Npgsql;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using SmartPool.Application.Features.ManageStaffs.Commands.AddEmployee;
using SmartPool.Application.Features.ManageStaffs.Commands.UpdateStaff;
using SmartPool.Application.Features.ManageStaffs.Queries.GetStaffEdit;
using SmartPool.Application.Features.ManageStaffs.Mapping;
using SmartPool.Application.Features.ManageStaffs.Onboarding;
using SmartPool.Application.Features.ManageStaffs.WorkPatterns;
using SmartPool.Application.Interfaces.Services;
using SmartPool.Domain.Entities;
using SmartPool.Infrastructure.Persistence.DbContext;
using SmartPool.Infrastructure.Persistence.Repositories.Staff;
using SmartPool.Infrastructure.Services;
using SmartPool.Infrastructure.StaffOnboarding;

namespace SmartPool.IntegrationTests.ManageStaffs;

// Plan §7 check 1 (bounded): add creates account + legal profile + initial contract
// and mails the personal address; duplicate identity leaves no partial account and
// sends no mail. Fake mail and storage only.
public sealed class AddEmployeeAcceptanceIntegrationTests
{
    private static readonly Guid ShiftId = Guid.Parse("55555555-5555-5555-5555-555555555555");

    [Fact]
    public async Task ProfileEmailDatabaseRaceMapsToFieldErrorAndRollsBack()
    {
        var race = new CompetingEmailWrite(IntegrationTestDatabase.GetConnectionString());
        var options = new DbContextOptionsBuilder<SmartPoolDbContext>(await IntegrationTestDatabase.CreateOptionsAsync())
            .AddInterceptors(race).Options;
        await using var context = new SmartPoolDbContext(options);
        // EnsureCreated omits expression indexes. Install the exact existing migration constraint.
        await context.Database.ExecuteSqlRawAsync("CREATE UNIQUE INDEX IF NOT EXISTS users_email_lower_key ON smart_pool.users (lower(btrim(email)))");
        if (!await context.Shifts.AnyAsync(x => x.Id == ShiftId))
        {
            context.Shifts.Add(new Shift { Id = ShiftId, Name = "Acceptance shift", StartTime = new(9, 0), EndTime = new(12, 0), IsActive = true });
            await context.SaveChangesAsync();
        }
        var operations = await CreateOperationsAsync(context, new RecordingMail());
        var request = await BuildRequest(context, $"race-original-{Guid.NewGuid():N}@example.test");
        var created = await operations.AddEmployeeAsync(request, default);
        context.ChangeTracker.Clear();
        var read = await operations.GetStaffEditAsync(new(created.UserId, request.ActorId), default);
        race.TargetUserId = created.UserId;
        race.Email = $"race-{Guid.NewGuid():N}@example.test";
        var failure = await Assert.ThrowsAsync<ValidationException>(() => operations.UpdateStaffAsync(new()
        {
            UserId = created.UserId, ActorId = request.ActorId,
            Details = new() { PersonalEmail = race.Email, Gender = "FEMALE" },
            ExpectedUpdatedAt = read.ExpectedUpdatedAt, ExpectedUserUpdatedAt = read.ExpectedUserUpdatedAt,
            ExpectedProfileUpdatedAt = read.ExpectedProfileUpdatedAt, ExpectedProfileExists = read.ExpectedProfileExists,
            ExpectedLegalProfileVersion = read.ExpectedLegalProfileVersion
        }, default));
        Assert.True(race.Inserted);
        Assert.Contains(failure.Errors, x => x.PropertyName == "Details.PersonalEmail");
        context.ChangeTracker.Clear();
        var after = await operations.GetStaffEditAsync(new(created.UserId, request.ActorId), default);
        Assert.Equal(read.Details.PersonalEmail, after.Details.PersonalEmail);
        Assert.Equal(read.Details.Gender, after.Details.Gender);
        Assert.Equal(read.ExpectedUserUpdatedAt, after.ExpectedUserUpdatedAt);
        Assert.Equal(read.ExpectedProfileUpdatedAt, after.ExpectedProfileUpdatedAt);
    }

    // Commit a competing mixed-case address after application preflight, before database save.
    private sealed class CompetingEmailWrite(string connectionString) : SaveChangesInterceptor
    {
        public Guid TargetUserId { get; set; }
        public string? Email { get; set; }
        public bool Inserted { get; private set; }
        public override async ValueTask<InterceptionResult<int>> SavingChangesAsync(
            DbContextEventData eventData, InterceptionResult<int> result, CancellationToken cancellationToken = default)
        {
            if (Email is null || Inserted) return result;
            await using var connection = new NpgsqlConnection(connectionString);
            await connection.OpenAsync(cancellationToken);
            await using var command = new NpgsqlCommand("""
                INSERT INTO smart_pool.users (id, role_id, password_hash, email, status, is_deleted, created_at, updated_at)
                SELECT @id, role_id, 'synthetic-only', @email, 'Active', false, now(), now()
                FROM smart_pool.users WHERE id = @target
                """, connection);
            command.Parameters.AddWithValue("id", Guid.NewGuid());
            command.Parameters.AddWithValue("email", " " + Email.ToUpperInvariant() + " ");
            command.Parameters.AddWithValue("target", TargetUserId);
            Assert.Equal(1, await command.ExecuteNonQueryAsync(cancellationToken));
            Inserted = true;
            return result;
        }
    }

    [Fact]
    public async Task ProfileEditRoundTripRejectsStaleAndDuplicateValuesWithoutAccessChanges()
    {
        await using var context = await CreateContextAsync();
        var operations = await CreateOperationsAsync(context, new RecordingMail());
        var first = await BuildRequest(context, $"edit-{Guid.NewGuid():N}@example.test");
        var second = await BuildRequest(context, $"edit-{Guid.NewGuid():N}@example.test");
        var one = await operations.AddEmployeeAsync(first, CancellationToken.None);
        await operations.AddEmployeeAsync(second, CancellationToken.None);
        context.ChangeTracker.Clear();
        var read = await operations.GetStaffEditAsync(new(one.UserId, first.ActorId), default);
        var user = await context.Users.Include(x => x.UserProfile).SingleAsync(x => x.Id == one.UserId);
        var employee = await context.Employees.SingleAsync(x => x.UserId == one.UserId);
        var oldStatus = employee.Status;
        var oldAccess = user.Status;
        var oldHash = user.PasswordHash;
        UpdateStaffCommand Edit(StaffEditDetails details) => new() {
            ActorId = first.ActorId, UserId = one.UserId, Status = oldStatus ?? "", Details = details,
            ExpectedUpdatedAt = read.ExpectedUpdatedAt, ExpectedUserUpdatedAt = read.ExpectedUserUpdatedAt,
            ExpectedProfileUpdatedAt = read.ExpectedProfileUpdatedAt, ExpectedProfileExists = read.ExpectedProfileExists,
            ExpectedLegalProfileVersion = read.ExpectedLegalProfileVersion
        };
        var duplicateEmail = await Assert.ThrowsAsync<ValidationException>(() => operations.UpdateStaffAsync(Edit(new() { PersonalEmail = second.PersonalEmail.ToUpperInvariant() }), default));
        Assert.Contains(duplicateEmail.Errors, x => x.PropertyName == "Details.PersonalEmail");
        var duplicateCccd = await Assert.ThrowsAsync<ValidationException>(() => operations.UpdateStaffAsync(Edit(new() { CccdNumber = second.CccdNumber }), default));
        Assert.Contains(duplicateCccd.Errors, x => x.PropertyName == "Details.CccdNumber");
        var corrected = "11" + RandomNumberGenerator.GetInt32(10000, 99999) + RandomNumberGenerator.GetInt32(10000, 99999);
        var newEmail = $"NEW-{Guid.NewGuid():N}@example.test";
        await operations.UpdateStaffAsync(Edit(new() { Gender = "FEMALE", PersonalEmail = newEmail, CccdNumber = corrected }), default);
        context.ChangeTracker.Clear();
        var after = await operations.GetStaffEditAsync(new(one.UserId, first.ActorId), default);
        user = await context.Users.AsNoTracking().SingleAsync(x => x.Id == one.UserId);
        employee = await context.Employees.AsNoTracking().SingleAsync(x => x.UserId == one.UserId);
        Assert.Equal("FEMALE", after.Details.Gender);
        Assert.Equal(newEmail.ToLowerInvariant(), after.Details.PersonalEmail);
        Assert.Equal(corrected, after.Details.CccdNumber);
        Assert.Equal(corrected, after.Details.TaxIdentifier);
        Assert.Equal(first.PermanentAddress, after.Details.PermanentAddress);
        Assert.Equal(oldStatus, employee.Status);
        Assert.Equal(oldAccess, user.Status);
        Assert.Equal(oldHash, user.PasswordHash);
        var stale = await Assert.ThrowsAsync<StaffOnboardingException>(() => operations.UpdateStaffAsync(Edit(new() { Gender = "MALE" }), default));
        Assert.Equal("STALE_VERSION", stale.Code);
        read = after;
        await operations.UpdateStaffAsync(Edit(new() { Gender = "MALE" }), default);
        var genderOnly = await operations.GetStaffEditAsync(new(one.UserId, first.ActorId), default);
        Assert.Equal(after.ExpectedLegalProfileVersion, genderOnly.ExpectedLegalProfileVersion);
    }

    [Fact]
    public async Task AddEmployeeCreatesAccountProfileAndContractThenMailsPersonalAddress()
    {
        await using var context = await CreateContextAsync();
        var mail = new RecordingMail();
        var operations = await CreateOperationsAsync(context, mail);
        var personalEmail = $"accept-{Guid.NewGuid():N}@example.test";

        var response = await operations.AddEmployeeAsync(await BuildRequest(context, personalEmail), CancellationToken.None);

        Assert.Equal("SENT", response.EmailDelivery);
        Assert.Matches(@"^[a-z]+[0-9]{3}@smartpool\.com$", response.CompanyEmail);
        Assert.Equal(personalEmail, Assert.Single(mail.Recipients));
        Assert.Equal("Tài khoản SmartPool của bạn", mail.Subject);
        Assert.Contains(WebUtility.HtmlEncode(response.CompanyEmail), mail.Body);
        Assert.Contains("đổi mật khẩu", mail.Body);

        var user = await context.Users.Include(item => item.UserProfile).SingleAsync(item => item.Id == response.UserId);
        Assert.True(user.MustChangePassword);
        Assert.NotNull(user.TemporaryPasswordExpiresAtUtc);
        Assert.Equal("SALES", (await context.Roles.SingleAsync(item => item.Id == user.RoleId)).Name);
        Assert.Equal("Nguyễn Văn Phúc Lâm", user.UserProfile!.FullName);
        Assert.NotNull(await context.Employees.SingleOrDefaultAsync(item => item.UserId == response.UserId && item.CompanyEmail == response.CompanyEmail));
        var legal = await context.StaffLegalProfiles.SingleAsync(item => item.EmployeeId == response.UserId);
        Assert.Equal(1, legal.Version);
        var pattern = await context.StaffScheduleChanges.SingleAsync(item => item.EmployeeId == response.UserId && item.Kind == "Pattern");
        var shifts = await context.EmployeeShifts.Where(item => item.EmployeeId == response.UserId).ToListAsync();
        Assert.Equal(31, shifts.Count);
        Assert.All(shifts, item => Assert.Equal(pattern.Id, item.ScheduleChangeId));
    }

    [Fact]
    public async Task DuplicateCccdLeavesNoPartialAccountAndSendsNoMail()
    {
        await using var context = await CreateContextAsync();
        var mail = new RecordingMail();
        var operations = await CreateOperationsAsync(context, mail);
        var first = await BuildRequest(context, $"accept-{Guid.NewGuid():N}@example.test");
        await operations.AddEmployeeAsync(first, CancellationToken.None);
        var usersBefore = await context.Users.CountAsync();
        var employeesBefore = await context.Employees.CountAsync();
        mail.Recipients.Clear();

        var duplicate = await BuildRequest(context, $"accept-{Guid.NewGuid():N}@example.test");
        duplicate.CccdNumber = first.CccdNumber;
        var failure = await Assert.ThrowsAsync<ValidationException>(() =>
            operations.AddEmployeeAsync(duplicate, CancellationToken.None));

        Assert.Contains(failure.Errors, error => error.PropertyName == "CccdNumber");
        Assert.Equal(usersBefore, await context.Users.CountAsync());
        Assert.Equal(employeesBefore, await context.Employees.CountAsync());
        Assert.Empty(mail.Recipients);
    }

    private static async Task<SmartPoolDbContext> CreateContextAsync()
    {
        var options = await IntegrationTestDatabase.CreateOptionsAsync();
        var context = new SmartPoolDbContext(options);
        if (!await context.Shifts.AnyAsync(item => item.Id == ShiftId))
        {
            context.Shifts.Add(new Shift { Id = ShiftId, Name = "Acceptance shift", StartTime = new TimeOnly(9, 0), EndTime = new TimeOnly(12, 0), IsActive = true });
            await context.SaveChangesAsync();
        }
        return context;
    }

    private static async Task<StaffOperations> CreateOperationsAsync(SmartPoolDbContext context, RecordingMail mail)
    {
        var clock = TimeProvider.System;
        var services = new StaffOnboardingServices(Options.Create(new StaffOnboardingOptions
        {
            EncryptionKey = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32)),
            IdentityKey = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32)),
            FrontendOrigin = "https://example.test"
        }), new PasswordHasherService(), mail);
        var mapper = new MapperConfiguration(config => config.AddProfile<StaffMappingProfile>(), NullLoggerFactory.Instance).CreateMapper();
        return new StaffOperations(context, mapper, clock,
            Options.Create(new SmartPool.Application.Features.ManageStaffs.AttendanceQr.StaffAttendanceQrSettings()),
            services, new EphemeralDataProtectionProvider(), new CompanyEmailGenerator(context), new TempPasswordGenerator());
    }

    private static async Task<AddEmployeeCommand> BuildRequestAsync(SmartPoolDbContext context, string personalEmail) =>
        new()
        {
            ActorId = (await context.Users.FirstAsync(item => item.Role!.Name == "ADMIN" && item.IsDeleted != true)).Id,
            FullName = "Nguyễn Văn Phúc Lâm",
            Gender = "MALE",
            DateOfBirth = new DateOnly(1995, 1, 1),
            Phone = "09" + RandomNumberGenerator.GetInt32(10000000, 99999999),
            PersonalEmail = personalEmail,
            CccdNumber = "00" + RandomNumberGenerator.GetInt32(10000, 99999) + RandomNumberGenerator.GetInt32(10000, 99999),
            PermanentAddress = "1 Synthetic Street",
            TemporaryAddress = null,
            JoinDate = DateOnly.FromDateTime(DateTime.UtcNow.Date).AddDays(2),
            TargetRole = "SALES",
            Contract = null // set below
        };

    private static async Task<AddEmployeeCommand> BuildRequest(SmartPoolDbContext context, string personalEmail)
    {
        var request = await BuildRequestAsync(context, personalEmail);
        request.Contract = new ContractTerms("FULL_TIME", request.JoinDate, request.JoinDate.AddDays(30),
            Enumerable.Range(1, 7).Select(day => new WeeklyPatternDay(day, [ShiftId])).ToList());
        return request;
    }

    private sealed class RecordingMail : IEmailService
    {
        public List<string> Recipients { get; } = [];
        public string Subject { get; private set; } = "";
        public string Body { get; private set; } = "";
        public Task SendAsync(string to, string subject, string htmlBody, CancellationToken cancellationToken = default)
        { Recipients.Add(to); Subject = subject; Body = htmlBody; return Task.CompletedTask; }
        public Task SendAsync(IEnumerable<string> recipients, string subject, string htmlBody, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();
    }

}
