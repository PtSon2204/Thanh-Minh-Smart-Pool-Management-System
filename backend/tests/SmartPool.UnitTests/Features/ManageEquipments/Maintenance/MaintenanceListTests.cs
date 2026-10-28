using MediatR;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using SmartPool.API.Controllers;
using SmartPool.Application.Common.Models;
using SmartPool.Application.Features.ManageEquipments.Maintenance;
using SmartPool.Application.Features.ManageEquipments.Maintenance.Queries.GetMaintenanceSchedules;
using SmartPool.Application.Interfaces.Repositories;
using SmartPool.Infrastructure.Persistence.DbContext;
using SmartPool.Infrastructure.Persistence.Repositories.Maintenance;

namespace SmartPool.UnitTests.Features.ManageEquipments.Maintenance;

public sealed class MaintenanceListTests
{
    [Fact(DisplayName = "UTCID01 (N) - Query danh sách có search/filter/pagination -> Forward nguyên query và trả danh sách")]
    public async Task UTCID01_ListValidQuery_DelegatesAndReturnsList()
    {
        var query = new GetMaintenanceSchedulesQuery
        {
            SearchTerm = "máy bơm",
            Status = "Overdue",
            EquipmentId = Guid.NewGuid(),
            PageIndex = 2,
            PageSize = 20
        };
        var expected = new MaintenanceList(
            new PagedResponse<MaintenanceItem>
            {
                Items = [new(Guid.NewGuid(), query.EquipmentId.Value, "Máy bơm", "Khu A", "Bảo dưỡng",
                    "Month", 1, new DateOnly(2026, 10, 27), Guid.NewGuid(), "Nhân viên A", 3, "Pending", true, null)],
                TotalCount = 21,
                PageIndex = 2,
                PageSize = 20
            },
            new MaintenanceSummary(21, 8, 3, 10));
        var sender = new Mock<ISender>(MockBehavior.Strict);
        using var cts = new CancellationTokenSource();
        sender.Setup(s => s.Send(query, cts.Token)).ReturnsAsync(expected);

        var result = await Controller(sender).List(query, cts.Token);

        var ok = Assert.IsType<OkObjectResult>(result);
        Assert.Same(expected, ok.Value);
        Assert.Equal(21, expected.Page.TotalCount);
        Assert.Equal("máy bơm", query.SearchTerm);
        Assert.Equal("Overdue", query.Status);
        Assert.Equal(2, query.PageIndex);
        Assert.Equal(20, query.PageSize);
        sender.Verify(s => s.Send(query, cts.Token), Times.Once);
        sender.VerifyNoOtherCalls();
    }

    [Fact(DisplayName = "UTCID02 (A) - GetListAsync ném MaintenanceException -> Controller trả status code của lỗi")]
    public async Task UTCID02_MaintenanceException_ReturnsItsStatusCode()
    {
        var query = new GetMaintenanceSchedulesQuery();
        var sender = new Mock<ISender>(MockBehavior.Strict);
        sender.Setup(s => s.Send(query, CancellationToken.None))
            .ThrowsAsync(new MaintenanceException(400, "Thông tin phân trang không hợp lệ."));

        var result = await Controller(sender).List(query, CancellationToken.None);

        var response = Assert.IsType<ObjectResult>(result);
        Assert.Equal(400, response.StatusCode);
        sender.Verify(s => s.Send(query, CancellationToken.None), Times.Once);
        sender.VerifyNoOtherCalls();
    }

    [Fact(DisplayName = "UTCID03 (A) - PageIndex bằng 0 -> GetListAsync ném MaintenanceException 400")]
    public async Task UTCID03_PageIndexBelowMinimum_IsRejected()
    {
        await AssertInvalidQuery(new MaintenanceListQuery { PageIndex = 0, PageSize = 10 },
            "Thông tin phân trang không hợp lệ.");
    }

    [Fact(DisplayName = "UTCID04 (A) - PageSize bằng 101 -> GetListAsync ném MaintenanceException 400")]
    public async Task UTCID04_PageSizeAboveMaximum_IsRejected()
    {
        await AssertInvalidQuery(new MaintenanceListQuery { PageIndex = 1, PageSize = 101 },
            "Thông tin phân trang không hợp lệ.");
    }

    [Fact(DisplayName = "UTCID05 (A) - PageIndex vượt giới hạn tính offset -> GetListAsync ném MaintenanceException 400")]
    public async Task UTCID05_PageIndexAboveSafeOffset_IsRejected()
    {
        await AssertInvalidQuery(new MaintenanceListQuery { PageIndex = int.MaxValue / 100 + 1, PageSize = 100 },
            "Thông tin phân trang không hợp lệ.");
    }

    [Fact(DisplayName = "UTCID06 (A) - SearchTerm dài 256 ký tự -> GetListAsync ném MaintenanceException 400")]
    public async Task UTCID06_SearchTermAboveMaximum_IsRejected()
    {
        await AssertInvalidQuery(new MaintenanceListQuery { SearchTerm = new string('A', 256) },
            "Từ khóa tìm kiếm tối đa 255 ký tự.");
    }

    [Fact(DisplayName = "UTCID07 (A) - Status ngoài danh sách cho phép -> GetListAsync ném MaintenanceException 400")]
    public async Task UTCID07_UnsupportedStatus_IsRejected()
    {
        await AssertInvalidQuery(new MaintenanceListQuery { Status = "Unknown" },
            "Trạng thái lọc không hợp lệ.");
    }

    private static MaintenanceController Controller(Mock<ISender> sender) =>
        new(sender.Object, NullLogger<MaintenanceController>.Instance);

    private static async Task AssertInvalidQuery(MaintenanceListQuery query, string expectedMessage)
    {
        using var db = new SmartPoolDbContext();
        var operations = new MaintenanceOperations(db, TimeProvider.System, new MaintenanceRequestValidator());

        var exception = await Assert.ThrowsAsync<MaintenanceException>(() =>
            operations.GetListAsync(query, CancellationToken.None));

        Assert.Equal(400, exception.StatusCode);
        Assert.Equal(expectedMessage, exception.Message);
    }
}
