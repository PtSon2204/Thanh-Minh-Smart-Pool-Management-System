using System.Net;
using System.Security.Cryptography;
using System.Text.RegularExpressions;
using AutoMapper;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using SmartPool.Application.Features.ManageStaffs.AttendanceQr;
using SmartPool.Application.Features.ManageStaffs.Commands.CreateStaff;
using SmartPool.Application.Features.ManageStaffs.Mapping;
using SmartPool.Application.Features.ManageStaffs.Onboarding;
using SmartPool.Application.Interfaces.Services;
using SmartPool.Domain.Entities;
using SmartPool.Infrastructure.Persistence.DbContext;
using SmartPool.Infrastructure.Persistence.Repositories.Staff;
using SmartPool.Infrastructure.Services;
using SmartPool.Infrastructure.StaffOnboarding;
using SmartPool.Application.Common.Time;
using SmartPool.Application.Features.ManageStaffs.WorkPatterns;

namespace SmartPool.IntegrationTests;

public sealed class StaffOnboardingIntegrationTests
{
    [Fact]
    public async Task OwnWorkspaceAndDocumentsCannotSelectAnotherEmployeesRecords()
    {
        await using var fixture = await Fixture.CreateAsync();
        var workspace = await fixture.InviteAsync();
        workspace = await fixture.Submit(workspace, fixture.Credentials(workspace));
        workspace = await fixture.Approve(workspace);
        var employeeId = workspace.Invitation!.CompletedUserId!.Value;
        var foreign = await fixture.InviteAsync();
        var foreignCredentials = fixture.Credentials(foreign);
        foreign = await fixture.Run(new()
        {
            Action = OnboardingAction.Upload, InvitationId = foreignCredentials.Id, Token = foreignCredentials.Token,
            ExpectedVersion = foreign.Revision!.Version, Upload = new("FRONT", "image/png", [137, 80, 78, 71, 13, 10, 26, 10])
        });
        var own = await fixture.Run(new()
        {
            Action = OnboardingAction.Own, ActorId = employeeId, EmployeeId = fixture.AdminId
        });
        Assert.Equal(workspace.Revision!.Id, own.ApprovedRevision!.Id);
        var document = Assert.Single(foreign.Revision!.Documents);
        var denied = await Assert.ThrowsAsync<StaffOnboardingException>(() => fixture.Operations.ReadStaffDocumentAsync(
            new(document.Id, employeeId, false, null, null), default));
        Assert.Equal("DOCUMENT_FORBIDDEN", denied.Code);
        Assert.Equal(403, denied.Status);
    }

    [Fact]
    public async Task ResendIsBlockedDuringReviewAndAllowedAfterChangesRequested()
    {
        await using var fixture = await Fixture.CreateAsync();
        var workspace = await fixture.InviteAsync();
        var originalCredentials = fixture.Credentials(workspace);
        workspace = await fixture.Submit(workspace, originalCredentials);
        var original = await fixture.Context.StaffInvitations.AsNoTracking().SingleAsync(item => item.Id == originalCredentials.Id);
        var denied = await Assert.ThrowsAsync<StaffOnboardingException>(() => fixture.Run(new()
        {
            Action = OnboardingAction.Resend, ActorId = fixture.AdminId, InvitationId = originalCredentials.Id,
            ExpectedVersion = workspace.Invitation!.Version
        }));
        Assert.Equal("RESEND_NOT_ALLOWED", denied.Code);
        var unchanged = await fixture.Context.StaffInvitations.AsNoTracking().SingleAsync(item => item.Id == originalCredentials.Id);
        Assert.Equal(original.TokenDigest, unchanged.TokenDigest);
        Assert.Equal(original.ExpiresAtUtc, unchanged.ExpiresAtUtc);
        workspace = await fixture.Run(new()
        {
            Action = OnboardingAction.RequestChanges, ActorId = fixture.AdminId, RevisionId = workspace.Revision!.Id,
            ExpectedVersion = workspace.Revision.Version, Reason = "Synthetic correction needed"
        });
        workspace = await fixture.Run(new()
        {
            Action = OnboardingAction.Resend, ActorId = fixture.AdminId, InvitationId = originalCredentials.Id,
            ExpectedVersion = workspace.Invitation!.Version
        });
        var replacement = fixture.Credentials(workspace);
        Assert.NotEqual(originalCredentials.Token, replacement.Token);
        Assert.Equal("CHANGES_REQUESTED", workspace.Revision!.State);
        await Assert.ThrowsAsync<StaffOnboardingException>(() => fixture.Run(new()
        {
            Action = OnboardingAction.Inspect, InvitationId = originalCredentials.Id, Token = originalCredentials.Token
        }));
        var reopened = await fixture.Run(new() { Action = OnboardingAction.Inspect, InvitationId = replacement.Id, Token = replacement.Token });
        Assert.Equal("CHANGES_REQUESTED", reopened.Revision!.State);
    }

