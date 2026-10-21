using SmartPool.Infrastructure.Persistence.Repositories.Staff;
using MediatR;
using Microsoft.EntityFrameworkCore;
using SmartPool.Application.Features.ManageStaffs;
using SmartPool.Application.Features.ManageStaffs.Commands.AssignShift;
using SmartPool.Application.Features.ManageStaffs.Commands.AssignShiftBatch;
using SmartPool.Domain.Entities;
using SmartPool.Infrastructure.Persistence.Repositories;

namespace SmartPool.IntegrationTests;

public sealed partial class StaffScheduleBatchTests
{
    [Fact]
    public async Task GivenSingleAndBatchRace_WhenTheyTargetExactTuple_ThenOneAssignmentIsCreated()
    {
        await using var fixture = await StaffEmploymentFixture.CreateAsync();
        var shift = CreateShift(new TimeOnly(8, 0), new TimeOnly(12, 0));
        var workDate = new DateOnly(2026, 11, 8);
        try
        {
            await AddShiftsAsync(fixture, shift);
            using var gate = new Barrier(2);

            async Task<bool> AssignSingleAsync()
            {
                await using var operationContext = fixture.CreateContext();
                Assert.True(await Task.Run(() => gate.SignalAndWait(TimeSpan.FromSeconds(10))));
                try
                {
                    await fixture.CreateOperations(operationContext).AssignShiftAsync(
                        Single(fixture.AttachedStaffUserId, shift.Id, workDate), CancellationToken.None);
                    return true;
                }
                catch (StaffConflictException)
                {
                    return false;
                }
            }

            async Task<bool> AssignBatchAsync()
            {
                await using var operationContext = fixture.CreateContext();
                Assert.True(await Task.Run(() => gate.SignalAndWait(TimeSpan.FromSeconds(10))));
                try
                {
                    await fixture.CreateOperations(operationContext).AssignShiftBatchAsync(
                        Batch(Item(fixture.AttachedStaffUserId, shift.Id, workDate)), CancellationToken.None);
                    return true;
                }
                catch (AssignShiftBatchConflictException)
                {
                    return false;
                }
            }

            var outcomes = await Task.WhenAll(AssignSingleAsync(), AssignBatchAsync());

            Assert.Single(outcomes, outcome => outcome);
            Assert.Equal(1, await CountAssignmentsAsync(fixture, [shift.Id]));
        }
        finally
        {
            await CleanupScheduleAsync(fixture, [shift.Id], 1);
        }
    }

    [Fact]
    public async Task GivenTwoBatchRace_WhenTheyTargetExactTuple_ThenOneBatchIsCreated()
    {
        await using var fixture = await StaffEmploymentFixture.CreateAsync();
        var shift = CreateShift(new TimeOnly(13, 0), new TimeOnly(17, 0));
        var workDate = new DateOnly(2026, 11, 9);
        try
        {
            await AddShiftsAsync(fixture, shift);
            using var gate = new Barrier(2);

            async Task<bool> AssignAsync()
            {
                await using var operationContext = fixture.CreateContext();
                Assert.True(await Task.Run(() => gate.SignalAndWait(TimeSpan.FromSeconds(10))));
                try
                {
                    await fixture.CreateOperations(operationContext).AssignShiftBatchAsync(
                        Batch(Item(fixture.AttachedStaffUserId, shift.Id, workDate)), CancellationToken.None);
                    return true;
                }
                catch (AssignShiftBatchConflictException)
                {
                    return false;
                }
            }

            var outcomes = await Task.WhenAll(AssignAsync(), AssignAsync());

            Assert.Single(outcomes, outcome => outcome);
            Assert.Equal(1, await CountAssignmentsAsync(fixture, [shift.Id]));
        }
        finally
        {
            await CleanupScheduleAsync(fixture, [shift.Id], 1);
        }
    }

    private static AssignShiftCommand Single(Guid employeeId, Guid shiftId, DateOnly workDate) => new()
    {
        EmployeeId = employeeId,
        ShiftId = shiftId,
        WorkDate = workDate
    };

    private static AssignShiftBatchCommand Batch(params AssignShiftBatchItem[] items) => new() { Assignments = items };

