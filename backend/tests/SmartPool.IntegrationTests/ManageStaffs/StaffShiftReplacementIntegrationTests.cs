using Microsoft.EntityFrameworkCore;
using Npgsql;
using SmartPool.Application.Features.ManageStaffs.Onboarding;
using SmartPool.Application.Features.ManageStaffs.WorkPatterns;
using SmartPool.Domain.Entities;

namespace SmartPool.IntegrationTests.ManageStaffs;

public sealed class StaffShiftReplacementIntegrationTests
{
    [Fact]
    public async Task ReplacingTwoShiftsTransfersDutiesReplaysAndKeepsOtherContractShifts()
    {
        await using var fixture = await StaffWorkPatternTestFixture.CreateAsync();
        var contract = Contract(fixture, fixture.Today.AddDays(8), [fixture.MorningShiftId, fixture.AfternoonShiftId, fixture.NightShiftId]);
        var patternCommand = fixture.PatternCommand(contract, fixture.Today.AddDays(1), 0);
        var patternPreview = await fixture.PreviewAsync(patternCommand);
        await fixture.ConfirmAsync(patternCommand, patternPreview, Guid.NewGuid());
        var workDate = fixture.Today.AddDays(2);
        var replacementEmployeeId = await fixture.AddReplacementEmployeeAsync();
        var sourceMorning = await Assignment(fixture, workDate, fixture.MorningShiftId);
        var sourceNight = await Assignment(fixture, workDate, fixture.NightShiftId);
        var duty = await fixture.AddDutyAsync(workDate, fixture.MorningShiftId);

        var firstCommand = Replacement(sourceMorning.Id, replacementEmployeeId);
        var firstPreview = await Preview(fixture, firstCommand);
        Assert.Equal("ready", firstPreview.Status);
        Assert.False(firstPreview.ReusesExistingAssignment);
        Assert.Single(firstPreview.Duties);
        var firstOperationId = Guid.NewGuid();
        var first = await Confirm(fixture, firstCommand, firstPreview, firstOperationId);
        var replay = await fixture.Operations.ReplaceStaffShiftAsync(firstCommand, default);

        Assert.Equal(firstOperationId, replay.OperationId);
        Assert.Equal(first.ReplacementAssignmentId, replay.ReplacementAssignmentId);
        Assert.Equal("Cancelled", (await fixture.Context.EmployeeShifts.SingleAsync(item => item.Id == sourceMorning.Id)).Status);
        Assert.Equal(replacementEmployeeId, (await fixture.Context.EmployeeShifts.SingleAsync(item => item.Id == first.ReplacementAssignmentId)).EmployeeId);
        Assert.Equal(replacementEmployeeId, (await fixture.Context.PoolDutySchedules.SingleAsync(item => item.Id == duty.Id)).AssignedEmployeeId);
        Assert.Equal("Scheduled", (await fixture.Context.PoolDutySchedules.SingleAsync(item => item.Id == duty.Id)).Status);
        Assert.Equal(2, await fixture.Context.StaffScheduleDeliveries.CountAsync(item => item.OperationId == firstOperationId));
        Assert.Equal(2, await fixture.Context.Notifications.CountAsync(item =>
            (item.UserId == fixture.EmployeeId || item.UserId == replacementEmployeeId) && item.Type == "StaffSchedule" &&
            (item.Title == "Ca làm việc đã được chuyển" || item.Title == "Bạn được phân công thay ca")));

        await fixture.AddAssignmentAsync(workDate, fixture.NightShiftId, employeeId: replacementEmployeeId);
        var secondCommand = Replacement(sourceNight.Id, replacementEmployeeId);
        var secondPreview = await Preview(fixture, secondCommand);
        Assert.True(secondPreview.ReusesExistingAssignment);
        var second = await Confirm(fixture, secondCommand, secondPreview, Guid.NewGuid());
        Assert.NotEqual(first.OperationId, second.OperationId);
        Assert.Equal(1, await fixture.Context.EmployeeShifts.CountAsync(item => item.EmployeeId == replacementEmployeeId &&
            item.WorkDate == workDate && item.ShiftId == fixture.NightShiftId && item.Status == "Scheduled"));
        Assert.Equal("Cancelled", (await fixture.Context.EmployeeShifts.SingleAsync(item => item.Id == sourceNight.Id)).Status);

        var exceptions = await fixture.Context.StaffScheduleExceptions.Where(item => item.EmployeeId == fixture.EmployeeId &&
            item.WorkDate == workDate && item.Kind == "Replace").OrderBy(item => item.Version).ToListAsync();
        Assert.Equal(2, exceptions.Count);
        Assert.All(exceptions, item => Assert.Equal(replacementEmployeeId, item.DestinationEmployeeId));
        var view = Assert.IsType<WorkPatternView>(await fixture.Operations.ExecuteWorkPatternAsync(new WorkPatternCommand
        { EmployeeId = fixture.EmployeeId, ActorId = fixture.AdminId, Action = WorkPatternAction.Get }, default));
        Assert.All(view.Exceptions.Where(item => item.Kind == "Replace"), item => Assert.Equal("Synthetic replacement", item.DestinationEmployeeName));

        var regenerate = fixture.PatternCommand(contract, workDate, 3);
        var regeneratePreview = await fixture.PreviewAsync(regenerate);
        var regenerated = await fixture.ConfirmAsync(regenerate, regeneratePreview, Guid.NewGuid());
        Assert.Equal(0, regenerated.Result.CreatedCount);
        Assert.Equal(0, regenerated.Result.CancelledCount);
        Assert.Equal("Scheduled", (await Assignment(fixture, workDate, fixture.AfternoonShiftId)).Status);
        Assert.False(await fixture.Context.EmployeeShifts.AnyAsync(item => item.EmployeeId == fixture.EmployeeId &&
            item.WorkDate == workDate && item.ShiftId == fixture.MorningShiftId && item.Status == "Scheduled"));
        Assert.False(await fixture.Context.EmployeeShifts.AnyAsync(item => item.EmployeeId == fixture.EmployeeId &&
            item.WorkDate == workDate && item.ShiftId == fixture.NightShiftId && item.Status == "Scheduled"));
    }

