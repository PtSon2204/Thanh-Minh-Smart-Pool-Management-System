using AutoMapper;
using Moq;
using SmartPool.Application.Common.Models;
using SmartPool.Application.Features.ManageTickets.TicketType.Queries.GetAllTicketTypes;
using SmartPool.Application.Interfaces.Repositories;
using SmartPool.Domain.Enums;
using SmartPool.Infrastructure.Persistence.DbContext;
using SmartPool.Infrastructure.Persistence.Repositories.Tickets;
using TicketTypeEntity = SmartPool.Domain.Entities.TicketType;

namespace SmartPool.UnitTests.Features.ManageTickets.TicketType.Queries;

public class GetAllTicketTypesTests
{
    private readonly TicketTypeEntity[] _data =
    [
        TicketTypeTestSupport.Entity("Vé tháng người lớn", day: 8),
        TicketTypeTestSupport.Entity("Vé tháng trẻ em", active: false, day: 7),
        TicketTypeTestSupport.Entity("Vé tháng ưu đãi", active: null, deleted: null, day: 6),
        TicketTypeTestSupport.Entity("Vé lượt người lớn", "VE_LUOT", day: 5),
        TicketTypeTestSupport.Entity("Vé lượt trẻ em", "VE_LUOT", active: false, day: 4),
        TicketTypeTestSupport.Entity("Vé đã xóa", deleted: true, day: 10),
        TicketTypeTestSupport.Entity("Vé không hỗ trợ", "VE_NAM", day: 9)
    ];

    // Run the real operations and handler; only the database surface is substituted.
    private async Task<PagedResponse<GetAllTicketTypesResponse>> Execute(
        GetAllTicketTypesQuery? query = null, TicketTypeEntity[]? data = null)
    {
        var contextMock = new Mock<SmartPoolDbContext>();
        contextMock.SetupGet(c => c.TicketTypes).Returns(TicketTypeTestSupport.DbSet(data ?? _data));
        using var context = contextMock.Object;
        var handler = new GetAllTicketTypesHandler(new TicketTypeOperations(context), TicketTypeTestSupport.Mapper());
        return await handler.Handle(query ?? new(), CancellationToken.None);
    }

    private void AssertItems(PagedResponse<GetAllTicketTypesResponse> result, params int[] indices) =>
        Assert.Equal(indices.Select(i => _data[i].Id), result.Items.Select(i => i.Id));

    [Fact(DisplayName = "UTCID01 (N) - Không có bộ lọc -> Trả vé hợp lệ theo ngày tạo giảm dần và phân trang mặc định")]
    public async Task Handle_DefaultQuery_ReturnsVisibleTicketsNewestFirst()
    {
        // Reverse input so passing order assertions requires production OrderByDescending.
        var result = await Execute(data: _data.Reverse().ToArray());
        AssertItems(result, 0, 1, 2, 3, 4);
        Assert.Equal(5, result.TotalCount);
        Assert.Equal(1, result.PageIndex);
        Assert.Equal(10, result.PageSize);
        Assert.Equal(1, result.TotalPages);
        var item = result.Items[0];
        Assert.Equal(_data[0].Name, item.Name);
        Assert.Equal(TicketCategoryEnum.VE_THANG, item.TicketCategory);
        Assert.Equal(_data[0].Price, item.Price);
        Assert.Equal(_data[0].DurationDays, item.DurationDays);
        Assert.Equal(_data[0].CreatedAt, item.CreatedAt);
        Assert.True(item.IsActive);
    }

    [Fact(DisplayName = "UTCID02 (N) - Tìm kiếm một phần tên không phân biệt hoa thường -> Trả đúng vé khớp")]
    public async Task Handle_Search_ReturnsMatchingNames()
    {
        var result = await Execute(new() { SearchTerm = "THÁNG" });
        AssertItems(result, 0, 1, 2);
        Assert.Equal(3, result.TotalCount);
    }

    [Fact(DisplayName = "UTCID03 (B) - Tìm kiếm chuỗi rỗng -> Bỏ qua tìm kiếm")]
    public async Task Handle_EmptySearch_ReturnsAllVisibleTickets()
    {
        var result = await Execute(new() { SearchTerm = "" });
        AssertItems(result, 0, 1, 2, 3, 4);
        Assert.Equal(5, result.TotalCount);
    }

    [Fact(DisplayName = "UTCID04 (B) - Tìm kiếm chỉ có khoảng trắng -> Bỏ qua tìm kiếm")]
    public async Task Handle_WhitespaceSearch_ReturnsAllVisibleTickets()
    {
        var result = await Execute(new() { SearchTerm = "   " });
        AssertItems(result, 0, 1, 2, 3, 4);
    }

    [Fact(DisplayName = "UTCID05 (N) - Tìm kiếm không khớp -> Danh sách rỗng và tổng bằng 0")]
    public async Task Handle_NoSearchMatch_ReturnsEmptyPage()
    {
        var result = await Execute(new() { SearchTerm = "không tìm thấy" });
        Assert.Empty(result.Items);
        Assert.Equal(0, result.TotalCount);
        Assert.Equal(0, result.TotalPages);
    }