    [Fact]
    public async Task InvitationPrefillSurvivesInspectionAndEmployeeDraftCorrections()
    {
        await using var fixture = await Fixture.CreateAsync();
        var email = $"synthetic-prefill-{Guid.NewGuid():N}@example.test";
        var command = new OnboardingCommand { Action = OnboardingAction.Create, ActorId = fixture.AdminId, Email = email,
            TargetRole = "SALES", JoinDate = VietnamTimeBoundary.GetDate(fixture.Clock).AddDays(-1),
            Contract = fixture.ContractFor(VietnamTimeBoundary.GetDate(fixture.Clock).AddDays(-1), VietnamTimeBoundary.GetDate(fixture.Clock).AddDays(-1)),
            FullName = " Synthetic Invite Name ", Phone = "+84901234567" };
        var failure = await Assert.ThrowsAsync<StaffOnboardingException>(() => fixture.Run(command));
        Assert.Equal("INVALID_JOIN_DATE", failure.Code);
        Assert.False(await fixture.Context.StaffInvitations.AnyAsync(item => item.Email == email));
        command.JoinDate = VietnamTimeBoundary.GetDate(fixture.Clock);
        command.Contract = fixture.ContractFor(command.JoinDate.Value, command.JoinDate.Value);
        var workspace = await fixture.Run(command);
        var credentials = fixture.Credentials(workspace);
        fixture.Context.ChangeTracker.Clear();
        var inspected = await fixture.Run(new() { Action = OnboardingAction.Inspect, InvitationId = credentials.Id, Token = credentials.Token });
        Assert.Equal("Synthetic Invite Name", inspected.Revision!.Profile.FullName);
        Assert.Equal("0901234567", inspected.Revision.Profile.Phone);
        await fixture.Run(new() { Action = OnboardingAction.Draft, InvitationId = credentials.Id, Token = credentials.Token,
            ExpectedVersion = inspected.Revision.Version, Profile = inspected.Revision.Profile with { FullName = "Employee corrected name", Phone = "0907654321" } });
        fixture.Context.ChangeTracker.Clear();
        var reopened = await fixture.Run(new() { Action = OnboardingAction.Inspect, InvitationId = credentials.Id, Token = credentials.Token });
        Assert.Equal("Employee corrected name", reopened.Revision!.Profile.FullName);
        Assert.Equal("0907654321", reopened.Revision.Profile.Phone);
        Assert.Equal(StaffInvitationEmail.Subject, fixture.Mail.Subject);
        Assert.Contains("Hoàn thiện hồ sơ nhân viên", fixture.Mail.Body);
    }