    [Fact]
    public async Task ReplacementBeforeTodaysShiftStartsIsAllowed()
    {
        await using var fixture = await StaffWorkPatternTestFixture.CreateAsync();
        var replacementEmployeeId = await fixture.AddReplacementEmployeeAsync();
        var source = await fixture.AddAssignmentAsync(fixture.Today, fixture.NightShiftId);
        var command = Replacement(source.Id, replacementEmployeeId);
        var preview = await Preview(fixture, command);
        var completed = await Confirm(fixture, command, preview, Guid.NewGuid());

        Assert.Equal("completed", completed.Status);
        Assert.Equal("Cancelled", (await fixture.Context.EmployeeShifts.SingleAsync(item => item.Id == source.Id)).Status);
        Assert.Equal("Scheduled", (await fixture.Context.EmployeeShifts.SingleAsync(item => item.Id == completed.ReplacementAssignmentId)).Status);
    }

    [Fact]
    public async Task MovingOneContractShiftDoesNotCancelOtherShiftsOnThatDateDuringRegeneration()
    {
        await using var fixture = await StaffWorkPatternTestFixture.CreateAsync();
        var contract = Contract(fixture, fixture.Today.AddDays(8), [fixture.MorningShiftId, fixture.AfternoonShiftId]);
        var patternCommand = fixture.PatternCommand(contract, fixture.Today.AddDays(1), 0);
        var patternPreview = await fixture.PreviewAsync(patternCommand);
        await fixture.ConfirmAsync(patternCommand, patternPreview, Guid.NewGuid());
        var workDate = fixture.Today.AddDays(2);
        var source = await Assignment(fixture, workDate, fixture.MorningShiftId);
        var moveCommand = new WorkPatternCommand
        {
            EmployeeId = fixture.EmployeeId,
            ActorId = fixture.AdminId,
            Action = WorkPatternAction.ExceptionPreview,
            Kind = "Move",
            WorkDate = workDate,
            SourceAssignmentIds = [source.Id],
            DestinationShiftId = fixture.NightShiftId,
            Reason = "Synthetic move",
            ExpectedVersion = 1
        };
        var movePreview = Assert.IsType<ScheduleResult>(await fixture.Operations.ExecuteWorkPatternAsync(moveCommand, default));
        moveCommand.Action = WorkPatternAction.ExceptionConfirm;
        moveCommand.PreviewToken = movePreview.PreviewToken;
        moveCommand.OperationId = Guid.NewGuid();
        var move = Assert.IsType<WorkPatternResponse>(await fixture.Operations.ExecuteWorkPatternAsync(moveCommand, default));

        var regenerate = fixture.PatternCommand(contract, workDate, move.Version);
        var regeneratePreview = await fixture.PreviewAsync(regenerate);
        var regenerated = await fixture.ConfirmAsync(regenerate, regeneratePreview, Guid.NewGuid());

        Assert.Equal(0, regenerated.Result.CancelledCount);
        Assert.Equal("Cancelled", (await fixture.Context.EmployeeShifts.SingleAsync(item => item.Id == source.Id)).Status);
        Assert.Equal("Scheduled", (await Assignment(fixture, workDate, fixture.AfternoonShiftId)).Status);
        Assert.Equal("Scheduled", (await Assignment(fixture, workDate, fixture.NightShiftId)).Status);
    }

