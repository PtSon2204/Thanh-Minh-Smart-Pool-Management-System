using FluentValidation;
using Microsoft.EntityFrameworkCore;
using SmartPool.Application.Features.ManageStaffs.Onboarding;
using SmartPool.Application.Features.ManageStaffs.WorkPatterns;
using SmartPool.Domain.Entities;
using SmartPool.Infrastructure.Services;

namespace SmartPool.IntegrationTests.ManageStaffs;

public sealed class StaffWorkPatternIntegrationTests
{
    [Fact]
    public async Task SameDayContractRestoresOnlyContractOwnedLocksAndPreservesTodayShifts()
    {
        await using var fixture = await StaffWorkPatternTestFixture.CreateAsync();
        var context = fixture.Context;
        var employee = await context.Employees.Include(item => item.User)
            .SingleAsync(item => item.UserId == fixture.EmployeeId);
        var initialCommand = fixture.PatternCommand(fixture.Contract(fixture.Today.AddDays(1)), fixture.Today, 0);
        var initial = await fixture.ConfirmAsync(initialCommand, await fixture.PreviewAsync(initialCommand), Guid.NewGuid());
        Assert.Equal("Working", employee.Status);

        fixture.Clock.Now = fixture.Clock.Now.AddDays(2);
        var policy = new StaffContractAccessPolicy(context, fixture.Clock);
        await new StaffContractLifecycle(context, policy, fixture.Clock).SynchronizeEmployeeAsync(fixture.EmployeeId);
        await context.Entry(employee).ReloadAsync();
        await context.Entry(employee.User).ReloadAsync();
        Assert.Equal("Inactive", employee.Status);
        Assert.Equal("LOCKED", employee.User.Status);
        Assert.NotNull(employee.ContractInactiveSinceUtc);

        var todayAssignment = await fixture.AddAssignmentAsync(fixture.Today, fixture.MorningShiftId);
        todayAssignment.ScheduleChangeId = await context.StaffScheduleChanges
            .Where(item => item.EmployeeId == fixture.EmployeeId && item.AppliedVersion == initial.Version)
            .Select(item => item.Id).SingleAsync();
        await context.SaveChangesAsync();
        var contract = fixture.Contract(fixture.Today.AddDays(3), [fixture.AfternoonShiftId]);
        var command = fixture.PatternCommand(contract, fixture.Today, initial.Version);
        var preview = await fixture.PreviewAsync(command);
        Assert.Equal(3, preview.CreatedCount);
        Assert.Equal(0, preview.CancelledCount);
        var confirmed = await fixture.ConfirmAsync(command, preview, Guid.NewGuid());
        Assert.Equal("Working", employee.Status);
        Assert.Equal("ACTIVE", employee.User.Status);
        Assert.Null(employee.ContractInactiveSinceUtc);
        Assert.True((await policy.EvaluateAsync(fixture.EmployeeId, "SALES")).IsAllowed);
        Assert.Equal("Scheduled", todayAssignment.Status);
        Assert.False(await context.EmployeeShifts.AnyAsync(item => item.EmployeeId == fixture.EmployeeId &&
            item.WorkDate == fixture.Today && item.ShiftId == fixture.AfternoonShiftId));

        await fixture.Operations.UpdateStaffAsync(new()
        { UserId = fixture.EmployeeId, Status = "Inactive" }, default);
        var manualCommand = fixture.PatternCommand(contract with { EndDate = contract.EndDate.AddDays(1) },
            fixture.Today, confirmed.Version);
        var manualPreview = await fixture.PreviewAsync(manualCommand);
        Assert.Equal("skipped_ineligible", manualPreview.Status);
        var manual = await fixture.ConfirmAsync(manualCommand, manualPreview, Guid.NewGuid());
        Assert.Equal("Inactive", employee.Status);
        Assert.Equal("LOCKED", employee.User.Status);
        Assert.Null(employee.ContractInactiveSinceUtc);
        Assert.False((await policy.EvaluateAsync(fixture.EmployeeId, "SALES")).IsAllowed);

        employee.Status = "Working";
        employee.User.Status = "ACTIVE";
        await context.SaveChangesAsync();
        var futureCommand = fixture.PatternCommand(contract with
        { StartDate = fixture.Today.AddDays(1), EndDate = fixture.Today.AddDays(5) },
            fixture.Today.AddDays(1), manual.Version);
        await fixture.ConfirmAsync(futureCommand, await fixture.PreviewAsync(futureCommand), Guid.NewGuid());
        Assert.Equal("Working", employee.Status);
        Assert.Equal("ACTIVE", employee.User.Status);
        Assert.True((await policy.EvaluateAsync(fixture.EmployeeId, "SALES")).IsAllowed);
    }

