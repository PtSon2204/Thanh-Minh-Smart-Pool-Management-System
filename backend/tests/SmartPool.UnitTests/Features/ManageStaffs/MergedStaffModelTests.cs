using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using SmartPool.Domain.Entities;
using SmartPool.Infrastructure.Persistence.DbContext;

namespace SmartPool.UnitTests.Features.ManageStaffs;

public sealed class MergedStaffModelTests
{
    [Fact]
    public void GivenMergedAttendanceAndOperations_WhenBuildingModel_ThenBothFeaturesRemainMapped()
    {
        var options = new DbContextOptionsBuilder<SmartPoolDbContext>()
            .UseNpgsql("Host=localhost;Database=smartpool_test;Username=model_check;Password=unused")
            .Options;
        using var context = new SmartPoolDbContext(options);
        var snapshot = context.GetService<IMigrationsAssembly>().ModelSnapshot;
        Assert.NotNull(snapshot);

        foreach (var type in new[] { typeof(AttendanceLog), typeof(Equipment), typeof(Rental), typeof(WaterQualityLog) })
        {
            var entity = context.Model.FindEntityType(type);
            var snapshotEntity = snapshot.Model.FindEntityType(type.FullName!);
            Assert.NotNull(entity);
            Assert.NotNull(snapshotEntity);
            Assert.Equal(entity.GetTableName(), snapshotEntity.GetTableName());
            Assert.Equal(entity.GetSchema(), snapshotEntity.GetSchema());
        }

        Assert.Null(context.Model.FindEntityType("SmartPool.Domain.Entities.Salary"));
        Assert.Null(snapshot.Model.FindEntityType("SmartPool.Domain.Entities.Salary"));
        foreach (var removedEntity in new[] { "GoodsReceipt", "GoodsReceiptDetail" })
        {
            Assert.Null(context.Model.FindEntityType($"SmartPool.Domain.Entities.{removedEntity}"));
            Assert.Null(snapshot.Model.FindEntityType($"SmartPool.Domain.Entities.{removedEntity}"));
        }
        Assert.NotNull(context.Model.FindEntityType(typeof(EmployeeShift))!.FindProperty("CheckInMethod"));
        Assert.NotNull(context.Model.FindEntityType(typeof(EmployeeShift))!.FindProperty("CheckOutMethod"));

        var sql = context.Database.GenerateCreateScript();
        Assert.Contains("attendance_logs", sql);
        Assert.Contains("equipments", sql);
        Assert.Contains("rentals", sql);
        Assert.DoesNotContain("goods_receipts", sql);
        Assert.Contains("water_quality_logs", sql);
    }
}