    [Fact]
    public async Task ApprovalUsesExistingRoleRegardlessOfCasing()
    {
        await using var fixture = await Fixture.CreateAsync();
        var role = new Role { Id = Guid.NewGuid(), Name = "Coach", IsDeleted = false };
        fixture.Context.Roles.Add(role);
        await fixture.Context.SaveChangesAsync();
        var workspace = await fixture.Run(new()
        {
            Action = OnboardingAction.Create, ActorId = fixture.AdminId,
            Email = $"synthetic-role-{Guid.NewGuid():N}@example.test", TargetRole = "COACH", JoinDate = VietnamTimeBoundary.GetDate(fixture.Clock),
            Contract = fixture.ContractFor(VietnamTimeBoundary.GetDate(fixture.Clock), VietnamTimeBoundary.GetDate(fixture.Clock).AddDays(8)),
            FullName = "Synthetic staff", Phone = "0901234567"
        });
        var credentials = fixture.Credentials(workspace);
        var inspected = await fixture.Run(new() { Action = OnboardingAction.Inspect, InvitationId = credentials.Id, Token = credentials.Token });
        var visibleShift = Assert.Single(inspected.Invitation!.Shifts!);
        Assert.Equal("Synthetic onboarding", visibleShift.Name);
        Assert.Equal(new TimeOnly(6, 0), visibleShift.StartTime);
        workspace = await fixture.Submit(workspace, credentials);
        workspace = await fixture.Approve(workspace);
        var user = await fixture.Context.Users.SingleAsync(item => item.Id == workspace.Invitation!.CompletedUserId);
        Assert.Equal("APPROVED", workspace.Revision!.State);
        Assert.Equal(role.Id, user.RoleId);
        Assert.Equal(8, workspace.ScheduleResult!.CreatedCount);
        var completedInvitation = workspace.Invitation!;
        var completedEmployeeId = completedInvitation.CompletedUserId!.Value;
        var assignments = await fixture.Context.EmployeeShifts.Where(item => item.EmployeeId == completedEmployeeId).OrderBy(item => item.WorkDate).ToListAsync();
        Assert.Equal(8, assignments.Count);
        Assert.Equal(completedInvitation.Contract!.StartDate.AddDays(1), assignments[0].WorkDate);
        Assert.Equal(completedInvitation.Contract.EndDate, assignments[^1].WorkDate);
        Assert.False(string.IsNullOrWhiteSpace(workspace.Revision.ReviewerName));
        var employeeWorkspace = await fixture.Run(new() { Action = OnboardingAction.EmployeeDetail, ActorId = fixture.AdminId, EmployeeId = completedEmployeeId });
        Assert.Equal(completedInvitation.Contract.EndDate, employeeWorkspace.Contract!.EndDate);
        Assert.Equal(visibleShift.Id, Assert.Single(employeeWorkspace.Shifts!).Id);
        var delivery = await fixture.Context.StaffScheduleDeliveries.SingleAsync(item => item.OperationId == completedInvitation.Id);
        Assert.Equal("Pending", delivery.Status);
        Assert.Equal(completedEmployeeId, delivery.RecipientId);
        Assert.Contains("06:00-12:00", delivery.HtmlBody);
        Assert.Single(await fixture.Context.Notifications.Where(item => item.UserId == delivery.RecipientId && item.Type == "StaffSchedule").ToListAsync());
        Assert.Equal("Coach", (await fixture.Context.Roles.SingleAsync(item => item.Id == role.Id)).Name);
    }

    [Fact]
    public async Task LegacyInvitationWithoutContractCanBeApprovedWithoutScheduleGeneration()
    {
        await using var fixture = await Fixture.CreateAsync();
        var workspace = await fixture.InviteAsync();
        var credentials = fixture.Credentials(workspace);
        workspace = await fixture.Submit(workspace, credentials);
        var invitation = await fixture.Context.StaffInvitations.SingleAsync(item => item.Id == credentials.Id);
        invitation.ContractJson = null;
        await fixture.Context.SaveChangesAsync();

        var approved = await fixture.Approve(workspace);

        Assert.Equal("APPROVED", approved.Revision!.State);
        Assert.Equal("COMPLETED", approved.Invitation!.Status);
        Assert.Equal("skipped_legacy_no_contract", approved.ScheduleResult!.Status);
        Assert.Equal("legacy_contract_missing", Assert.Single(approved.ScheduleResult.Details).Code);
        var employeeId = approved.Invitation.CompletedUserId!.Value;
        Assert.False(await fixture.Context.StaffWorkPatterns.AnyAsync(item => item.EmployeeId == employeeId));
        Assert.False(await fixture.Context.EmployeeShifts.AnyAsync(item => item.EmployeeId == employeeId));
    }