    [Fact(DisplayName = "UTCID06 (N) - Lọc VE_THANG -> Chỉ trả vé tháng")]
    public async Task Handle_MonthlyFilter_ReturnsMonthlyTickets()
    {
        var result = await Execute(new() { Category = TicketCategoryEnum.VE_THANG });
        AssertItems(result, 0, 1, 2);
        Assert.Equal(3, result.TotalCount);
        Assert.All(result.Items, item => Assert.Equal(TicketCategoryEnum.VE_THANG, item.TicketCategory));
    }

    [Fact(DisplayName = "UTCID07 (N) - Lọc VE_LUOT -> Chỉ trả vé lượt")]
    public async Task Handle_SingleUseFilter_ReturnsSingleUseTickets()
    {
        var result = await Execute(new() { Category = TicketCategoryEnum.VE_LUOT });
        AssertItems(result, 3, 4);
        Assert.Equal(2, result.TotalCount);
    }

    [Fact(DisplayName = "UTCID08 (N) - Lọc hoạt động -> Bao gồm IsActive true và null")]
    public async Task Handle_ActiveFilter_IncludesNullAsActive()
    {
        var result = await Execute(new() { IsActive = true });
        AssertItems(result, 0, 2, 3);
        Assert.Equal(3, result.TotalCount);
        Assert.All(result.Items, item => Assert.True(item.IsActive));
    }

    [Fact(DisplayName = "UTCID09 (N) - Lọc không hoạt động -> Chỉ trả IsActive false")]
    public async Task Handle_InactiveFilter_ReturnsOnlyFalse()
    {
        var result = await Execute(new() { IsActive = false });
        AssertItems(result, 1, 4);
        Assert.Equal(2, result.TotalCount);
        Assert.All(result.Items, item => Assert.False(item.IsActive));
    }

    [Fact(DisplayName = "UTCID10 (N) - Kết hợp search và các bộ lọc -> Áp dụng đồng thời tất cả điều kiện")]
    public async Task Handle_CombinedFilters_ReturnsIntersection()
    {
        var result = await Execute(new()
        {
            SearchTerm = "NGƯỜI LỚN", Category = TicketCategoryEnum.VE_LUOT, IsActive = true,
            PageIndex = 1, PageSize = 1
        });
        AssertItems(result, 3);
        Assert.Equal(1, result.TotalCount);
        Assert.Equal(1, result.TotalPages);
    }

    [Fact(DisplayName = "UTCID11 (B) - Trang đầu kích thước 2 -> Trả 2 vé đầu và tổng 5 vé")]
    public async Task Handle_FirstPage_ReturnsFirstTwo()
    {
        var result = await Execute(new() { PageIndex = 1, PageSize = 2 });
        AssertItems(result, 0, 1);
        Assert.Equal(5, result.TotalCount);
        Assert.Equal(3, result.TotalPages);
        Assert.Equal(1, result.PageIndex);
        Assert.Equal(2, result.PageSize);
    }

    [Fact(DisplayName = "UTCID12 (N) - Trang thứ 2 kích thước 2 -> Bỏ qua đúng 2 vé đầu")]
    public async Task Handle_SecondPage_ReturnsNextTwo()
    {
        var result = await Execute(new() { PageIndex = 2, PageSize = 2 });
        AssertItems(result, 2, 3);
        Assert.Equal(5, result.TotalCount);
        Assert.Equal(3, result.TotalPages);
        Assert.Equal(2, result.PageIndex);
        Assert.Equal(2, result.PageSize);
    }

    [Fact(DisplayName = "UTCID13 (B) - Trang cuối không đầy -> Trả vé còn lại và giữ tổng số vé")]
    public async Task Handle_LastPage_ReturnsRemainder()
    {
        var result = await Execute(new() { PageIndex = 3, PageSize = 2 });
        AssertItems(result, 4);
        Assert.Equal(5, result.TotalCount);
        Assert.Equal(3, result.TotalPages);
    }

    [Fact(DisplayName = "UTCID14 (B) - Trang vượt tổng số trang -> Danh sách rỗng và giữ tổng số vé")]
    public async Task Handle_PageBeyondEnd_ReturnsEmptyItemsWithTotal()
    {
        var result = await Execute(new() { PageIndex = 4, PageSize = 2 });
        Assert.Empty(result.Items);
        Assert.Equal(5, result.TotalCount);
        Assert.Equal(3, result.TotalPages);
        Assert.Equal(4, result.PageIndex);
    }

    [Fact(DisplayName = "UTCID15 (B) - Lọc trước phân trang -> Tổng và số trang tính theo dữ liệu đã lọc")]
    public async Task Handle_FilteredPagination_CountsFilteredTickets()
    {
        var result = await Execute(new() { Category = TicketCategoryEnum.VE_THANG, PageIndex = 2, PageSize = 2 });
        AssertItems(result, 2);
        Assert.Equal(3, result.TotalCount);
        Assert.Equal(2, result.TotalPages);
    }