    [Fact]
    public async Task RenewalPreservesPeriodsReceiptsAndEmploymentBlocks()
    {
        await using var fixture = await StaffWorkPatternTestFixture.CreateAsync();
        var context = fixture.Context;
        var policy = new StaffContractAccessPolicy(context, fixture.Clock);
        var lifecycle = new StaffContractLifecycle(context, policy, fixture.Clock);
        var employee = await context.Employees.Include(item => item.User).SingleAsync(item => item.UserId == fixture.EmployeeId);

        WorkPatternCommand Renewal(long version, DateOnly end, DateOnly? start = null) => new()
        {
            EmployeeId = fixture.EmployeeId, ActorId = fixture.AdminId, Action = WorkPatternAction.RenewalPreview,
            ExpectedVersion = version, NewEndDate = end, NewStartDate = start
        };
        async Task<ScheduleResult> Preview(WorkPatternCommand command) =>
            Assert.IsType<ScheduleResult>(await fixture.Operations.ExecuteWorkPatternAsync(command, default));
        async Task<WorkPatternResponse> Confirm(WorkPatternCommand command, ScheduleResult preview)
        {
            command.Action = WorkPatternAction.RenewalConfirm;
            command.PreviewToken = preview.PreviewToken;
            command.OperationId = Guid.NewGuid();
            return Assert.IsType<WorkPatternResponse>(await fixture.Operations.ExecuteWorkPatternAsync(command, default));
        }
        async Task Reject(WorkPatternCommand command, string code)
        {
            var exception = await Assert.ThrowsAsync<StaffOnboardingException>(() => fixture.Operations.ExecuteWorkPatternAsync(command, default));
            Assert.Equal(code, exception.Code);
        }

        await Reject(Renewal(0, fixture.Today.AddDays(5)), "RENEWAL_CONTRACT_REQUIRED");
        var initial = Contract(fixture, fixture.Today.AddDays(4), [fixture.MorningShiftId]);
        var setup = fixture.PatternCommand(initial, fixture.Today.AddDays(1), 0);
        var first = await fixture.ConfirmAsync(setup, await fixture.PreviewAsync(setup), Guid.NewGuid());
        fixture.Clock.Now = fixture.Clock.Now.AddDays(1);
        var original = await context.StaffScheduleChanges.AsNoTracking().SingleAsync(item => item.EmployeeId == fixture.EmployeeId);
        var cancelled = await fixture.AddAssignmentAsync(initial.EndDate.AddDays(1), fixture.MorningShiftId, "Cancelled");
        var attended = await fixture.AddAssignmentAsync(initial.EndDate.AddDays(2), fixture.AfternoonShiftId, "Present", fixture.Clock.Now.UtcDateTime);
        var duty = await fixture.AddDutyAsync(initial.EndDate.AddDays(1), fixture.MorningShiftId);

        await Reject(Renewal(first.Version, initial.EndDate), "INVALID_RENEWAL_DATES");
        var extensionCommand = Renewal(first.Version, initial.EndDate.AddDays(3));
        var extensionPreview = await Preview(extensionCommand);
        Assert.Equal(2, extensionPreview.CreatedCount);
        Assert.Equal(0, extensionPreview.CancelledCount);
        Assert.True(extensionPreview.ProtectedCount >= 2);
        Assert.Empty(extensionPreview.AffectedTasks);
        var extension = await Confirm(extensionCommand, extensionPreview);
        Assert.Equal(initial.StartDate, extension.Contract!.StartDate);
        Assert.Equal(initial.EmploymentType, extension.Contract.EmploymentType);
        Assert.Equal(System.Text.Json.JsonSerializer.Serialize(initial.WeeklyPattern), System.Text.Json.JsonSerializer.Serialize(extension.Contract.WeeklyPattern));
        Assert.Equal(original.ContractJson, (await context.StaffScheduleChanges.AsNoTracking().SingleAsync(item => item.Id == original.Id)).ContractJson);
        Assert.Equal("Cancelled", cancelled.Status);
        Assert.Equal("Present", attended.Status);
        Assert.Equal("Scheduled", duty.Status);
        var count = await context.EmployeeShifts.CountAsync(item => item.EmployeeId == fixture.EmployeeId);
        var replay = Assert.IsType<WorkPatternResponse>(await fixture.Operations.ExecuteWorkPatternAsync(extensionCommand, default));
        Assert.Equal(extension.Version, replay.Version);
        Assert.Equal(count, await context.EmployeeShifts.CountAsync(item => item.EmployeeId == fixture.EmployeeId));
        Assert.Equal(2, await context.StaffScheduleChanges.CountAsync(item => item.EmployeeId == fixture.EmployeeId));
        var receipt = Assert.IsType<WorkPatternResponse>(await fixture.Operations.ExecuteWorkPatternAsync(new()
        {
            Action = WorkPatternAction.Receipt, EmployeeId = fixture.EmployeeId, ActorId = fixture.AdminId, OperationId = extensionCommand.OperationId
        }, default));
        Assert.Equal(extension.Version, receipt.Version);
        await Reject(Renewal(first.Version, extension.Contract.EndDate.AddDays(1)), "STALE_VERSION");
        var stale = Renewal(extension.Version, extension.Contract.EndDate.AddDays(1));
        var stalePreview = await Preview(stale);
        await fixture.AddAssignmentAsync(stale.NewEndDate, fixture.AfternoonShiftId);
        stale.Action = WorkPatternAction.RenewalConfirm;
        stale.PreviewToken = stalePreview.PreviewToken;
        stale.OperationId = Guid.NewGuid();
        await Reject(stale, "STALE_PREVIEW");
        Assert.Equal(2, await context.StaffScheduleChanges.CountAsync(item => item.EmployeeId == fixture.EmployeeId));

        fixture.Clock.Now = fixture.Clock.Now.AddDays(extension.Contract.EndDate.DayNumber - fixture.Today.DayNumber + 1);
        await lifecycle.SynchronizeEmployeeAsync(fixture.EmployeeId);
        var todayRenewal = Renewal(extension.Version, fixture.Today.AddDays(2), fixture.Today);
        var renewed = await Confirm(todayRenewal, await Preview(todayRenewal));
        Assert.Equal(fixture.Today, renewed.Contract!.StartDate);
        Assert.Equal("Working", employee.Status);
        Assert.Null(employee.ContractInactiveSinceUtc);
        Assert.True((await policy.EvaluateAsync(fixture.EmployeeId, "SALES")).IsAllowed);
        Assert.False(await context.EmployeeShifts.AnyAsync(item => item.EmployeeId == fixture.EmployeeId && item.WorkDate == fixture.Today && item.ScheduleChangeId != null));

        fixture.Clock.Now = fixture.Clock.Now.AddDays(3);
        await lifecycle.SynchronizeEmployeeAsync(fixture.EmployeeId);
        var futureCommand = Renewal(renewed.Version, fixture.Today.AddDays(4), fixture.Today.AddDays(2));
        var future = await Confirm(futureCommand, await Preview(futureCommand));
        Assert.Equal("Inactive", employee.Status);
        Assert.NotNull(employee.ContractInactiveSinceUtc);
        Assert.False((await policy.EvaluateAsync(fixture.EmployeeId, "SALES")).IsAllowed);
        var view = Assert.IsType<WorkPatternView>(await fixture.Operations.ExecuteWorkPatternAsync(new()
        { EmployeeId = fixture.EmployeeId, ActorId = fixture.AdminId, Action = WorkPatternAction.Get }, default));
        Assert.True(view.Renewal!.HasPendingRevision);
        Assert.Equal(renewed.Contract.EndDate, view.Renewal.CurrentContract!.EndDate);
        await Reject(Renewal(future.Version, future.Contract!.EndDate.AddDays(1)), "RENEWAL_PENDING_REVISION");
        fixture.Clock.Now = fixture.Clock.Now.AddDays(2);
        Assert.True((await policy.EvaluateAsync(fixture.EmployeeId, "SALES")).IsAllowed);
        await lifecycle.SynchronizeEmployeeAsync(fixture.EmployeeId);
        await context.Entry(employee).ReloadAsync();
        Assert.Equal("Working", employee.Status);

        employee.Status = "Inactive";
        employee.ContractInactiveSinceUtc = null;
        await context.SaveChangesAsync();
        var manualCommand = Renewal(future.Version, future.Contract.EndDate.AddDays(1));
        var manual = await Confirm(manualCommand, await Preview(manualCommand));
        Assert.Equal("skipped_ineligible", manual.Result.Status);
        Assert.Equal("Inactive", employee.Status);
        Assert.Null(employee.ContractInactiveSinceUtc);
        Assert.False((await policy.EvaluateAsync(fixture.EmployeeId, "SALES")).IsAllowed);
        employee.User.Status = "Inactive";
        employee.ContractInactiveSinceUtc = fixture.Clock.Now.UtcDateTime;
        await context.SaveChangesAsync();
        var disabledCommand = Renewal(manual.Version, manual.Contract!.EndDate.AddDays(1));
        var disabled = await Confirm(disabledCommand, await Preview(disabledCommand));
        Assert.Equal("skipped_ineligible", disabled.Result.Status);
        Assert.Equal("Inactive", employee.Status);
        Assert.Equal("Inactive", employee.User.Status);
        Assert.NotNull(employee.ContractInactiveSinceUtc);
    }

