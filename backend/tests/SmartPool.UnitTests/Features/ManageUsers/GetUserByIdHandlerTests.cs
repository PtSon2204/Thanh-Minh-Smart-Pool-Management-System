using Microsoft.AspNetCore.Mvc;
using Moq;
using SmartPool.Application.Features.ManageUsers.Queries.GetUserById;
using SmartPool.Application.Interfaces.Repositories;

namespace SmartPool.UnitTests.Features.ManageUsers;

public sealed class GetUserByIdHandlerTests
{
    [Fact(DisplayName = "UTCID01 (N) - Tìm thấy user → 200, trả đúng tài khoản, hồ sơ và role")]
    public async Task Handle_UserExists_ReturnsDetail()
    {
        var id = Guid.NewGuid();
        var roleId = Guid.NewGuid();
        var expected = new GetUserByIdResponse
        {
            Id = id, Username = "customer", Email = "customer@example.com", Phone = "0912345678",
            FullName = "Nguyen Van A", Address = "Ha Noi", AvatarUrl = "avatar.png",
            DateOfBirth = new DateOnly(2000, 1, 2), RoleId = roleId, Role = "CUSTOMER",
            IsCoach = true, Status = "Active", CreatedAt = DateTime.UtcNow.AddDays(-2), UpdatedAt = DateTime.UtcNow
        };
        var users = new Mock<IUserOperations>();
        users.Setup(x => x.GetUserByIdAsync(id, CancellationToken.None)).ReturnsAsync(expected);

        var result = await new GetUserByIdHandler(users.Object).Handle(new GetUserByIdQuery { Id = id }, CancellationToken.None);

        var actual = Assert.IsType<GetUserByIdResponse>(Assert.IsType<OkObjectResult>(result).Value);
        Assert.Same(expected, actual);
        Assert.Equal((id, "customer", "customer@example.com", "0912345678", "Nguyen Van A"),
            (actual.Id, actual.Username, actual.Email, actual.Phone, actual.FullName));
        Assert.Equal(("Ha Noi", "avatar.png", new DateOnly(2000, 1, 2), roleId, "CUSTOMER", true, "Active"),
            (actual.Address, actual.AvatarUrl, actual.DateOfBirth, actual.RoleId, actual.Role, actual.IsCoach, actual.Status));
        Assert.Equal(expected.CreatedAt, actual.CreatedAt);
        Assert.Equal(expected.UpdatedAt, actual.UpdatedAt);
        users.Verify(x => x.GetUserByIdAsync(id, CancellationToken.None), Times.Once);
    }

    [Fact(DisplayName = "UTCID02 (A) - Guid.Empty → 400, không truy vấn")]
    public async Task Handle_EmptyId_ReturnsBadRequest()
    {
        var users = new Mock<IUserOperations>();

        var result = await new GetUserByIdHandler(users.Object)
            .Handle(new GetUserByIdQuery { Id = Guid.Empty }, CancellationToken.None);

        var problem = Assert.IsType<ProblemDetails>(Assert.IsType<BadRequestObjectResult>(result).Value);
        Assert.Equal(400, problem.Status);
        Assert.Equal("Mã người dùng không hợp lệ.", problem.Title);
        users.VerifyNoOtherCalls();
    }

    [Fact(DisplayName = "UTCID03 (A) - User không tồn tại hoặc đã xóa → 404")]
    public async Task Handle_UserUnavailable_ReturnsNotFound()
    {
        var id = Guid.NewGuid();
        var users = new Mock<IUserOperations>();
        users.Setup(x => x.GetUserByIdAsync(id, CancellationToken.None)).ReturnsAsync((GetUserByIdResponse?)null);

        var result = await new GetUserByIdHandler(users.Object)
            .Handle(new GetUserByIdQuery { Id = id }, CancellationToken.None);

        var problem = Assert.IsType<ProblemDetails>(Assert.IsType<NotFoundObjectResult>(result).Value);
        Assert.Equal(404, problem.Status);
        Assert.Equal("Không tìm thấy người dùng.", problem.Title);
        users.Verify(x => x.GetUserByIdAsync(id, CancellationToken.None), Times.Once);
    }

    [Fact(DisplayName = "UTCID04 (A) - Repository lỗi → truyền tiếp exception")]
    public async Task Handle_RepositoryThrows_PropagatesException()
    {
        var id = Guid.NewGuid();
        var users = new Mock<IUserOperations>();
        var failure = new InvalidOperationException("DB unavailable");
        users.Setup(x => x.GetUserByIdAsync(id, CancellationToken.None)).ThrowsAsync(failure);

        Assert.Same(failure, await Assert.ThrowsAsync<InvalidOperationException>(() =>
            new GetUserByIdHandler(users.Object).Handle(new GetUserByIdQuery { Id = id }, CancellationToken.None)));
        users.Verify(x => x.GetUserByIdAsync(id, CancellationToken.None), Times.Once);
    }
}