    [Fact]
    public async Task ReplacementCanTargetAnotherShiftOnDateWithAnIndependentMoveException()
    {
        await using var fixture = await StaffWorkPatternTestFixture.CreateAsync();
        var contract = Contract(fixture, fixture.Today.AddDays(8), [fixture.MorningShiftId, fixture.AfternoonShiftId]);
        var patternCommand = fixture.PatternCommand(contract, fixture.Today.AddDays(1), 0);
        var patternPreview = await fixture.PreviewAsync(patternCommand);
        await fixture.ConfirmAsync(patternCommand, patternPreview, Guid.NewGuid());
        var workDate = fixture.Today.AddDays(2);
        var morning = await Assignment(fixture, workDate, fixture.MorningShiftId);
        var afternoon = await Assignment(fixture, workDate, fixture.AfternoonShiftId);
        var move = new WorkPatternCommand
        {
            EmployeeId = fixture.EmployeeId, ActorId = fixture.AdminId, Action = WorkPatternAction.ExceptionPreview,
            Kind = "Move", WorkDate = workDate, SourceAssignmentIds = [afternoon.Id],
            DestinationShiftId = fixture.NightShiftId, Reason = "Synthetic independent move", ExpectedVersion = 1
        };
        var movePreview = Assert.IsType<ScheduleResult>(await fixture.Operations.ExecuteWorkPatternAsync(move, default));
        move.Action = WorkPatternAction.ExceptionConfirm;
        move.PreviewToken = movePreview.PreviewToken;
        move.OperationId = Guid.NewGuid();
        var moved = Assert.IsType<WorkPatternResponse>(await fixture.Operations.ExecuteWorkPatternAsync(move, default));
        Assert.Equal("completed", moved.Result.Status);
        var replacementEmployeeId = await fixture.AddReplacementEmployeeAsync();

        var replaceCommand = Replacement(morning.Id, replacementEmployeeId);
        var replacePreview = await Preview(fixture, replaceCommand);

        Assert.Equal("ready", replacePreview.Status);
        Assert.Null(replacePreview.ConflictCode);
        var replaced = await Confirm(fixture, replaceCommand, replacePreview, Guid.NewGuid());
        Assert.Equal("completed", replaced.Status);
        Assert.Equal("Scheduled", (await Assignment(fixture, workDate, fixture.NightShiftId)).Status);
    }