    [Fact]
    public async Task ExtensionAddsOnlyNewDatesAndReplayDoesNotDuplicateRowsOrNotices()
    {
        await using var fixture = await StaffWorkPatternTestFixture.CreateAsync();
        var initial = Contract(fixture, fixture.Today.AddDays(7), [fixture.MorningShiftId, fixture.AfternoonShiftId]);
        var firstCommand = fixture.PatternCommand(initial, fixture.Today.AddDays(1), 0);
        var firstPreview = await fixture.PreviewAsync(firstCommand);
        Assert.Equal(14, firstPreview.CreatedCount);
        var first = await fixture.ConfirmAsync(firstCommand, firstPreview, Guid.NewGuid());
        Assert.Equal(14, first.Result.CreatedCount);

        var extended = initial with { EndDate = fixture.Today.AddDays(14) };
        var extensionCommand = fixture.PatternCommand(extended, initial.EndDate.AddDays(1), first.Version);
        var extensionPreview = await fixture.PreviewAsync(extensionCommand);
        Assert.Equal(14, extensionPreview.CreatedCount);
        var operationId = Guid.NewGuid();
        var extension = await fixture.ConfirmAsync(extensionCommand, extensionPreview, operationId);
        var replay = Assert.IsType<WorkPatternResponse>(await fixture.Operations.ExecuteWorkPatternAsync(extensionCommand, default));

        Assert.Equal(14, extension.Result.CreatedCount);
        Assert.Equal(extension.Result.CreatedCount, replay.Result.CreatedCount);
        Assert.Equal(28, await fixture.Context.EmployeeShifts.CountAsync(item => item.EmployeeId == fixture.EmployeeId));
        Assert.Equal(2, await fixture.Context.Notifications.CountAsync(item => item.UserId == fixture.EmployeeId && item.Type == "StaffSchedule"));
        Assert.Equal(2, await fixture.Context.StaffScheduleDeliveries.CountAsync(item => item.EmployeeId == fixture.EmployeeId));
    }