    [Fact(DisplayName = "UTCID16 (B) - Không có dữ liệu -> Danh sách rỗng và tổng số trang bằng 0")]
    public async Task Handle_EmptyDatabase_ReturnsEmptyPage()
    {
        var result = await Execute(data: []);
        Assert.Empty(result.Items);
        Assert.Equal(0, result.TotalCount);
        Assert.Equal(0, result.TotalPages);
        Assert.Equal(1, result.PageIndex);
        Assert.Equal(10, result.PageSize);
    }

    [Fact(DisplayName = "UTCID17 (N) - Số vé chia hết kích thước trang -> Không tạo thêm trang thừa")]
    public async Task Handle_ExactPageSize_ReturnsExactPageCount()
    {
        var result = await Execute(new() { Category = TicketCategoryEnum.VE_LUOT, PageSize = 1, PageIndex = 2 });
        AssertItems(result, 4);
        Assert.Equal(2, result.TotalCount);
        Assert.Equal(2, result.TotalPages);
    }

    [Fact(DisplayName = "UTCID18 (N) - Tìm đúng tên vé đã xóa -> Không trả vé đã xóa")]
    public async Task Handle_SearchDeletedTicket_ReturnsNothing()
    {
        var result = await Execute(new() { SearchTerm = "Vé đã xóa" });
        Assert.Empty(result.Items);
        Assert.Equal(0, result.TotalCount);
    }

    [Fact(DisplayName = "UTCID19 (A) - Phân loại không hỗ trợ -> Danh sách rỗng")]
    public async Task Handle_UnsupportedCategory_ReturnsNothing()
    {
        var result = await Execute(new() { Category = (TicketCategoryEnum)2 });
        Assert.Empty(result.Items);
        Assert.Equal(0, result.TotalCount);
    }

    [Fact(DisplayName = "UTCID20 (N) - Handler nhận search filter pagination -> Truyền đúng tham số và CancellationToken")]
    public async Task Handle_ForwardsExactArguments()
    {
        var operations = new Mock<ITicketTypeOperations>(MockBehavior.Strict);
        using var cts = new CancellationTokenSource();
        var items = new List<TicketTypeEntity> { _data[4] };
        operations.Setup(o => o.GetPagedTicketTypesAsync("trẻ em", "VE_LUOT", false, 2, 3, cts.Token))
            .ReturnsAsync((items, 7));
        var result = await new GetAllTicketTypesHandler(operations.Object, TicketTypeTestSupport.Mapper()).Handle(new()
        {
            SearchTerm = "trẻ em", Category = TicketCategoryEnum.VE_LUOT, IsActive = false, PageIndex = 2, PageSize = 3
        }, cts.Token);
        AssertItems(result, 4);
        Assert.Equal(7, result.TotalCount);
        Assert.Equal(2, result.PageIndex);
        Assert.Equal(3, result.PageSize);
        Assert.Equal(3, result.TotalPages);
        operations.Verify(o => o.GetPagedTicketTypesAsync("trẻ em", "VE_LUOT", false, 2, 3, cts.Token), Times.Once);
    }

    [Fact(DisplayName = "UTCID21 (A) - Operations gặp lỗi -> Truyền lỗi và không gọi mapper")]
    public async Task Handle_OperationsFailure_PropagatesException()
    {
        var operations = new Mock<ITicketTypeOperations>();
        var failure = new InvalidOperationException("Query failed");
        operations.Setup(o => o.GetPagedTicketTypesAsync(null, null, null, 1, 10, It.IsAny<CancellationToken>()))
            .ThrowsAsync(failure);
        var mapper = new Mock<IMapper>(MockBehavior.Strict);
        Assert.Same(failure, await Assert.ThrowsAsync<InvalidOperationException>(() =>
            new GetAllTicketTypesHandler(operations.Object, mapper.Object).Handle(new(), CancellationToken.None)));
        mapper.Verify(m => m.Map<List<GetAllTicketTypesResponse>>(It.IsAny<object>()), Times.Never);
    }

    [Fact(DisplayName = "UTCID22 (A) - Truy vấn bị hủy -> Truyền OperationCanceledException")]
    public async Task Handle_CancelledQuery_PropagatesCancellation()
    {
        using var cts = new CancellationTokenSource();
        cts.Cancel();
        var operations = new Mock<ITicketTypeOperations>();
        operations.Setup(o => o.GetPagedTicketTypesAsync(null, null, null, 1, 10, cts.Token))
            .Returns(Task.FromCanceled<(List<TicketTypeEntity>, int)>(cts.Token));
        var mapper = new Mock<IMapper>(MockBehavior.Strict);
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            new GetAllTicketTypesHandler(operations.Object, mapper.Object).Handle(new(), cts.Token));
        mapper.Verify(m => m.Map<List<GetAllTicketTypesResponse>>(It.IsAny<object>()), Times.Never);
    }
}
