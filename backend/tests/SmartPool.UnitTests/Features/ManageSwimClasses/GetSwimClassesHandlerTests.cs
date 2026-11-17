using FluentValidation;
using Moq;
using SmartPool.Application.Common.Models;
using SmartPool.Application.Features.ManageSwimClasses.Queries.GetSwimClasses;
using SmartPool.Application.Interfaces.Repositories;
using SmartPool.Application.Interfaces.Services;

namespace SmartPool.UnitTests.Features.ManageSwimClasses;

public sealed class GetSwimClassesHandlerTests
{
    [Fact(DisplayName = "UTCID01 (N) - Customer xem danh sách → chuyển role CUSTOMER và trả trang dữ liệu")]
    public async Task Handle_Customer_ReturnsRepositoryPage()
    {
        var h = new Harness();
        var query = new GetSwimClassesQuery();
        var page = new PagedResponse<SwimClassListItem> { PageIndex = 1, PageSize = 10, TotalCount = 1 };
        page.Items.Add(Item());
        h.Operations.Setup(x => x.GetListAsync(query, h.UserId, "CUSTOMER", h.Token)).ReturnsAsync(page);

        var result = await h.Handle(query);

        Assert.Same(page, result);
        Assert.Equal(1, result.TotalCount);
        Assert.Single(result.Items);
        h.Operations.Verify(x => x.GetListAsync(query, h.UserId, "CUSTOMER", h.Token), Times.Once);
    }

    [Fact(DisplayName = "UTCID02 (N) - Coach xem danh sách → dùng role COACH")]
    public async Task Handle_Coach_UsesCoachScope()
    {
        var h = new Harness(role: "COACH");
        var query = new GetSwimClassesQuery();
        h.Operations.Setup(x => x.GetListAsync(query, h.UserId, "COACH", h.Token)).ReturnsAsync(new PagedResponse<SwimClassListItem>());

        await h.Handle(query);

        h.Operations.Verify(x => x.GetListAsync(query, h.UserId, "COACH", h.Token), Times.Once);
    }

    [Fact(DisplayName = "UTCID03 (N) - User có quyền Coach chọn scope coach → xem theo COACH")]
    public async Task Handle_DualRoleWithCoachScope_UsesCoachScope()
    {
        var h = new Harness();
        h.CurrentUser.Setup(x => x.IsInRole("COACH")).Returns(true);
        var query = new GetSwimClassesQuery { Scope = " coach " };
        h.Operations.Setup(x => x.GetListAsync(query, h.UserId, "COACH", h.Token)).ReturnsAsync(new PagedResponse<SwimClassListItem>());

        await h.Handle(query);

        h.Operations.Verify(x => x.GetListAsync(query, h.UserId, "COACH", h.Token), Times.Once);
    }

    [Fact(DisplayName = "UTCID04 (A) - Chọn scope không có quyền → từ chối, không đọc DB")]
    public async Task Handle_UnauthorizedScope_ThrowsBeforeRepository()
    {
        var h = new Harness();

        var error = await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            h.Handle(new GetSwimClassesQuery { Scope = "COACH" }));