    [Fact]
    public async Task ShorteningCancelsOnlyFutureGeneratedRowsBeyondTheInclusiveEnd()
    {
        await using var fixture = await StaffWorkPatternTestFixture.CreateAsync();
        var initial = Contract(fixture, fixture.Today.AddDays(14), [fixture.MorningShiftId]);
        var firstCommand = fixture.PatternCommand(initial, fixture.Today.AddDays(1), 0);
        var firstPreview = await fixture.PreviewAsync(firstCommand);
        await fixture.ConfirmAsync(firstCommand, firstPreview, Guid.NewGuid());
        var extended = initial with { EndDate = fixture.Today.AddDays(21) };
        var extensionCommand = fixture.PatternCommand(extended, initial.EndDate.AddDays(1), 1);
        var extensionPreview = await fixture.PreviewAsync(extensionCommand);
        await fixture.ConfirmAsync(extensionCommand, extensionPreview, Guid.NewGuid());

        var shortened = extended with { EndDate = fixture.Today.AddDays(17) };
        var shortenCommand = fixture.PatternCommand(shortened, initial.EndDate.AddDays(1), 2);
        var shortenPreview = await fixture.PreviewAsync(shortenCommand);
        var result = await fixture.ConfirmAsync(shortenCommand, shortenPreview, Guid.NewGuid());

        Assert.Equal(4, shortenPreview.CancelledCount);
        Assert.Equal(4, result.Result.CancelledCount);
        Assert.Equal(4, await fixture.Context.EmployeeShifts.CountAsync(item => item.EmployeeId == fixture.EmployeeId &&
            item.ScheduleChange != null && item.ScheduleChange.Kind == "Pattern" && item.WorkDate > shortened.EndDate && item.Status == "Cancelled"));
        Assert.Equal(14, await fixture.Context.EmployeeShifts.CountAsync(item => item.EmployeeId == fixture.EmployeeId &&
            item.ScheduleChange != null && item.ScheduleChange.Kind == "Pattern" && item.WorkDate <= initial.EndDate && item.Status == "Scheduled"));
    }