    [Fact]
    public async Task CccdUploadsUseAuthenticatedDeliveryWithoutAManualToggle()
    {
        await using var fixture = await Fixture.CreateAsync();
        var workspace = await fixture.InviteAsync();
        var credentials = fixture.Credentials(workspace);
        foreach (var purpose in new[] { "FRONT", "BACK" })
        {
            workspace = await fixture.Run(new()
            {
                Action = OnboardingAction.Upload, InvitationId = credentials.Id, Token = credentials.Token,
                ExpectedVersion = workspace.Revision!.Version, Upload = new(purpose, "image/png", [137, 80, 78, 71, 13, 10, 26, 10])
            });
            Assert.Equal("authenticated", fixture.Storage.LastDeliveryType);
        }
        Assert.Equal(2, workspace.Revision!.Documents.Count);
        workspace = await fixture.Run(new()
        {
            Action = OnboardingAction.Upload, InvitationId = credentials.Id, Token = credentials.Token,
            ExpectedVersion = workspace.Revision!.Version, Upload = new("AVATAR", "image/png", [137, 80, 78, 71, 13, 10, 26, 10])
        });
        Assert.Equal(3, workspace.Revision!.Documents.Count);
        Assert.Equal("upload", fixture.Storage.LastDeliveryType);
        var stored = await fixture.Context.StaffOnboardingDocuments.AsNoTracking().Where(item => item.RevisionId == workspace.Revision.Id).ToListAsync();
        Assert.All(stored.Where(item => item.Purpose != "AVATAR"), item => Assert.Equal("authenticated", item.DeliveryType));
        Assert.All(workspace.Revision.Documents, item => Assert.Equal("AVAILABLE", item.Status));
    }

    [Fact]
    public async Task ProviderUploadFailureReturnsItsCodeAndKeepsCleanupWork()
    {
        await using var fixture = await Fixture.CreateAsync();
        var workspace = await fixture.InviteAsync();
        var credentials = fixture.Credentials(workspace);
        fixture.Storage.FailUpload = true;
        var failure = await Assert.ThrowsAsync<StaffOnboardingException>(() => fixture.Run(new()
        {
            Action = OnboardingAction.Upload, InvitationId = credentials.Id, Token = credentials.Token,
            ExpectedVersion = workspace.Revision!.Version, Upload = new("FRONT", "image/png", [137, 80, 78, 71, 13, 10, 26, 10])
        }));
        Assert.Equal("DOCUMENT_UPLOAD_FAILED", failure.Code);
        Assert.Equal("authenticated", fixture.Storage.LastDeliveryType);
        fixture.Context.ChangeTracker.Clear();
        var document = await fixture.Context.StaffOnboardingDocuments.SingleAsync(item => item.RevisionId == workspace.Revision!.Id);
        Assert.Equal("PENDING", document.DeletionState);
        Assert.NotNull(document.PublicId);
        Assert.Equal("authenticated", document.DeliveryType);
    }

    [Fact]
    public async Task OnboardingReadsDoNotWaitForTheWriteLock()
    {
        await using var fixture = await Fixture.CreateAsync();
        var workspace = await fixture.InviteAsync();
        var credentials = fixture.Credentials(workspace);
        await using var writer = new SmartPoolDbContext(fixture.DbOptions);
        await using var transaction = await writer.Database.BeginTransactionAsync();
        await writer.Database.ExecuteSqlRawAsync("SELECT pg_advisory_xact_lock(704318229012)");
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        var inspected = await fixture.Operations.ExecuteOnboardingAsync(new()
        {
            Action = OnboardingAction.Inspect, InvitationId = credentials.Id, Token = credentials.Token
        }, timeout.Token);
        var detail = await fixture.Operations.ExecuteOnboardingAsync(new()
        {
            Action = OnboardingAction.Detail, ActorId = fixture.AdminId, InvitationId = credentials.Id
        }, timeout.Token);
        Assert.Equal(workspace.Revision!.Id, inspected.Revision!.Id);
        Assert.Equal(workspace.Revision.Id, detail.Revision!.Id);
    }