    [Fact]
    public async Task ConcurrentConfirmationsWithTheSameOperationIdReturnOneReceiptAndApplyOnce()
    {
        await using var fixture = await StaffWorkPatternTestFixture.CreateAsync();
        await using var secondContextFixture = await StaffWorkPatternTestFixture.CreateAsync();
        var workDate = fixture.Today.AddDays(2);
        var replacementEmployeeId = await fixture.AddReplacementEmployeeAsync();
        var source = await fixture.AddAssignmentAsync(workDate, fixture.MorningShiftId);
        var previewCommand = Replacement(source.Id, replacementEmployeeId);
        var preview = await Preview(fixture, previewCommand);
        var operationId = Guid.NewGuid();
        var firstCommand = Replacement(source.Id, replacementEmployeeId);
        var secondCommand = Replacement(source.Id, replacementEmployeeId);
        foreach (var command in new[] { firstCommand, secondCommand })
        {
            command.Action = StaffShiftReplacementAction.Confirm;
            command.ActorId = fixture.AdminId;
            command.PreviewToken = preview.PreviewToken;
            command.OperationId = operationId;
        }

        var responses = await Task.WhenAll(
            fixture.Operations.ReplaceStaffShiftAsync(firstCommand, default),
            secondContextFixture.Operations.ReplaceStaffShiftAsync(secondCommand, default));

        Assert.All(responses, item => Assert.Equal(operationId, item.OperationId));
        Assert.Equal(responses[0].ReplacementAssignmentId, responses[1].ReplacementAssignmentId);
        Assert.Equal(1, await fixture.Context.StaffScheduleExceptions.CountAsync(item => item.EmployeeId == fixture.EmployeeId &&
            item.Kind == "Replace" && item.WorkDate == workDate));
        Assert.Equal(2, await fixture.Context.StaffScheduleDeliveries.CountAsync(item => item.OperationId == operationId));
        Assert.Equal(1, await fixture.Context.EmployeeShifts.CountAsync(item => item.EmployeeId == replacementEmployeeId &&
            item.WorkDate == workDate && item.ShiftId == fixture.MorningShiftId && item.Status == "Scheduled"));

        var mismatchedReplay = ConfirmRequest(fixture, source.Id, replacementEmployeeId, preview.PreviewToken!, operationId);
        mismatchedReplay.Reason = "Changed payload";
        var mismatchError = await Assert.ThrowsAsync<StaffOnboardingException>(() =>
            fixture.Operations.ReplaceStaffShiftAsync(mismatchedReplay, default));
        Assert.Equal("OPERATION_PAYLOAD_MISMATCH", mismatchError.Code);
    }