    [Fact]
    public async Task LeaveRemainsSuppressedAfterPatternRegeneration()
    {
        await using var fixture = await StaffWorkPatternTestFixture.CreateAsync();
        var contract = Contract(fixture, fixture.Today.AddDays(7), [fixture.MorningShiftId]);
        var patternCommand = fixture.PatternCommand(contract, fixture.Today.AddDays(1), 0);
        var patternPreview = await fixture.PreviewAsync(patternCommand);
        await fixture.ConfirmAsync(patternCommand, patternPreview, Guid.NewGuid());
        var leaveDate = fixture.Today.AddDays(3);
        var source = await fixture.Context.EmployeeShifts.SingleAsync(item => item.EmployeeId == fixture.EmployeeId &&
            item.WorkDate == leaveDate && item.ScheduleChange != null && item.ScheduleChange.Kind == "Pattern" && item.Status == "Scheduled");
        var leaveCommand = new WorkPatternCommand { EmployeeId = fixture.EmployeeId, ActorId = fixture.AdminId,
            Action = WorkPatternAction.ExceptionPreview, Kind = "Leave", WorkDate = leaveDate,
            SourceAssignmentIds = [source.Id], Reason = "Synthetic agreed leave", ExpectedVersion = 1 };
        var leavePreview = Assert.IsType<ScheduleResult>(await fixture.Operations.ExecuteWorkPatternAsync(leaveCommand, default));
        leaveCommand.Action = WorkPatternAction.ExceptionConfirm;
        leaveCommand.PreviewToken = leavePreview.PreviewToken;
        leaveCommand.OperationId = Guid.NewGuid();
        var leave = Assert.IsType<WorkPatternResponse>(await fixture.Operations.ExecuteWorkPatternAsync(leaveCommand, default));

        var regenerateCommand = fixture.PatternCommand(contract, leaveDate, leave.Version);
        var regeneratePreview = await fixture.PreviewAsync(regenerateCommand);
        var regenerated = await fixture.ConfirmAsync(regenerateCommand, regeneratePreview, Guid.NewGuid());

        Assert.Equal(1, leave.Result.CancelledCount);
        Assert.Equal(0, regeneratePreview.CreatedCount);
        Assert.Contains(regeneratePreview.Details, item => item.Code == "exception_preserved" && item.WorkDate == leaveDate);
        Assert.Equal(leave.Version + 1, regenerated.Version);
        Assert.Equal("Cancelled", (await fixture.Context.EmployeeShifts.SingleAsync(item => item.Id == source.Id)).Status);
        Assert.False(await fixture.Context.EmployeeShifts.AnyAsync(item => item.EmployeeId == fixture.EmployeeId && item.WorkDate == leaveDate && item.Status == "Scheduled"));
    }

