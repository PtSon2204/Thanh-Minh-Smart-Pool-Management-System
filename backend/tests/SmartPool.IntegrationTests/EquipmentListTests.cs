using Microsoft.EntityFrameworkCore;
using SmartPool.Domain.Entities;
using SmartPool.Infrastructure.Persistence.DbContext;
using SmartPool.Infrastructure.Persistence.Repositories.Equipments;
using Xunit;

namespace SmartPool.IntegrationTests;

[Collection("Database")]
public class EquipmentListTests
{
    [Fact]
    public async Task SearchAndFilter_UseDatabaseIndices()
    {
        await using var db = new SmartPoolDbContext(await IntegrationTestDatabase.CreateOptionsAsync());
        await using var transaction = await db.Database.BeginTransactionAsync();
        var prefix = Guid.NewGuid().ToString("N");
        var camera = Device(prefix + " Camera 01", "Camera", "Area A");
        var pump = Device(prefix + " Pump 01", "Pump", "Area B");
        var filter = Device(prefix + " Filter 01", "Filter", "Area B");
        filter.Status = "Inactive";
        db.Equipments.AddRange(camera, pump, filter);
        await db.SaveChangesAsync();

        var repository = new EquipmentOperations(db);
        var today = new DateOnly(2026, 10, 15);

        // Search by prefix
        var result = await repository.GetListAsync(new() { SearchTerm = prefix }, today, CancellationToken.None);
        Assert.Equal(3, result.TotalCount);

        // Filter by category
        result = await repository.GetListAsync(new() { SearchTerm = prefix, Category = "Pump" }, today, CancellationToken.None);
        Assert.Equal(1, result.TotalCount);
        Assert.Equal(pump.Id, result.Items[0].Id);

        // Filter by location
        result = await repository.GetListAsync(new() { SearchTerm = prefix, Location = "Area B" }, today, CancellationToken.None);
        Assert.Equal(2, result.TotalCount);

        // Filter by status
        result = await repository.GetListAsync(new() { SearchTerm = prefix, Status = "Inactive" }, today, CancellationToken.None);
        Assert.Equal(1, result.TotalCount);
        Assert.Equal(filter.Id, result.Items[0].Id);

        // Filter by missing
        result = await repository.GetListAsync(new() { SearchTerm = prefix + "-missing" }, today, CancellationToken.None);
        Assert.Empty(result.Items);

        await transaction.RollbackAsync();
    }

    [Fact]
    public async Task List_ReturnsEquipmentItems()
    {
        await using var db = new SmartPoolDbContext(await IntegrationTestDatabase.CreateOptionsAsync());
        await using var transaction = await db.Database.BeginTransactionAsync();
        var prefix = Guid.NewGuid().ToString("N");
        var device = Device(prefix + " Pump", prefix, prefix);
        device.Metadata = "{\"power\":\"2kW\",\"dimensions\":{\"width\":20}}";
        var noSchedule = Device(prefix + " Camera", prefix, prefix);
        var today = new DateOnly(2026, 10, 15);
        db.Equipments.AddRange(device, noSchedule);
        await db.SaveChangesAsync();

        var repository = new EquipmentOperations(db);
        var result = await repository.GetListAsync(new() { Category = prefix }, today, CancellationToken.None);
        Assert.Equal(2, result.TotalCount);
        var pump = Assert.Single(result.Items, x => x.Id == device.Id);
        Assert.Contains("2kW", pump.Metadata!);
        Assert.Null(pump.NextMaintenanceDate);

        Assert.Empty((await repository.GetListAsync(new() { Category = prefix + "-missing" }, today, CancellationToken.None)).Items);
        await transaction.RollbackAsync();
    }

    private static Equipment Device(string name, string category, string location) => new()
    {
        Id = Guid.NewGuid(), Name = name, Category = category, Location = location, Status = "Active", IsDeleted = false
    };
}