    [Fact]
    public async Task LateUploadAfterCancellationKeepsLocatorUntilSettledDeletion()
    {
        await using var fixture = await Fixture.CreateAsync();
        var workspace = await fixture.InviteAsync();
        var credentials = fixture.Credentials(workspace);
        fixture.Storage.UploadStarted = new(TaskCreationOptions.RunContinuationsAsynchronously);
        fixture.Storage.ReleaseUpload = new(TaskCreationOptions.RunContinuationsAsynchronously);
        var upload = fixture.Run(new() { Action = OnboardingAction.Upload, InvitationId = credentials.Id, Token = credentials.Token,
            ExpectedVersion = workspace.Revision!.Version, Upload = new("FRONT", "image/png", [137, 80, 78, 71, 13, 10, 26, 10]) });
        await fixture.Storage.UploadStarted.Task.WaitAsync(TimeSpan.FromSeconds(10));
        await using (var other = new SmartPoolDbContext(fixture.DbOptions))
        {
            var cleanup = new StaffDocumentCleanup(other, fixture.Storage, fixture.Clock);
            var services = new StaffOnboardingServices(Options.Create(fixture.Services.Options), new PasswordHasherService(), fixture.Mail, fixture.Storage, cleanup);
            await new StaffOperations(other, fixture.Mapper, fixture.Clock, Options.Create(new StaffAttendanceQrSettings()), services)
                .ExecuteOnboardingAsync(new() { Action = OnboardingAction.Cancel, ActorId = fixture.AdminId, InvitationId = credentials.Id,
                    ExpectedVersion = workspace.Invitation!.Version }, default);
            var pending = await other.StaffOnboardingDocuments.AsNoTracking().SingleAsync(item => item.RevisionId == workspace.Revision.Id);
            Assert.Equal("PENDING", pending.DeletionState);
            Assert.NotNull(pending.PublicId);
            Assert.Null(pending.UploadSettledAtUtc);
        }
        fixture.Storage.ReleaseUpload.SetResult(true);
        await Assert.ThrowsAsync<StaffOnboardingException>(() => upload);
        fixture.Context.ChangeTracker.Clear();
        var settled = await fixture.Context.StaffOnboardingDocuments.SingleAsync(item => item.RevisionId == workspace.Revision.Id);
        Assert.NotNull(settled.PublicId);
        Assert.NotNull(settled.UploadSettledAtUtc);
        fixture.Clock.Now = fixture.Clock.Now.AddMinutes(6);
        await fixture.Cleanup.RunBatchAsync(default);
        fixture.Context.ChangeTracker.Clear();
        settled = await fixture.Context.StaffOnboardingDocuments.SingleAsync(item => item.RevisionId == workspace.Revision.Id);
        Assert.Equal("DELETED", settled.DeletionState);
        Assert.Null(settled.PublicId);
    }