    [Theory]
    [InlineData("attended-source", "source_unavailable")]
    [InlineData("started-source", "shift_started")]
    [InlineData("attended-replacement", "destination_assignment_unavailable")]
    [InlineData("replacement-overlap", "destination_overlap")]
    [InlineData("replacement-on-leave", "replacement_on_leave")]
    public async Task PreviewBlocksUnsafeReplacementWithoutChangingAssignments(string scenario, string expectedCode)
    {
        await using var fixture = await StaffWorkPatternTestFixture.CreateAsync();
        var replacementEmployeeId = await fixture.AddReplacementEmployeeAsync();
        var workDate = scenario == "started-source" ? fixture.Today : fixture.Today.AddDays(2);
        var checkIn = scenario == "attended-source" ? fixture.Clock.GetUtcNow().UtcDateTime : (DateTime?)null;
        var sourceStatus = scenario == "attended-source" ? "Present" : "Scheduled";
        var source = await fixture.AddAssignmentAsync(workDate, fixture.MorningShiftId, sourceStatus, checkIn);

        if (scenario == "replacement-overlap")
        {
            var overlappingShiftId = Guid.NewGuid();
            fixture.Context.Shifts.Add(new Shift { Id = overlappingShiftId, Name = "Synthetic overlap", StartTime = new(10, 0), EndTime = new(15, 0), IsActive = true });
            await fixture.Context.SaveChangesAsync();
            await fixture.AddAssignmentAsync(workDate, overlappingShiftId, employeeId: replacementEmployeeId);
        }
        if (scenario == "attended-replacement")
            await fixture.AddAssignmentAsync(workDate, fixture.MorningShiftId, "Present",
                fixture.Clock.GetUtcNow().UtcDateTime, replacementEmployeeId);
        if (scenario == "replacement-on-leave")
        {
            fixture.Context.StaffScheduleExceptions.Add(new StaffScheduleException { EmployeeId = replacementEmployeeId,
                Kind = "Leave", WorkDate = workDate, SourceAssignmentIdsJson = "[]", Reason = "Synthetic leave",
                Version = 1, CreatedBy = fixture.AdminId, CreatedAtUtc = fixture.Clock.GetUtcNow().UtcDateTime });
            await fixture.Context.SaveChangesAsync();
        }

        var result = await Preview(fixture, Replacement(source.Id, replacementEmployeeId));

        Assert.Equal("conflict", result.Status);
        Assert.Equal(expectedCode, result.ConflictCode);
        Assert.Null(result.PreviewToken);
        Assert.Equal(sourceStatus, (await fixture.Context.EmployeeShifts.SingleAsync(item => item.Id == source.Id)).Status);
        Assert.False(await fixture.Context.StaffScheduleExceptions.AnyAsync(item => item.EmployeeId == fixture.EmployeeId && item.Kind == "Replace"));
        Assert.False(await fixture.Context.EmployeeShifts.AnyAsync(item => item.EmployeeId == replacementEmployeeId &&
            item.WorkDate == workDate && item.ShiftId == fixture.MorningShiftId && item.Status == "Scheduled"));
    }

    [Fact]
    public async Task ConfirmationRejectsPreviewAfterReplacementEmployeeScheduleChanges()
    {
        await using var fixture = await StaffWorkPatternTestFixture.CreateAsync();
        var workDate = fixture.Today.AddDays(2);
        var replacementEmployeeId = await fixture.AddReplacementEmployeeAsync();
        var source = await fixture.AddAssignmentAsync(workDate, fixture.MorningShiftId);
        var command = Replacement(source.Id, replacementEmployeeId);
        var preview = await Preview(fixture, command);
        await fixture.AddAssignmentAsync(workDate, fixture.MorningShiftId, employeeId: replacementEmployeeId);
        command.Action = StaffShiftReplacementAction.Confirm;
        command.PreviewToken = preview.PreviewToken;
        command.OperationId = Guid.NewGuid();

        var error = await Assert.ThrowsAsync<StaffOnboardingException>(() => fixture.Operations.ReplaceStaffShiftAsync(command, default));

        Assert.Equal("STALE_PREVIEW", error.Code);
        Assert.Equal("Scheduled", (await fixture.Context.EmployeeShifts.SingleAsync(item => item.Id == source.Id)).Status);
        Assert.False(await fixture.Context.StaffScheduleExceptions.AnyAsync(item => item.EmployeeId == fixture.EmployeeId && item.Kind == "Replace"));
    }

    [Fact]
    public async Task ConcurrentConfirmationsWithOneOperationIdReturnTheSavedReceiptOnce()
    {
        await using var fixture = await StaffWorkPatternTestFixture.CreateAsync();
        await using var secondFixture = await StaffWorkPatternTestFixture.CreateAsync();
        var workDate = fixture.Today.AddDays(2);
        var replacementEmployeeId = await fixture.AddReplacementEmployeeAsync();
        var source = await fixture.AddAssignmentAsync(workDate, fixture.MorningShiftId);
        var preview = await Preview(fixture, Replacement(source.Id, replacementEmployeeId));
        var operationId = Guid.NewGuid();
        var requests = new[]
        {
            ConfirmRequest(fixture, source.Id, replacementEmployeeId, preview.PreviewToken!, operationId),
            ConfirmRequest(fixture, source.Id, replacementEmployeeId, preview.PreviewToken!, operationId)
        };

        var responses = await Task.WhenAll(
            fixture.Operations.ReplaceStaffShiftAsync(requests[0], default),
            secondFixture.Operations.ReplaceStaffShiftAsync(requests[1], default));

        Assert.All(responses, item => Assert.Equal("completed", item.Status));
        Assert.Equal(responses[0].ReplacementAssignmentId, responses[1].ReplacementAssignmentId);
        Assert.Equal(1, await fixture.Context.StaffScheduleExceptions.CountAsync(item => item.EmployeeId == fixture.EmployeeId &&
            item.WorkDate == workDate && item.Kind == "Replace"));
        Assert.Equal(2, await fixture.Context.StaffScheduleDeliveries.CountAsync(item => item.OperationId == operationId));
        Assert.Equal(1, await fixture.Context.EmployeeShifts.CountAsync(item => item.EmployeeId == replacementEmployeeId &&
            item.WorkDate == workDate && item.ShiftId == fixture.MorningShiftId && item.Status == "Scheduled"));
    }