    [Fact]
    public async Task IndependentShiftMovesCanBeRecordedForTheSameDate()
    {
        await using var fixture = await StaffWorkPatternTestFixture.CreateAsync();
        var contract = Contract(fixture, fixture.Today.AddDays(8), [fixture.MorningShiftId, fixture.AfternoonShiftId]);
        var patternCommand = fixture.PatternCommand(contract, fixture.Today.AddDays(1), 0);
        var patternPreview = await fixture.PreviewAsync(patternCommand);
        await fixture.ConfirmAsync(patternCommand, patternPreview, Guid.NewGuid());
        var workDate = fixture.Today.AddDays(3);
        var eveningShiftId = Guid.NewGuid();
        fixture.Context.Shifts.Add(new Shift { Id = eveningShiftId, Name = "Synthetic evening", StartTime = new(19, 30), EndTime = new(21, 30), IsActive = true });
        await fixture.Context.SaveChangesAsync();
        var morning = await fixture.Context.EmployeeShifts.SingleAsync(item => item.EmployeeId == fixture.EmployeeId &&
            item.WorkDate == workDate && item.ShiftId == fixture.MorningShiftId);
        var afternoon = await fixture.Context.EmployeeShifts.SingleAsync(item => item.EmployeeId == fixture.EmployeeId &&
            item.WorkDate == workDate && item.ShiftId == fixture.AfternoonShiftId);

        var first = await Move(fixture, workDate, morning.Id, fixture.NightShiftId, 1);
        var second = await Move(fixture, workDate, afternoon.Id, eveningShiftId, first.Version);

        Assert.Equal("completed", first.Result.Status);
        Assert.Equal("completed", second.Result.Status);
        Assert.Equal(2, await fixture.Context.StaffScheduleChanges.CountAsync(item => item.EmployeeId == fixture.EmployeeId &&
            item.AppliedVersion != null && item.WorkDate == workDate && item.Kind == "Move"));
        Assert.Equal("Scheduled", (await fixture.Context.EmployeeShifts.SingleAsync(item => item.EmployeeId == fixture.EmployeeId &&
            item.WorkDate == workDate && item.ShiftId == fixture.NightShiftId)).Status);
        Assert.Equal("Scheduled", (await fixture.Context.EmployeeShifts.SingleAsync(item => item.EmployeeId == fixture.EmployeeId &&
            item.WorkDate == workDate && item.ShiftId == eveningShiftId)).Status);
    }