    [Fact]
    public async Task SubmissionChangesApprovalAndRetentionRemainPrivate()
    {
        await using var fixture = await Fixture.CreateAsync();
        var workspace = await fixture.InviteAsync();
        Assert.False(await fixture.Context.Users.AnyAsync(item => item.Email == workspace.Invitation!.Email));
        var credentials = fixture.Credentials(workspace);
        var upload = new StaffDocumentUpload("FRONT", "image/png", Convert.FromBase64String("iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mP8/x8AAwMCAO+a3ioAAAAASUVORK5CYII="));
        workspace = await fixture.Run(new() { Action = OnboardingAction.Upload, InvitationId = credentials.Id, Token = credentials.Token,
            ExpectedVersion = workspace.Revision!.Version, Upload = upload });
        var document = Assert.Single(workspace.Revision!.Documents);
        Assert.Equal(document.UploadedAtUtc.AddDays(30), document.DeleteAfterUtc);
        Assert.Equal("authenticated", fixture.Storage.LastDeliveryType);
        var originalTime = fixture.Clock.Now;
        fixture.Clock.Now = new DateTimeOffset(document.DeleteAfterUtc!.Value);
        var expiry = await Assert.ThrowsAsync<StaffOnboardingException>(() => fixture.Operations.ReadStaffDocumentAsync(new(document.Id, fixture.AdminId, true, null, null), default));
        Assert.Equal("DOCUMENT_UNAVAILABLE", expiry.Code);
        fixture.Clock.Now = originalTime;
        workspace = await fixture.Submit(workspace, credentials);
        Assert.False(await fixture.Context.Employees.AnyAsync(item => item.User.Email == workspace.Invitation!.Email));
        workspace = await fixture.Run(new() { Action = OnboardingAction.RequestChanges, ActorId = fixture.AdminId,
            RevisionId = workspace.Revision!.Id, ExpectedVersion = workspace.Revision.Version, Reason = "Check synthetic address" });
        Assert.Equal("CHANGES_REQUESTED", workspace.Revision!.State);
        workspace = await fixture.Submit(workspace, credentials);
        Assert.Equal(document.DeleteAfterUtc, Assert.Single(workspace.Revision!.Documents).DeleteAfterUtc);
        fixture.Storage.FailDelete = true;
        workspace = await fixture.Approve(workspace);
        Assert.Equal("APPROVED", workspace.Revision!.State);
        var employeeId = workspace.Invitation!.CompletedUserId!.Value;
        Assert.Equal("Working", (await fixture.Context.Employees.SingleAsync(item => item.UserId == employeeId)).Status);
        var account = await fixture.Context.Users.SingleAsync(item => item.Id == employeeId);
        Assert.True(new PasswordHasherService().VerifyPassword(account, account.PasswordHash!, "Synthetic123!"));
        var protectedProfile = await fixture.Context.StaffOnboardingRevisions.Select(item => item.ProtectedProfile).FirstAsync();
        Assert.DoesNotContain("001234567890", protectedProfile);
        await Assert.ThrowsAsync<StaffOnboardingException>(() => fixture.Operations.ReadStaffDocumentAsync(new(document.Id, fixture.AdminId, true, null, null), default));
        fixture.Context.ChangeTracker.Clear();
        var retained = await fixture.Context.StaffOnboardingDocuments.SingleAsync(item => item.Id == document.Id);
        Assert.Equal("PENDING", retained.DeletionState);
        fixture.Storage.FailDelete = false;
        fixture.Clock.Now = fixture.Clock.Now.AddMinutes(6);
        await fixture.Cleanup.RunBatchAsync(default);
        fixture.Context.ChangeTracker.Clear();
        retained = await fixture.Context.StaffOnboardingDocuments.SingleAsync(item => item.Id == document.Id);
        Assert.Equal("DELETED", retained.DeletionState);
        Assert.Null(retained.PublicId);
        var replay = await fixture.Approve(workspace);
        Assert.Equal("ALREADY_APPROVED", replay.Code);
        Assert.Equal(1, await fixture.Context.Employees.CountAsync(item => item.UserId == employeeId));
    }

    [Fact]
    public async Task ConcurrentApprovalCorrectionAndIdentityRacePreserveAccounts()
    {
        await using var fixture = await Fixture.CreateAsync();
        var workspace = await fixture.InviteAsync();
        var credentials = fixture.Credentials(workspace);
        workspace = await fixture.Submit(workspace, credentials);
        var approved = await Task.WhenAll(fixture.Approve(workspace), fixture.ApproveInNewContext(workspace));
        Assert.Contains(approved, item => item.Code == "ALREADY_APPROVED");
        workspace = approved[0];
        var employeeId = workspace.Invitation!.CompletedUserId!.Value;
        var own = await fixture.Run(new() { Action = OnboardingAction.RequestCorrection, ActorId = employeeId, Reason = "Synthetic bank correction" });
        var opened = await fixture.Run(new() { Action = OnboardingAction.OpenCorrection, ActorId = fixture.AdminId, EmployeeId = employeeId,
            ExpectedVersion = own.CorrectionRequest!.Version, Reason = "Update bank" });
        var previous = opened.ApprovedRevision!.Profile.BankName;
        var corrected = opened.Revision!.Profile with { BankName = "Corrected synthetic bank" };
        var submitted = await fixture.Run(new() { Action = OnboardingAction.Submit, ActorId = employeeId, ExpectedVersion = opened.Revision.Version, Profile = corrected });
        Assert.Equal(previous, submitted.ApprovedRevision!.Profile.BankName);
        var correctedResult = await fixture.Approve(submitted);
        Assert.Equal(corrected.BankName, correctedResult.ApprovedRevision!.Profile.BankName);
        Assert.Equal(1, await fixture.Context.Employees.CountAsync(item => item.UserId == employeeId));
        await Assert.ThrowsAsync<StaffOnboardingException>(() => fixture.Operations.CreateStaffAsync(new CreateStaffCommand { UserId = employeeId }, default));
        var other = await fixture.InviteAsync();
        var otherCredentials = fixture.Credentials(other);
        var customerRole = await fixture.Context.Roles.SingleAsync(item => item.Name == "CUSTOMER" && item.IsDeleted != true);
        var customer = new User { Id = Guid.NewGuid(), Email = other.Invitation!.Email, Username = $"synthetic-{Guid.NewGuid():N}", RoleId = customerRole.Id, Status = "Active" };
        fixture.Context.Users.Add(customer);
        await fixture.Context.SaveChangesAsync();
        var failure = await Assert.ThrowsAsync<StaffOnboardingException>(() => fixture.Submit(other, otherCredentials));
        Assert.Equal("ACCOUNT_ALREADY_EXISTS", failure.Code);
        Assert.False(await fixture.Context.Employees.AnyAsync(item => item.UserId == customer.Id));
        Assert.Equal(customerRole.Id, (await fixture.Context.Users.SingleAsync(item => item.Id == customer.Id)).RoleId);
    }

