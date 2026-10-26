using SmartPool.Infrastructure.Persistence.Repositories.Staff;
using Microsoft.EntityFrameworkCore;
using SmartPool.Application.Features.ManageStaffs;
using SmartPool.Application.Features.ManageStaffs.Commands.SaveShift;
using SmartPool.Domain.Entities;
using SmartPool.Infrastructure.Persistence.DbContext;
using SmartPool.Infrastructure.Persistence.Repositories;

namespace SmartPool.IntegrationTests;

public sealed class ShiftEditRestrictionsTests
{
    [Fact]
    public async Task GivenUnusedShift_WhenUpdatingTimes_ThenRejectsWithoutChangingHistory()
    {
        await using var fixture = await ShiftFixture.CreateAsync();
        var operations = fixture.Operations;
        var updatedStart = new TimeOnly(10, 0);
        var updatedEnd = new TimeOnly(18, 0);

        await Assert.ThrowsAsync<StaffConflictException>(() => operations.SaveShiftAsync(
            fixture.Command(updatedStart, updatedEnd), CancellationToken.None));

        var persisted = await fixture.ReadShiftAsync();
        Assert.Equal(fixture.OriginalStart, persisted.StartTime);
        Assert.Equal(fixture.OriginalEnd, persisted.EndTime);
    }

    [Fact]
    public async Task GivenCancelledAssignment_WhenUpdatingShiftTimes_ThenRejectsWithoutPersistingChanges()
    {
        await using var fixture = await ShiftFixture.CreateAsync();
        await fixture.AddAssignmentAsync("Cancelled");

        await Assert.ThrowsAsync<StaffConflictException>(() => fixture.Operations.SaveShiftAsync(
            fixture.Command(new TimeOnly(10, 0), new TimeOnly(18, 0)), CancellationToken.None));

        var persisted = await fixture.ReadShiftAsync();
        Assert.Equal(fixture.OriginalStart, persisted.StartTime);
        Assert.Equal(fixture.OriginalEnd, persisted.EndTime);
        Assert.Equal("Original shift", persisted.Name);
        Assert.True(persisted.IsActive);
    }

    [Fact]
    public async Task GivenReferencedShift_WhenUpdatingNameAndStatus_ThenRejectsWithoutChangingHistory()
    {
        await using var fixture = await ShiftFixture.CreateAsync();
        await fixture.AddAssignmentAsync("Scheduled");

        await Assert.ThrowsAsync<StaffConflictException>(() => fixture.Operations.SaveShiftAsync(
            fixture.Command(fixture.OriginalStart, fixture.OriginalEnd, "Renamed shift", false), CancellationToken.None));

        var persisted = await fixture.ReadShiftAsync();
        Assert.Equal("Original shift", persisted.Name);
        Assert.True(persisted.IsActive);
        Assert.Equal(fixture.OriginalStart, persisted.StartTime);
        Assert.Equal(fixture.OriginalEnd, persisted.EndTime);
    }

    [Fact]
    public async Task GivenCustomShift_WhenCreating_ThenRejectsWithoutInserting()
    {
        await using var fixture = await ShiftFixture.CreateAsync();
        var name = $"Custom shift {Guid.NewGuid():N}";
        var command = fixture.Command(new TimeOnly(9, 0), new TimeOnly(17, 0), name);
        command.Id = null;

        await Assert.ThrowsAsync<StaffConflictException>(() => fixture.Operations.SaveShiftAsync(command, CancellationToken.None));

        await using var read = fixture.CreateContext();
        Assert.False(await read.Shifts.AnyAsync(shift => shift.Name == name));
    }

    private sealed class ShiftFixture : IAsyncDisposable
    {
        private readonly StaffEmploymentFixture employment;
        private readonly SmartPoolDbContext context;
        private readonly Guid shiftId = Guid.NewGuid();

        private ShiftFixture(StaffEmploymentFixture employment)
        {
            this.employment = employment;
            context = employment.CreateContext();
            Operations = employment.CreateOperations(context);
        }

        public StaffOperations Operations { get; }
        public TimeOnly OriginalStart { get; } = new(8, 0);
        public TimeOnly OriginalEnd { get; } = new(16, 0);
        public SmartPoolDbContext CreateContext() => employment.CreateContext();

        public static async Task<ShiftFixture> CreateAsync()
        {
            var employment = await StaffEmploymentFixture.CreateAsync();
            var fixture = new ShiftFixture(employment);
            fixture.context.Shifts.Add(new Shift
            {
                Id = fixture.shiftId,
                Name = "Original shift",
                StartTime = fixture.OriginalStart,
                EndTime = fixture.OriginalEnd,
                IsActive = true
            });
            await fixture.context.SaveChangesAsync();
            return fixture;
        }

        public SaveShiftCommand Command(TimeOnly start, TimeOnly end, string name = "Changed shift", bool isActive = true) => new()
        {
            Id = shiftId,
            Name = name,
            StartTime = start,
            EndTime = end,
            IsActive = isActive
        };

        public async Task AddAssignmentAsync(string status)
        {
            context.EmployeeShifts.Add(new EmployeeShift
            {
                Id = Guid.NewGuid(),
                EmployeeId = employment.AttachedStaffUserId,
                ShiftId = shiftId,
                WorkDate = new DateOnly(2026, 10, 14),
                Status = status,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            });
            await context.SaveChangesAsync();
        }

        public async Task<Shift> ReadShiftAsync()
        {
            await using var readContext = employment.CreateContext();
            return await readContext.Shifts.AsNoTracking().SingleAsync(shift => shift.Id == shiftId);
        }

        public async ValueTask DisposeAsync()
        {
            await context.EmployeeShifts.Where(item => item.ShiftId == shiftId).ExecuteDeleteAsync();
            await context.Shifts.Where(item => item.Id == shiftId).ExecuteDeleteAsync();
            await context.DisposeAsync();
            await employment.DisposeAsync();
        }
    }
}