    [Fact]
    public async Task PatternChangePreservesManualHistoricalAttendedRowsAndUnassignsDuty()
    {
        await using var fixture = await StaffWorkPatternTestFixture.CreateAsync();
        var contract = Contract(fixture, fixture.Today.AddDays(7), [fixture.MorningShiftId]);
        var initialCommand = fixture.PatternCommand(contract, fixture.Today.AddDays(1), 0);
        var initialPreview = await fixture.PreviewAsync(initialCommand);
        await fixture.ConfirmAsync(initialCommand, initialPreview, Guid.NewGuid());

        var changedDate = fixture.Today.AddDays(4);
        var manual = await fixture.Operations.AssignShiftAsync(new() { EmployeeId = fixture.EmployeeId,
            ShiftId = fixture.AfternoonShiftId, WorkDate = changedDate }, default);
        var historical = await fixture.AddAssignmentAsync(fixture.Today.AddDays(-1), fixture.MorningShiftId, "Completed", fixture.Clock.GetUtcNow().UtcDateTime.AddHours(-8));
        historical.CheckOutTime = fixture.Clock.GetUtcNow().UtcDateTime.AddHours(-2);
        historical.CheckOutMethod = "Manual";
        await fixture.Context.SaveChangesAsync();
        var attendedDate = fixture.Today.AddDays(5);
        var attended = await fixture.Context.EmployeeShifts.SingleAsync(item => item.EmployeeId == fixture.EmployeeId &&
            item.WorkDate == attendedDate && item.ShiftId == fixture.MorningShiftId && item.ScheduleChange != null && item.ScheduleChange.Kind == "Pattern");
        attended.Status = "Present";
        attended.CheckInTime = fixture.Clock.GetUtcNow().UtcDateTime;
        attended.CheckInMethod = "Manual";
        await fixture.Context.SaveChangesAsync();
        var duty = await fixture.AddDutyAsync(changedDate, fixture.MorningShiftId);
        var changedDay = IsoDay(changedDate);
        var changedContract = contract with { WeeklyPattern = contract.WeeklyPattern.Select(item =>
            item.DayOfWeek == changedDay ? item with { ShiftIds = [] } : item).ToList() };
        var command = fixture.PatternCommand(changedContract, changedDate, 1);
        var preview = await fixture.PreviewAsync(command);
        var result = await fixture.ConfirmAsync(command, preview, Guid.NewGuid());

        Assert.Equal(1, preview.CancelledCount);
        Assert.Equal(1, preview.ConflictCount);
        Assert.Single(preview.AffectedTasks);
        Assert.Equal(1, result.Result.CancelledCount);
        Assert.Equal("Scheduled", (await fixture.Context.EmployeeShifts.SingleAsync(item => item.Id == manual.Id)).Status);
        Assert.Null((await fixture.Context.EmployeeShifts.SingleAsync(item => item.Id == manual.Id)).ScheduleChangeId);
        Assert.Equal("Completed", (await fixture.Context.EmployeeShifts.SingleAsync(item => item.Id == historical.Id)).Status);
        var persistedAttendance = await fixture.Context.EmployeeShifts.SingleAsync(item => item.Id == attended.Id);
        Assert.Equal("Present", persistedAttendance.Status);
        Assert.NotNull(persistedAttendance.CheckInTime);
        var persistedDuty = await fixture.Context.PoolDutySchedules.SingleAsync(item => item.Id == duty.Id);
        Assert.Equal("Unassigned", persistedDuty.Status);
        Assert.Null(persistedDuty.AssignedEmployeeId);
        Assert.True(await fixture.Context.Notifications.AnyAsync(item => item.UserId == fixture.AdminId && item.Title == "Cần phân công lại ca trực"));
    }

    [Theory]
    [InlineData("started-source", "shift_started")]
    [InlineData("checked-in-source", "source_unavailable")]
    [InlineData("started-destination", "destination_started")]
    public async Task SameDayMovePreviewRejectsStartedOrCheckedInShifts(string scenario, string expectedCode)
    {
        await using var fixture = await StaffWorkPatternTestFixture.CreateAsync();
        var contract = Contract(fixture, fixture.Today.AddDays(7), [fixture.MorningShiftId]);
        var patternCommand = fixture.PatternCommand(contract, fixture.Today.AddDays(1), 0);
        var patternPreview = await fixture.PreviewAsync(patternCommand);
        var pattern = await fixture.ConfirmAsync(patternCommand, patternPreview, Guid.NewGuid());
        var (sourceShift, destinationShift, status, checkIn) = scenario switch
        {
            "started-source" => (fixture.MorningShiftId, fixture.AfternoonShiftId, "Scheduled", (DateTime?)null),
            "checked-in-source" => (fixture.AfternoonShiftId, fixture.MorningShiftId, "Present", fixture.Clock.GetUtcNow().UtcDateTime.AddHours(-1)),
            "started-destination" => (fixture.NightShiftId, fixture.MorningShiftId, "Scheduled", (DateTime?)null),
            _ => throw new ArgumentOutOfRangeException(nameof(scenario))
        };
        var source = await fixture.AddAssignmentAsync(fixture.Today, sourceShift, status, checkIn);
        var command = new WorkPatternCommand { EmployeeId = fixture.EmployeeId, ActorId = fixture.AdminId,
            Action = WorkPatternAction.ExceptionPreview, Kind = "Move", WorkDate = fixture.Today,
            SourceAssignmentIds = [source.Id], DestinationShiftId = destinationShift, Reason = "Synthetic same-day rule", ExpectedVersion = pattern.Version };

        var result = Assert.IsType<ScheduleResult>(await fixture.Operations.ExecuteWorkPatternAsync(command, default));

        Assert.Equal(1, result.ConflictCount);
        Assert.Equal(expectedCode, Assert.Single(result.Details).Code);
        Assert.Empty(await fixture.Context.StaffScheduleChanges.Where(item => item.EmployeeId == fixture.EmployeeId &&
            item.AppliedVersion != null && item.Kind != "Pattern").ToListAsync());
    }