    private sealed class Fixture : IAsyncDisposable
    {
        public required SmartPoolDbContext Context { get; init; }
        public required DbContextOptions<SmartPoolDbContext> DbOptions { get; init; }
        public required StaffOperations Operations { get; init; }
        public required StaffDocumentCleanup Cleanup { get; init; }
        public required StaffOnboardingServices Services { get; init; }
        public required Guid AdminId { get; init; }
        public required IMapper Mapper { get; init; }
        public required Guid ShiftId { get; init; }
        public FakeStorage Storage { get; init; } = new();
        public FakeMail Mail { get; init; } = new();
        public Clock Clock { get; init; } = new();
        public static async Task<Fixture> CreateAsync()
        {
            var options = await IntegrationTestDatabase.CreateOptionsAsync();
            var context = new SmartPoolDbContext(options);
            var admin = await context.Users.FirstAsync(item => item.Role!.Name == "ADMIN");
            var shiftId = Guid.NewGuid();
            context.Shifts.Add(new Shift { Id = shiftId, Name = "Synthetic onboarding", StartTime = new TimeOnly(6, 0), EndTime = new TimeOnly(12, 0), IsActive = true });
            await context.SaveChangesAsync();
            var mapper = new MapperConfiguration(config => config.AddProfile<StaffMappingProfile>(), NullLoggerFactory.Instance).CreateMapper();
            var storage = new FakeStorage(); var mail = new FakeMail(); var clock = new Clock();
            var cleanup = new StaffDocumentCleanup(context, storage, clock);
            var services = new StaffOnboardingServices(Options.Create(new StaffOnboardingOptions { EncryptionKey = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32)),
                IdentityKey = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32)), FrontendOrigin = "https://example.test" }), new PasswordHasherService(), mail, storage, cleanup);
            return new() { Context = context, DbOptions = options, Mapper = mapper, AdminId = admin.Id, ShiftId = shiftId, Storage = storage, Mail = mail, Clock = clock, Cleanup = cleanup, Services = services,
                Operations = new(context, mapper, clock, Options.Create(new StaffAttendanceQrSettings()), services) };
        }
        public Task<OnboardingWorkspace> Run(OnboardingCommand command) => Operations.ExecuteOnboardingAsync(command, default);
        public ContractTerms ContractFor(DateOnly startDate, DateOnly endDate) => new("FULL_TIME", startDate, endDate,
            Enumerable.Range(1, 7).Select(day => new WeeklyPatternDay(day, [ShiftId])).ToList());
        public Task<OnboardingWorkspace> InviteAsync()
        {
            var startDate = VietnamTimeBoundary.GetDate(Clock);
            return Run(new() { Action = OnboardingAction.Create, ActorId = AdminId, Email = $"synthetic-{Guid.NewGuid():N}@example.test", TargetRole = "SALES", JoinDate = startDate,
                Contract = ContractFor(startDate, startDate), FullName = "Synthetic staff", Phone = "0901234567" });
        }
        public (Guid Id, string Token) Credentials(OnboardingWorkspace workspace) => (workspace.Invitation!.Id, Regex.Match(WebUtility.HtmlDecode(Mail.Body), "token=([A-F0-9]{64})").Groups[1].Value);
        public Task<OnboardingWorkspace> Submit(OnboardingWorkspace workspace, (Guid Id, string Token) credentials) => Run(new() { Action = OnboardingAction.Submit, InvitationId = credentials.Id, Token = credentials.Token,
            ExpectedVersion = workspace.Revision!.Version, Password = "Synthetic123!", PasswordConfirmation = "Synthetic123!", Profile = new StaffLegalProfile {
                FullName = "Synthetic onboarding", Phone = "09" + RandomNumberGenerator.GetInt32(10000000, 99999999), DateOfBirth = new(2000, 1, 1), CccdNumber = "00" + RandomNumberGenerator.GetInt32(10000, 99999) + RandomNumberGenerator.GetInt32(10000, 99999),
                PermanentAddress = "Synthetic address", TemporaryAddress = "Synthetic address", BankName = "Synthetic bank", BankAccountNumber = "000123456", TaxIdentifierKind = "LEGACY_TAX_CODE", TaxIdentifier = "0012345678" } });
        public Task<OnboardingWorkspace> Approve(OnboardingWorkspace workspace) => Run(new() { Action = OnboardingAction.Approve, ActorId = AdminId, RevisionId = workspace.Revision!.Id, ExpectedVersion = workspace.Revision.Version, ReviewConfirmed = true });
        public async Task<OnboardingWorkspace> ApproveInNewContext(OnboardingWorkspace workspace)
        {
            await using var context = new SmartPoolDbContext(DbOptions);
            var services = new StaffOnboardingServices(Options.Create(Services.Options), new PasswordHasherService(), Mail, Storage, new StaffDocumentCleanup(context, Storage, Clock));
            return await new StaffOperations(context, Mapper, Clock, Options.Create(new StaffAttendanceQrSettings()), services).ExecuteOnboardingAsync(new() { Action = OnboardingAction.Approve,
                ActorId = AdminId, RevisionId = workspace.Revision!.Id, ExpectedVersion = workspace.Revision.Version, ReviewConfirmed = true }, default);
        }
        public ValueTask DisposeAsync() => Context.DisposeAsync();
    }
    private sealed class Clock : TimeProvider { public DateTimeOffset Now { get; set; } = DateTimeOffset.UtcNow; public override DateTimeOffset GetUtcNow() => Now; }
    private sealed class FakeMail : IEmailService
    {
        public string Body { get; private set; } = "";
        public string Subject { get; private set; } = "";
        public Task SendAsync(string to, string subject, string htmlBody, CancellationToken cancellationToken = default) { Body = htmlBody; Subject = subject; return Task.CompletedTask; }
        public Task SendAsync(IEnumerable<string> recipients, string subject, string htmlBody, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    }
    private sealed class FakeStorage : IStaffDocumentStorage
    {
        public bool FailDelete { get; set; }
        public bool FailUpload { get; set; }
        public TaskCompletionSource<bool>? UploadStarted { get; set; }
        public TaskCompletionSource<bool>? ReleaseUpload { get; set; }
        public string? LastDeliveryType { get; private set; }
        public async Task<StaffStoredAsset> UploadAsync(StaffOnboardingDocument document, byte[] bytes, CancellationToken cancellationToken)
        {
            LastDeliveryType = document.DeliveryType;
            if (FailUpload) throw new StaffOnboardingException("DOCUMENT_UPLOAD_FAILED", 503);
            UploadStarted?.TrySetResult(true);
            if (ReleaseUpload is not null) await ReleaseUpload.Task.WaitAsync(cancellationToken);
            return new StaffStoredAsset("synthetic", "1");
        }
        public Task<byte[]> ReadAsync(StaffOnboardingDocument document, CancellationToken cancellationToken) => Task.FromResult(new byte[] { 137, 80, 78, 71 });
        public Task DeleteAsync(StaffOnboardingDocument document, CancellationToken cancellationToken) => FailDelete ? Task.FromException(new IOException("Synthetic outage")) : Task.CompletedTask;
        public string AvatarUrl(StaffOnboardingDocument document) => "https://example.test/synthetic.png";
    }
}
