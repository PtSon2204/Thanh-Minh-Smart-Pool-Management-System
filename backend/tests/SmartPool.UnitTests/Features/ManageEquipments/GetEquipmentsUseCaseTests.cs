using FluentValidation;
using Moq;
using SmartPool.Application.Common.Models;
using SmartPool.Application.Features.ManageEquipments.Cameras;
using SmartPool.Application.Features.ManageEquipments.DTOs;
using SmartPool.Application.Features.ManageEquipments.Queries.GetEquipments;
using SmartPool.Application.Interfaces.Repositories;
using SmartPool.Application.Interfaces.Services;

namespace SmartPool.UnitTests.Features.ManageEquipments;

public sealed class GetEquipmentsUseCaseTests
{
    [Fact(DisplayName = "UTCID01 (N) - Search/filter/pagination hợp lệ -> Gửi nguyên query, Vietnam date và trả page")]
    public async Task UTCID01_ValidCombinedQuery_DelegatesAndReturnsPage()
    {
        var query = new GetEquipmentsQuery
        {
            SearchTerm = "pump", Category = "Pump", Location = "Pool", Status = "Active", PageIndex = 2, PageSize = 20
        };
        var expected = new PagedResponse<EquipmentListItem>
        {
            Items = [Item("Pump 2")], TotalCount = 21, PageIndex = 2, PageSize = 20
        };
        var repo = new Mock<IEquipmentOperations>(MockBehavior.Strict);
        using var cts = new CancellationTokenSource();
        repo.Setup(r => r.GetListAsync(query, new DateOnly(2026, 10, 28), cts.Token)).ReturnsAsync(expected);

        var result = await Handler(repo).Handle(query, cts.Token);

        Assert.Same(expected, result);
        Assert.Equal(21, result.TotalCount);
        Assert.Equal(2, result.PageIndex);
        Assert.Equal(20, result.PageSize);
        Assert.Equal("Pump 2", Assert.Single(result.Items).Name);
        repo.Verify(r => r.GetListAsync(query, new DateOnly(2026, 10, 28), cts.Token), Times.Once);
        repo.VerifyNoOtherCalls();
    }

    [Fact(DisplayName = "UTCID02 (N) - Không có thiết bị phù hợp -> Trả page rỗng từ repository")]
    public async Task UTCID02_NoMatchingEquipment_ReturnsEmptyPage()
    {
        var query = new GetEquipmentsQuery();
        var expected = new PagedResponse<EquipmentListItem> { Items = [], TotalCount = 0, PageIndex = 1, PageSize = 10 };
        var repo = new Mock<IEquipmentOperations>(MockBehavior.Strict);
        repo.Setup(r => r.GetListAsync(query, It.IsAny<DateOnly>(), CancellationToken.None)).ReturnsAsync(expected);

        var result = await Handler(repo).Handle(query, CancellationToken.None);

        Assert.Same(expected, result);
        Assert.Empty(result.Items);
        Assert.Equal(0, result.TotalCount);
        repo.Verify(r => r.GetListAsync(query, It.IsAny<DateOnly>(), CancellationToken.None), Times.Once);
        repo.VerifyNoOtherCalls();
    }

    [Fact(DisplayName = "UTCID03 (A) - PageIndex bằng 0 -> ValidationException, không gọi repository")]
    public async Task UTCID03_PageIndexZero_IsRejectedBeforeRepository()
    {
        var repo = new Mock<IEquipmentOperations>(MockBehavior.Strict);
        var query = new GetEquipmentsQuery { PageIndex = 0 };

        await Assert.ThrowsAsync<ValidationException>(() => Handler(repo).Handle(query, CancellationToken.None));

        repo.VerifyNoOtherCalls();
    }

    [Fact(DisplayName = "UTCID04 (A) - PageSize bằng 101 -> ValidationException, không gọi repository")]
    public async Task UTCID04_PageSizeAboveMaximum_IsRejectedBeforeRepository()
    {
        var repo = new Mock<IEquipmentOperations>(MockBehavior.Strict);
        var query = new GetEquipmentsQuery { PageSize = 101 };

        await Assert.ThrowsAsync<ValidationException>(() => Handler(repo).Handle(query, CancellationToken.None));

        repo.VerifyNoOtherCalls();
    }

    [Fact(DisplayName = "UTCID05 (A) - PageIndex vượt giới hạn an toàn -> ValidationException")]
    public async Task UTCID05_PageIndexAboveSafeMaximum_IsRejectedBeforeRepository()
    {
        var repo = new Mock<IEquipmentOperations>(MockBehavior.Strict);
        var query = new GetEquipmentsQuery { PageIndex = int.MaxValue / 100 + 1, PageSize = 100 };

        await Assert.ThrowsAsync<ValidationException>(() => Handler(repo).Handle(query, CancellationToken.None));

        repo.VerifyNoOtherCalls();
    }

    [Fact(DisplayName = "UTCID06 (B) - PageIndex và PageSize ở giới hạn tối đa -> Validator chấp nhận và handler gọi repository")]
    public async Task UTCID06_PaginationAtMaximum_IsAccepted()
    {
        var query = new GetEquipmentsQuery { PageIndex = int.MaxValue / 100, PageSize = 100 };
        var expected = new PagedResponse<EquipmentListItem> { Items = [], PageIndex = query.PageIndex, PageSize = query.PageSize };
        var repo = new Mock<IEquipmentOperations>(MockBehavior.Strict);
        repo.Setup(r => r.GetListAsync(query, It.IsAny<DateOnly>(), CancellationToken.None)).ReturnsAsync(expected);

        var result = await Handler(repo).Handle(query, CancellationToken.None);

        Assert.Same(expected, result);
        repo.Verify(r => r.GetListAsync(query, It.IsAny<DateOnly>(), CancellationToken.None), Times.Once);
        repo.VerifyNoOtherCalls();
    }