    private static AssignShiftBatchItem Item(Guid employeeId, Guid shiftId, DateOnly workDate) => new()
    {
        EmployeeId = employeeId,
        ShiftId = shiftId,
        WorkDate = workDate
    };

    private static Shift CreateShift(TimeOnly startTime, TimeOnly endTime) => new()
    {
        Id = Guid.NewGuid(),
        Name = $"Batch test {Guid.NewGuid():N}",
        StartTime = startTime,
        EndTime = endTime,
        IsActive = true
    };

    private static EmployeeShift CreateAssignment(Guid employeeId, Guid shiftId, DateOnly workDate) => new()
    {
        Id = Guid.NewGuid(), EmployeeId = employeeId, ShiftId = shiftId, WorkDate = workDate,
        Status = "Scheduled", CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow
    };

    private static async Task AddShiftsAsync(StaffEmploymentFixture fixture, params Shift[] shifts)
    {
        await using var setupContext = fixture.CreateContext();
        setupContext.Shifts.AddRange(shifts);
        await setupContext.SaveChangesAsync();
    }

    private static async Task AddScheduleAsync(StaffEmploymentFixture fixture, Shift[] shifts, EmployeeShift[] assignments)
    {
        await using var setupContext = fixture.CreateContext();
        setupContext.Shifts.AddRange(shifts);
        setupContext.EmployeeShifts.AddRange(assignments);
        await setupContext.SaveChangesAsync();
    }

    private static async Task<int> CountAssignmentsAsync(StaffEmploymentFixture fixture, Guid[] shiftIds)
    {
        await using var readContext = fixture.CreateContext();
        return await readContext.EmployeeShifts.CountAsync(item => shiftIds.Contains(item.ShiftId));
    }

    private static async Task<List<AssignmentSnapshot>> ReadAssignmentsAsync(StaffEmploymentFixture fixture, Guid[] shiftIds)
    {
        await using var readContext = fixture.CreateContext();
        return await readContext.EmployeeShifts.AsNoTracking()
            .Where(item => shiftIds.Contains(item.ShiftId))
            .OrderBy(item => item.Id)
            .Select(item => new AssignmentSnapshot(item.Id, item.EmployeeId, item.ShiftId, item.WorkDate, item.Status))
            .ToListAsync();
    }

    private static async Task CleanupScheduleAsync(StaffEmploymentFixture fixture, Guid[] shiftIds, int expectedEmployees)
    {
        await using var cleanupContext = fixture.CreateContext();
        await cleanupContext.EmployeeShifts.Where(item => shiftIds.Contains(item.ShiftId)).ExecuteDeleteAsync();
        await cleanupContext.Shifts.Where(item => shiftIds.Contains(item.Id)).ExecuteDeleteAsync();
        await fixture.CleanupAsync();
        Assert.Equal((expectedEmployees, 7, 7), fixture.CleanupCounts);
    }

    private sealed record AssignmentSnapshot(Guid Id, Guid EmployeeId, Guid ShiftId, DateOnly WorkDate, string? Status);

    private sealed class OperationsSender(StaffOperations operations) : ISender
    {
        public async Task<TResponse> Send<TResponse>(IRequest<TResponse> request, CancellationToken cancellationToken = default)
        {
            object response = request switch
            {
                AssignShiftCommand command => await operations.AssignShiftAsync(command, cancellationToken),
                AssignShiftBatchCommand command => await operations.AssignShiftBatchAsync(command, cancellationToken),
                _ => throw new InvalidOperationException("Unexpected request type.")
            };
            return (TResponse)response;
        }

        public Task Send<TRequest>(TRequest request, CancellationToken cancellationToken = default) where TRequest : IRequest =>
            throw new InvalidOperationException("Unexpected request type.");

        public Task<object?> Send(object request, CancellationToken cancellationToken = default) =>
            throw new InvalidOperationException("Unexpected request type.");

        public IAsyncEnumerable<TResponse> CreateStream<TResponse>(IStreamRequest<TResponse> request, CancellationToken cancellationToken = default) =>
            throw new InvalidOperationException("Unexpected stream request.");

        public IAsyncEnumerable<object?> CreateStream(object request, CancellationToken cancellationToken = default) =>
            throw new InvalidOperationException("Unexpected stream request.");
    }
}