    [Fact]
    public async Task ReplacementRejectsCheckInCommittedAfterPreviewAndBeforeSourceRowLock()
    {
        var utcNow = new DateTimeOffset(2026, 11, 6, 22, 45, 0, TimeSpan.Zero);
        await using var fixture = await StaffWorkPatternTestFixture.CreateAsync(utcNow);
        await using var previewFixture = await StaffWorkPatternTestFixture.CreateAsync(utcNow);
        await using var confirmFixture = await StaffWorkPatternTestFixture.CreateAsync(utcNow);
        await using var lockFixture = await StaffWorkPatternTestFixture.CreateAsync(utcNow);
        var replacementEmployeeId = await fixture.AddReplacementEmployeeAsync();
        var source = await fixture.AddAssignmentAsync(fixture.Today, fixture.MorningShiftId);
        var preview = await Preview(previewFixture, Replacement(source.Id, replacementEmployeeId));
        var confirm = ConfirmRequest(confirmFixture, source.Id, replacementEmployeeId, preview.PreviewToken!, Guid.NewGuid());

        using var lockTimeout = new CancellationTokenSource(TimeSpan.FromSeconds(15));
        Task<StaffShiftReplacementResult> confirmationTask;
        DateTime checkInAt;
        await using (var rowLock = await lockFixture.Context.Database.BeginTransactionAsync())
        {
            await lockFixture.Context.Database.ExecuteSqlInterpolatedAsync($"""
                SELECT id FROM smart_pool.employee_shifts WHERE id = {source.Id} FOR UPDATE
                """);
            confirmationTask = confirmFixture.Operations.ReplaceStaffShiftAsync(confirm, lockTimeout.Token);
            try
            {
                await using (var observer = new NpgsqlConnection(IntegrationTestDatabase.GetConnectionString()))
                {
                    await observer.OpenAsync();
                    var deadline = DateTimeOffset.UtcNow.AddSeconds(10);
                    var replacementIsWaitingForSource = false;
                    while (!replacementIsWaitingForSource && DateTimeOffset.UtcNow < deadline)
                    {
                        await using var command = new NpgsqlCommand("""
                            SELECT count(*) FROM pg_stat_activity
                            WHERE datname = current_database() AND wait_event_type = 'Lock'
                                AND query LIKE '%employee_shifts%FOR UPDATE%'
                            """, observer);
                        replacementIsWaitingForSource = Convert.ToInt64(await command.ExecuteScalarAsync()) > 0;
                        if (!replacementIsWaitingForSource) await Task.Delay(25);
                    }
                    Assert.True(replacementIsWaitingForSource, "Replacement confirmation did not wait on the held assignment row.");
                }

                checkInAt = utcNow.AddMinutes(5).UtcDateTime;
                await lockFixture.Context.Database.ExecuteSqlInterpolatedAsync($"""
                    UPDATE smart_pool.employee_shifts
                    SET status = 'Present', check_in_time = {checkInAt}, check_in_method = 'Manual', updated_at = {checkInAt}
                    WHERE id = {source.Id}
                    """);
                await rowLock.CommitAsync();
            }
            catch
            {
                lockTimeout.Cancel();
                await rowLock.RollbackAsync();
                try { await confirmationTask; } catch { }
                throw;
            }
        }

        var error = await Assert.ThrowsAsync<StaffOnboardingException>(() => confirmationTask);
        var persisted = await confirmFixture.Context.EmployeeShifts.AsNoTracking().SingleAsync(item => item.Id == source.Id);
        Assert.Equal("STALE_PREVIEW", error.Code);
        Assert.Equal("Present", persisted.Status);
        Assert.Equal(checkInAt, persisted.CheckInTime);
        Assert.False(await fixture.Context.StaffScheduleExceptions.AnyAsync(item => item.EmployeeId == fixture.EmployeeId && item.Kind == "Replace"));
    }

