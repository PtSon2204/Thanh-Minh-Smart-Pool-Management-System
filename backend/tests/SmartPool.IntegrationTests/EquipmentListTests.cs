using SmartPool.Infrastructure.Persistence.Repositories.Equipments;
using Microsoft.EntityFrameworkCore;
using SmartPool.Application.Features.ManageEquipments.Queries.GetEquipments;
using SmartPool.Domain.Entities;
using SmartPool.Infrastructure.Persistence.DbContext;
using SmartPool.Infrastructure.Persistence.Repositories;

namespace SmartPool.IntegrationTests;

public sealed class EquipmentListTests
{
    [Fact]
    public async Task List_CombinesDatabaseFiltersLiteralSearchAndStablePagination()
    {
        await using var db = new SmartPoolDbContext(await IntegrationTestDatabase.CreateOptionsAsync());
        await using var transaction = await db.Database.BeginTransactionAsync();
        var prefix = Guid.NewGuid().ToString("N");
        var first = Device(prefix + " Pump", prefix + " Filter", prefix + " Pool");
        var second = Device(first.Name, first.Category, first.Location!);
        var third = Device(prefix + " Camera", prefix + " Camera", prefix + " Gate");
        second.Status = "Maintenance";
        third.Status = "Inactive";
        var deleted = Device(prefix + " Deleted", first.Category, first.Location!);
        deleted.IsDeleted = true;
        var wildcard = Device(prefix + " 100%_pump", first.Category, first.Location!);
        db.Equipments.AddRange(first, second, third, deleted, wildcard);
        await db.SaveChangesAsync();
        var repository = new EquipmentOperations(db);
        var today = new DateOnly(2026, 10, 15);

        async Task<int> Count(GetEquipmentsQuery query) =>
            (await repository.GetListAsync(query, today, CancellationToken.None)).TotalCount;

        Assert.Equal(4, await Count(new() { SearchTerm = prefix }));
        Assert.Equal(3, await Count(new() { SearchTerm = prefix.ToUpperInvariant() + " pool" }));
        Assert.Equal(3, await Count(new() { Category = first.Category }));
        Assert.Equal(3, await Count(new() { Location = first.Location }));
        Assert.Equal(1, await Count(new() { SearchTerm = prefix, Category = third.Category, Location = third.Location, Status = "Inactive" }));
        Assert.Equal(2, await Count(new() { SearchTerm = prefix, Status = "Active" }));
        Assert.Equal(1, await Count(new() { SearchTerm = prefix, Status = "Maintenance" }));
        Assert.Equal(1, await Count(new() { SearchTerm = prefix, Status = "Inactive" }));
        Assert.Equal(0, await Count(new() { SearchTerm = prefix, Category = third.Category, Location = first.Location }));
        Assert.Equal(1, await Count(new() { SearchTerm = "100%_pump", Category = first.Category }));
        Assert.Equal(0, await Count(new() { SearchTerm = prefix, Status = "no-matching-status" }));

        var pageOne = await repository.GetListAsync(new() { SearchTerm = first.Name, PageSize = 1 }, today, CancellationToken.None);
        var pageTwo = await repository.GetListAsync(new() { SearchTerm = first.Name, PageSize = 1, PageIndex = 2 }, today, CancellationToken.None);
        Assert.Equal(2, pageOne.TotalCount);
        Assert.Equal(2, pageOne.TotalPages);
        Assert.NotEqual(Assert.Single(pageOne.Items).Id, Assert.Single(pageTwo.Items).Id);
        Assert.Empty((await repository.GetListAsync(new() { SearchTerm = first.Name, PageIndex = 3, PageSize = 1 }, today, CancellationToken.None)).Items);

        var options = await repository.GetFilterOptionsAsync(CancellationToken.None);
        Assert.Contains(first.Category, options.Categories);
        Assert.Contains(first.Location!, options.Locations);
        Assert.Contains("Active", options.Statuses);
        Assert.Contains("Maintenance", options.Statuses);
        Assert.Contains("Inactive", options.Statuses);
        await transaction.RollbackAsync();
    }

    [Fact]
    public async Task List_ProjectsMetadataAndNearestPendingMaintenanceWithoutCompletedOrCancelledSchedules()
    {
        await using var db = new SmartPoolDbContext(await IntegrationTestDatabase.CreateOptionsAsync());
        await using var transaction = await db.Database.BeginTransactionAsync();
        var prefix = Guid.NewGuid().ToString("N");
        var device = Device(prefix + " Pump", prefix, prefix);
        device.Metadata = "{\"power\":\"2kW\",\"dimensions\":{\"width\":20}}";
        var noSchedule = Device(prefix + " Camera", prefix, prefix);
        var today = new DateOnly(2026, 10, 15);
        db.Equipments.AddRange(device, noSchedule);
        db.MaintenanceSchedules.AddRange(
            Schedule(device, today.AddDays(-1), "Pending"),
            Schedule(device, today, "Completed"),
            Schedule(device, today, "Cancelled"),
            Schedule(device, today.AddDays(2), "Pending"),
            Schedule(device, today.AddDays(5), "Pending"));
        var loggedSchedule = Schedule(device, today, "Pending");
        db.MaintenanceSchedules.Add(loggedSchedule);
        db.MaintenanceLogs.Add(new MaintenanceLog
        {
            Id = Guid.NewGuid(), EquipmentId = device.Id, ScheduleId = loggedSchedule.Id, ActualDate = today.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc)
        });
        await db.SaveChangesAsync();

        var repository = new EquipmentOperations(db);
        var result = await repository.GetListAsync(new() { Category = prefix }, today, CancellationToken.None);
        Assert.Equal(2, result.TotalCount);
        var pump = Assert.Single(result.Items, x => x.Id == device.Id);
        Assert.Equal(today.AddDays(2), pump.NextMaintenanceDate);
        Assert.Contains("2kW", pump.Metadata!);
        Assert.Null(Assert.Single(result.Items, x => x.Id == noSchedule.Id).NextMaintenanceDate);

        db.MaintenanceSchedules.Add(Schedule(noSchedule, today, "Pending"));
        await db.SaveChangesAsync();
        result = await repository.GetListAsync(new() { Category = prefix }, today, CancellationToken.None);
        Assert.Equal(today, Assert.Single(result.Items, x => x.Id == noSchedule.Id).NextMaintenanceDate);
        Assert.Empty((await repository.GetListAsync(new() { Category = prefix + "-missing" }, today, CancellationToken.None)).Items);
        await transaction.RollbackAsync();
    }

    private static Equipment Device(string name, string category, string location) => new()
    {
        Id = Guid.NewGuid(), Name = name, Category = category, Location = location, Status = "Active", IsDeleted = false
    };

    private static MaintenanceSchedule Schedule(Equipment device, DateOnly date, string status) => new()
    {
        Id = Guid.NewGuid(), EquipmentId = device.Id, PlannedDate = date, Status = status
    };
}
