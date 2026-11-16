using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Moq;
using SmartPool.Application.Common.Models;
using SmartPool.Application.Features.ManageUsers.Queries.GetUsers;
using SmartPool.Application.Interfaces.Repositories;

namespace SmartPool.UnitTests.Features.ManageUsers;

public sealed class GetUsersHandlerTests
{
    [Fact(DisplayName = "UTCID01 (N) - Có người dùng → 200, trả đúng dữ liệu và phân trang")]
    public async Task Handle_UsersExist_ReturnsPage()
    {
        var h = new Harness();
        var item = new GetUsersResponse { Id = Guid.NewGuid(), Username = "customer", Role = "CUSTOMER" };
        var page = new PagedResponse<GetUsersResponse>
        {
            Items = [item], TotalCount = 21, PageIndex = 2, PageSize = 10
        };
        var query = new GetUsersQuery { PageIndex = 2, PageSize = 10 };
        h.Users.Setup(x => x.GetUsersAsync(query, h.Token)).ReturnsAsync(page);

        var result = await h.Handler.Handle(query, h.Token);

        var actual = Assert.IsType<PagedResponse<GetUsersResponse>>(Assert.IsType<OkObjectResult>(result).Value);
        Assert.Same(page, actual);
        Assert.Same(item, Assert.Single(actual.Items));
        Assert.Equal(21, actual.TotalCount);
        Assert.Equal(2, actual.PageIndex);
        Assert.Equal(10, actual.PageSize);
        Assert.Equal(3, actual.TotalPages);
        h.Users.Verify(x => x.GetUsersAsync(query, h.Token), Times.Once);
    }

    [Fact(DisplayName = "UTCID02 (B) - Trang không có kết quả → 200, Items rỗng")]
    public async Task Handle_EmptyPage_ReturnsOkWithEmptyItems()
    {
        var h = new Harness();
        var query = new GetUsersQuery { PageIndex = 3, PageSize = 10 };
        h.Users.Setup(x => x.GetUsersAsync(query, h.Token)).ReturnsAsync(new PagedResponse<GetUsersResponse>
        {
            PageIndex = 3, PageSize = 10, TotalCount = 11
        });

        var result = await h.Handler.Handle(query, h.Token);

        var page = Assert.IsType<PagedResponse<GetUsersResponse>>(Assert.IsType<OkObjectResult>(result).Value);
        Assert.Empty(page.Items);
        Assert.Equal(11, page.TotalCount);
        Assert.Equal(2, page.TotalPages);
    }

    [Fact(DisplayName = "UTCID03 (N) - Có search, role, status → chuyển nguyên query và CancellationToken")]
    public async Task Handle_Filters_ForwardedToRepository()
    {
        var h = new Harness();
        var query = new GetUsersQuery
        {
            PageIndex = 2, PageSize = 5, SearchTerm = " Nguyen ", Role = " coach ", Status = "active"
        };
        h.Users.Setup(x => x.GetUsersAsync(query, h.Token)).ReturnsAsync(new PagedResponse<GetUsersResponse>());

        Assert.IsType<OkObjectResult>(await h.Handler.Handle(query, h.Token));
        h.Users.Verify(x => x.GetUsersAsync(It.Is<GetUsersQuery>(q =>
            ReferenceEquals(q, query) && q.PageIndex == 2 && q.PageSize == 5 &&
            q.SearchTerm == " Nguyen " && q.Role == " coach " && q.Status == "active"), h.Token), Times.Once);
    }

    [Fact(DisplayName = "UTCID04 (A) - PageSize sai → 400, lỗi key pageSize, không truy vấn")]
    public async Task Handle_InvalidPageSize_ReturnsValidationProblem()
    {
        var h = new Harness();

        var result = await h.Handler.Handle(new GetUsersQuery { PageSize = 101 }, h.Token);

        var badRequest = Assert.IsType<BadRequestObjectResult>(result);
        var problem = Assert.IsType<ValidationProblemDetails>(badRequest.Value);
        Assert.Equal(StatusCodes.Status400BadRequest, badRequest.StatusCode);
        Assert.Equal(["Kích thước trang phải từ 1 đến 100."], problem.Errors["pageSize"]);
        h.Users.VerifyNoOtherCalls();
    }

    [Fact(DisplayName = "UTCID05 (A) - Nhiều trường sai → 400, lỗi theo từng key camelCase")]
    public async Task Handle_MultipleInvalidFields_ReturnsGroupedErrors()
    {
        var h = new Harness();
        var query = new GetUsersQuery { PageIndex = 0, PageSize = 0, Role = "UNKNOWN", Status = "INACTIVE" };

        var result = await h.Handler.Handle(query, h.Token);

        var problem = Assert.IsType<ValidationProblemDetails>(Assert.IsType<BadRequestObjectResult>(result).Value);
        Assert.Equal(["pageIndex", "pageSize", "role", "status"], problem.Errors.Keys.OrderBy(x => x));
        Assert.Contains("Chỉ số trang phải lớn hơn 0.", problem.Errors["pageIndex"]);
        Assert.Contains("Kích thước trang phải từ 1 đến 100.", problem.Errors["pageSize"]);
        Assert.Contains("Vai trò phải là CUSTOMER, LIFEGUARD, SALES, COACH hoặc ADMIN.", problem.Errors["role"]);
        Assert.Contains("Trạng thái phải là ACTIVE hoặc LOCKED.", problem.Errors["status"]);
        h.Users.VerifyNoOtherCalls();
    }

    [Fact(DisplayName = "UTCID06 (A) - Repository lỗi → truyền tiếp exception")]
    public async Task Handle_RepositoryThrows_PropagatesException()
    {
        var h = new Harness();
        var query = new GetUsersQuery();
        var failure = new InvalidOperationException("DB unavailable");
        h.Users.Setup(x => x.GetUsersAsync(query, h.Token)).ThrowsAsync(failure);

        Assert.Same(failure, await Assert.ThrowsAsync<InvalidOperationException>(() => h.Handler.Handle(query, h.Token)));
        h.Users.Verify(x => x.GetUsersAsync(query, h.Token), Times.Once);
    }

    private sealed class Harness
    {
        public readonly Mock<IUserOperations> Users = new();
        public readonly CancellationToken Token = new CancellationTokenSource().Token;
        public readonly GetUsersHandler Handler;

        public Harness() => Handler = new GetUsersHandler(Users.Object, new GetUsersValidator());
    }
}