    [Theory]
    [InlineData("missing", "CONTRACT_REQUIRED")]
    [InlineData("unknown-shift", "SHIFT_NOT_FOUND")]
    [InlineData("inactive-shift", "SHIFT_UNAVAILABLE")]
    [InlineData("overlap", "PATTERN_OVERLAP")]
    public async Task ContractPreviewRejectsInvalidTermsAtTheRepositoryBoundary(string scenario, string expectedCode)
    {
        await using var fixture = await StaffWorkPatternTestFixture.CreateAsync();
        var contract = Contract(fixture, fixture.Today.AddDays(7), [fixture.MorningShiftId]);
        if (scenario == "unknown-shift") contract = Contract(fixture, fixture.Today.AddDays(7), [Guid.NewGuid()]);
        if (scenario == "inactive-shift")
        {
            var shift = await fixture.Context.Shifts.SingleAsync(item => item.Id == fixture.MorningShiftId);
            shift.IsActive = false;
            await fixture.Context.SaveChangesAsync();
        }
        if (scenario == "overlap")
        {
            var overlapId = Guid.NewGuid();
            fixture.Context.Shifts.Add(new Shift { Id = overlapId, Name = "Synthetic overlap", StartTime = new(11, 0), EndTime = new(15, 0), IsActive = true });
            await fixture.Context.SaveChangesAsync();
            contract = contract with { WeeklyPattern = contract.WeeklyPattern.Select(item => item.DayOfWeek == 1
                ? item with { ShiftIds = [fixture.MorningShiftId, overlapId] } : item).ToList() };
        }
        if (scenario == "missing") contract = null!;
        var command = fixture.PatternCommand(contract, fixture.Today.AddDays(1), 0);
        command.Action = WorkPatternAction.Preview;

        var exception = await Assert.ThrowsAsync<SmartPool.Application.Features.ManageStaffs.Onboarding.StaffOnboardingException>(
            () => fixture.Operations.ExecuteWorkPatternAsync(command, default));

        Assert.Equal(expectedCode, exception.Code);
    }

    private static async Task<WorkPatternResponse> Move(StaffWorkPatternTestFixture fixture, DateOnly workDate,
        Guid sourceId, Guid destinationShiftId, long expectedVersion)
    {
        var command = new WorkPatternCommand { EmployeeId = fixture.EmployeeId, ActorId = fixture.AdminId,
            Action = WorkPatternAction.ExceptionPreview, Kind = "Move", WorkDate = workDate,
            SourceAssignmentIds = [sourceId], DestinationShiftId = destinationShiftId,
            Reason = "Synthetic independent move", ExpectedVersion = expectedVersion };
        var preview = Assert.IsType<ScheduleResult>(await fixture.Operations.ExecuteWorkPatternAsync(command, default));
        Assert.Equal("completed", preview.Status);
        command.Action = WorkPatternAction.ExceptionConfirm;
        command.PreviewToken = preview.PreviewToken;
        command.OperationId = Guid.NewGuid();
        return Assert.IsType<WorkPatternResponse>(await fixture.Operations.ExecuteWorkPatternAsync(command, default));
    }

    private static ContractTerms Contract(StaffWorkPatternTestFixture fixture, DateOnly endDate, IReadOnlyList<Guid> shifts) =>
        new("FULL_TIME", fixture.Today, endDate,
            Enumerable.Range(1, 7).Select(day => new WeeklyPatternDay(day, shifts)).ToList());

    private static int IsoDay(DateOnly date) => date.DayOfWeek == DayOfWeek.Sunday ? 7 : (int)date.DayOfWeek;
}