        Assert.Equal("Không có quyền xem phạm vi lớp được yêu cầu.", error.Message);
        h.Operations.VerifyNoOtherCalls();
    }

    [Fact(DisplayName = "UTCID05 (A) - Thiếu user ID → từ chối, không đọc DB")]
    public async Task Handle_MissingUserId_ThrowsBeforeRepository()
    {
        var h = new Harness();
        h.CurrentUser.SetupGet(x => x.UserId).Returns((Guid?)null);

        var error = await Assert.ThrowsAsync<UnauthorizedAccessException>(() => h.Handle());

        Assert.Equal("Không có quyền xem danh sách lớp dạy bơi.", error.Message);
        h.Operations.VerifyNoOtherCalls();
    }

    [Fact(DisplayName = "UTCID06 (A) - Role không phải Customer/Coach → từ chối")]
    public async Task Handle_UnsupportedRole_ThrowsBeforeRepository()
    {
        var h = new Harness(role: "ADMIN");

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => h.Handle());

        h.Operations.VerifyNoOtherCalls();
    }

    [Fact(DisplayName = "UTCID07 (A) - Phân trang sai → ValidationException, không đọc DB")]
    public async Task Handle_InvalidPagination_ThrowsValidationException()
    {
        var h = new Harness();

        var error = await Assert.ThrowsAsync<ValidationException>(() =>
            h.Handle(new GetSwimClassesQuery { PageIndex = 0, PageSize = 101 }));

        Assert.Contains(error.Errors, x => x.PropertyName == "PageIndex");
        Assert.Contains(error.Errors, x => x.PropertyName == "PageSize");
        h.Operations.VerifyNoOtherCalls();
    }

    [Fact(DisplayName = "UTCID08 (N) - Search/status/registrationOpenNow → chuyển nguyên query và token")]
    public async Task Handle_Filters_AreForwarded()
    {
        var h = new Harness();
        var query = new GetSwimClassesQuery
        {
            SearchTerm = " bơi ", Status = "Active", RegistrationOpenNow = true,
            PageIndex = 2, PageSize = 5
        };
        h.Operations.Setup(x => x.GetListAsync(query, h.UserId, "CUSTOMER", h.Token)).ReturnsAsync(new PagedResponse<SwimClassListItem>());

        await h.Handle(query);

        h.Operations.Verify(x => x.GetListAsync(It.Is<GetSwimClassesQuery>(q =>
            ReferenceEquals(q, query) && q.SearchTerm == " bơi " && q.Status == "Active" &&
            q.RegistrationOpenNow && q.PageIndex == 2 && q.PageSize == 5), h.UserId, "CUSTOMER", h.Token), Times.Once);
    }

    [Fact(DisplayName = "UTCID09 (B) - Repository trả trang rỗng → giữ nguyên tổng và phân trang")]
    public async Task Handle_EmptyPage_ReturnsUnchanged()
    {
        var h = new Harness();
        var query = new GetSwimClassesQuery { PageIndex = 3, PageSize = 2 };
        var page = new PagedResponse<SwimClassListItem> { TotalCount = 4, PageIndex = 3, PageSize = 2 };
        h.Operations.Setup(x => x.GetListAsync(query, h.UserId, "CUSTOMER", h.Token)).ReturnsAsync(page);

        var result = await h.Handle(query);

        Assert.Same(page, result);
        Assert.Empty(result.Items);
        Assert.Equal(4, result.TotalCount);
        Assert.Equal(2, result.TotalPages);
    }

    [Fact(DisplayName = "UTCID10 (A) - Repository lỗi → exception được truyền tiếp")]
    public async Task Handle_RepositoryThrows_PropagatesException()
    {
        var h = new Harness();
        var failure = new InvalidOperationException("DB unavailable");
        h.Operations.Setup(x => x.GetListAsync(It.IsAny<GetSwimClassesQuery>(), h.UserId, "CUSTOMER", h.Token)).ThrowsAsync(failure);

        Assert.Same(failure, await Assert.ThrowsAsync<InvalidOperationException>(() => h.Handle()));
    }

    [Fact(DisplayName = "UTCID11 (B) - Scope chỉ có khoảng trắng → dùng role hiện tại")]
    public async Task Handle_BlankScope_UsesCurrentRole()
    {
        var h = new Harness();
        var query = new GetSwimClassesQuery { Scope = "   " };
        h.Operations.Setup(x => x.GetListAsync(query, h.UserId, "CUSTOMER", h.Token)).ReturnsAsync(new PagedResponse<SwimClassListItem>());

        await h.Handle(query);

        h.Operations.Verify(x => x.GetListAsync(query, h.UserId, "CUSTOMER", h.Token), Times.Once);
    }

    private static SwimClassListItem Item() => new(Guid.NewGuid(), "Lớp bơi", null, 5, 100000m,
        Guid.NewGuid(), "Coach", DateTime.UtcNow, DateTime.UtcNow.AddDays(1), "Active", 2, 1,
        DateTime.UtcNow, DateTime.UtcNow);

    private sealed class Harness
    {
        public readonly Guid UserId = Guid.NewGuid();
        public readonly CancellationToken Token = new CancellationTokenSource().Token;
        public readonly Mock<ISwimClassOperations> Operations = new();
        public readonly Mock<ICurrentUser> CurrentUser = new();
        private readonly GetSwimClassesHandler _handler;

        public Harness(string role = "CUSTOMER")
        {
            CurrentUser.SetupGet(x => x.UserId).Returns(UserId);
            CurrentUser.SetupGet(x => x.Role).Returns(role);
            _handler = new GetSwimClassesHandler(Operations.Object, CurrentUser.Object, new GetSwimClassesValidator());
        }

        public Task<PagedResponse<SwimClassListItem>> Handle(GetSwimClassesQuery? query = null) =>
            _handler.Handle(query ?? new GetSwimClassesQuery(), Token);
    }
}