    [Fact]
    public async Task ReplacementHistoryRejectsANonexistentDestinationEmployee()
    {
        await using var fixture = await StaffWorkPatternTestFixture.CreateAsync();
        fixture.Context.StaffScheduleExceptions.Add(new StaffScheduleException
        {
            EmployeeId = fixture.EmployeeId,
            Kind = "Replace",
            WorkDate = fixture.Today.AddDays(2),
            SourceAssignmentIdsJson = "[]",
            DestinationShiftId = fixture.MorningShiftId,
            DestinationEmployeeId = Guid.NewGuid(),
            Reason = "Synthetic invalid reference",
            Version = 1,
            CreatedBy = fixture.AdminId,
            CreatedAtUtc = fixture.Clock.GetUtcNow().UtcDateTime
        });

        await Assert.ThrowsAsync<DbUpdateException>(() => fixture.Context.SaveChangesAsync());
    }

    private static ContractTerms Contract(StaffWorkPatternTestFixture fixture, DateOnly endDate, IReadOnlyList<Guid> shifts) =>
        new("FULL_TIME", fixture.Today, endDate, Enumerable.Range(1, 7).Select(day => new WeeklyPatternDay(day, shifts)).ToList());

    private static Task<EmployeeShift> Assignment(StaffWorkPatternTestFixture fixture, DateOnly date, Guid shiftId) =>
        fixture.Context.EmployeeShifts.SingleAsync(item => item.EmployeeId == fixture.EmployeeId && item.WorkDate == date && item.ShiftId == shiftId);

    private static ReplaceStaffShiftCommand Replacement(Guid sourceAssignmentId, Guid replacementEmployeeId) => new()
    {
        SourceAssignmentId = sourceAssignmentId,
        ReplacementEmployeeId = replacementEmployeeId,
        ActorId = Guid.Empty,
        Reason = "Synthetic staff replacement"
    };

    private static ReplaceStaffShiftCommand ConfirmRequest(StaffWorkPatternTestFixture fixture,
        Guid sourceAssignmentId, Guid replacementEmployeeId, string previewToken, Guid operationId) => new()
    {
        Action = StaffShiftReplacementAction.Confirm,
        SourceAssignmentId = sourceAssignmentId,
        ReplacementEmployeeId = replacementEmployeeId,
        ActorId = fixture.AdminId,
        Reason = "Synthetic staff replacement",
        PreviewToken = previewToken,
        OperationId = operationId
    };

    private static async Task<StaffShiftReplacementResult> Preview(StaffWorkPatternTestFixture fixture, ReplaceStaffShiftCommand command)
    {
        command.Action = StaffShiftReplacementAction.Preview;
        command.ActorId = fixture.AdminId;
        return await fixture.Operations.ReplaceStaffShiftAsync(command, default);
    }

    private static async Task<StaffShiftReplacementResult> Confirm(StaffWorkPatternTestFixture fixture,
        ReplaceStaffShiftCommand command, StaffShiftReplacementResult preview, Guid operationId)
    {
        command.Action = StaffShiftReplacementAction.Confirm;
        command.ActorId = fixture.AdminId;
        command.PreviewToken = preview.PreviewToken;
        command.OperationId = operationId;
        return await fixture.Operations.ReplaceStaffShiftAsync(command, default);
    }
}