    [Fact(DisplayName = "UTCID07 (A) - SearchTerm/category/location/status vượt độ dài tối đa -> ValidationException")]
    public async Task UTCID07_OversizedSearchAndFilters_AreRejected()
    {
        var query = new GetEquipmentsQuery
        {
            SearchTerm = new string('S', 256), Category = new string('C', 51),
            Location = new string('L', 256), Status = new string('T', 51)
        };
        var repo = new Mock<IEquipmentOperations>(MockBehavior.Strict);

        var error = await Assert.ThrowsAsync<ValidationException>(() => Handler(repo).Handle(query, CancellationToken.None));

        Assert.Equal(4, error.Errors.Count());
        Assert.Contains(error.Errors, item => item.PropertyName == nameof(GetEquipmentsQuery.SearchTerm));
        Assert.Contains(error.Errors, item => item.PropertyName == nameof(GetEquipmentsQuery.Category));
        Assert.Contains(error.Errors, item => item.PropertyName == nameof(GetEquipmentsQuery.Location));
        Assert.Contains(error.Errors, item => item.PropertyName == nameof(GetEquipmentsQuery.Status));
        repo.VerifyNoOtherCalls();
    }

    [Fact(DisplayName = "UTCID08 (N) - Metadata camera Browser -> Chỉ trả specifications, source Browser, không gọi camera service")]
    public async Task UTCID08_BrowserCamera_HidesEnvelopeAndDoesNotRequestCameraStatus()
    {
        var query = new GetEquipmentsQuery();
        var item = Item("Camera 1", "Camera",
            "{\"_camera\":{\"version\":1,\"source\":\"Browser\",\"protectedConfiguration\":\"secret\"},\"specifications\":{\"resolution\":\"1080p\"}}");
        var repo = new Mock<IEquipmentOperations>(MockBehavior.Strict);
        var cameras = new Mock<ICameraService>(MockBehavior.Strict);
        repo.Setup(r => r.GetListAsync(query, It.IsAny<DateOnly>(), CancellationToken.None))
            .ReturnsAsync(new PagedResponse<EquipmentListItem> { Items = [item] });

        var result = await Handler(repo, cameras).Handle(query, CancellationToken.None);

        var actual = Assert.Single(result.Items);
        Assert.Equal("{\"resolution\":\"1080p\"}", actual.Metadata);
        Assert.Equal("Browser", actual.CameraSource);
        Assert.Null(actual.CameraConnection);
        cameras.VerifyNoOtherCalls();
    }

    [Fact(DisplayName = "UTCID09 (N) - Metadata camera RTSP -> Ẩn cấu hình bảo vệ, trả trạng thái camera")]
    public async Task UTCID09_RtspCamera_HidesEnvelopeAndAddsConnectionStatus()
    {
        var query = new GetEquipmentsQuery();
        var id = Guid.NewGuid();
        var item = Item("Camera 2", "Camera",
            "{\"_camera\":{\"version\":1,\"identity\":\"cam-id\",\"protectedConfiguration\":\"secret\"},\"specifications\":{\"resolution\":\"4K\"}}");
        item.Id = id;
        var status = new CameraConnectionStatus("Connected", DateTimeOffset.Parse("2026-10-28T10:00:00Z"));
        var repo = new Mock<IEquipmentOperations>(MockBehavior.Strict);
        var cameras = new Mock<ICameraService>(MockBehavior.Strict);
        repo.Setup(r => r.GetListAsync(query, It.IsAny<DateOnly>(), CancellationToken.None))
            .ReturnsAsync(new PagedResponse<EquipmentListItem> { Items = [item] });
        cameras.Setup(c => c.GetStatus(id)).Returns(status);

        var result = await Handler(repo, cameras).Handle(query, CancellationToken.None);

        var actual = Assert.Single(result.Items);
        Assert.Equal("{\"resolution\":\"4K\"}", actual.Metadata);
        Assert.Equal("RTSP", actual.CameraSource);
        Assert.Same(status, actual.CameraConnection);
        cameras.Verify(c => c.GetStatus(id), Times.Once);
        cameras.VerifyNoOtherCalls();
    }

    [Fact(DisplayName = "UTCID10 (A) - Repository ném exception -> Handler truyền tiếp exception")]
    public async Task UTCID10_RepositoryFailure_IsPropagated()
    {
        var query = new GetEquipmentsQuery();
        var repo = new Mock<IEquipmentOperations>(MockBehavior.Strict);
        var failure = new InvalidOperationException("Equipment list read failed");
        repo.Setup(r => r.GetListAsync(query, It.IsAny<DateOnly>(), CancellationToken.None)).ThrowsAsync(failure);

        var actual = await Assert.ThrowsAsync<InvalidOperationException>(() => Handler(repo).Handle(query, CancellationToken.None));

        Assert.Same(failure, actual);
        repo.Verify(r => r.GetListAsync(query, It.IsAny<DateOnly>(), CancellationToken.None), Times.Once);
        repo.VerifyNoOtherCalls();
    }

    private static GetEquipmentsHandler Handler(Mock<IEquipmentOperations> repository, Mock<ICameraService>? cameras = null) =>
        new(repository.Object, new GetEquipmentsValidator(), new FixedTime(), cameras?.Object);

    private static EquipmentListItem Item(string name, string category = "Pump", string? metadata = null) => new()
    {
        Id = Guid.NewGuid(), Name = name, Category = category, Location = "Pool", Metadata = metadata, Status = "Active"
    };

    private sealed class FixedTime : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => new(2026, 10, 27, 18, 0, 0, TimeSpan.Zero);
    }
}
