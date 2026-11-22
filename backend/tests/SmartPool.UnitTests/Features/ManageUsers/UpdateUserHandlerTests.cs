using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Moq;
using SmartPool.Application.Common.Exceptions;
using SmartPool.Application.Features.ManageUsers.Commands.UpdateUser;
using SmartPool.Application.Interfaces.Repositories;

namespace SmartPool.UnitTests.Features.ManageUsers;

public sealed class UpdateUserHandlerTests
{
    [Fact(DisplayName = "UTCID01 (N) - Cập nhật hợp lệ → 200, chuyển đủ dữ liệu đã chuẩn hóa")]
    public async Task Handle_ValidRequest_ReturnsUpdatedUser()
    {
        var h = new Harness();
        var expected = new UpdateUserResponse
        {
            Id = h.Id, Username = "customer01", Email = "user@example.com", Phone = "0912345678",
            FullName = "Nguyen Van A", Address = "Pool Street", DateOfBirth = new DateOnly(2000, 1, 2),
            AvatarUrl = "avatar.png", Role = "CUSTOMER", Status = "ACTIVE", UpdatedAt = DateTime.UtcNow
        };
        UpdateUserValues? captured = null;
        h.Users.Setup(x => x.UpdateUserAsync(It.IsAny<UpdateUserValues>(), h.Token))
            .Callback<UpdateUserValues, CancellationToken>((values, _) => captured = values)
            .ReturnsAsync(expected);

        var result = await h.Handle();

        var actual = Assert.IsType<UpdateUserResponse>(Assert.IsType<OkObjectResult>(result).Value);
        Assert.Same(expected, actual);
        Assert.Equal((h.Id, "user@example.com", "0912345678", "Nguyen Van A", new DateOnly(2000, 1, 2), "Pool Street"),
            (captured!.Id, captured.Email, captured.Phone, captured.FullName, captured.DateOfBirth, captured.Address));
        h.Users.Verify(x => x.UpdateUserAsync(It.IsAny<UpdateUserValues>(), h.Token), Times.Once);
    }

    [Fact(DisplayName = "UTCID02 (B) - Phone +84 và Address chỉ có khoảng trắng → chuẩn hóa về 0... và null")]
    public async Task Handle_InternationalPhoneAndBlankAddress_NormalizesInput()
    {
        var h = new Harness();
        UpdateUserValues? captured = null;
        h.Users.Setup(x => x.UpdateUserAsync(It.IsAny<UpdateUserValues>(), h.Token))
            .Callback<UpdateUserValues, CancellationToken>((values, _) => captured = values)
            .ReturnsAsync(new UpdateUserResponse { Id = h.Id });

        Assert.IsType<OkObjectResult>(await h.Handle(Request(h.Id, phone: " +84912345678 ", address: "   ")));

        Assert.Equal("0912345678", captured!.Phone);
        Assert.Null(captured.Address);
    }

    [Fact(DisplayName = "UTCID03 (A) - Input sai → 400 với key camelCase, không gọi repository")]
    public async Task Handle_InvalidInput_ReturnsValidationProblem()
    {
        var h = new Harness();

        var result = await h.Handle(Request(Guid.Empty, email: "bad-email", phone: "0212345678", fullName: ""));

        var badRequest = Assert.IsType<BadRequestObjectResult>(result);
        Assert.Equal(StatusCodes.Status400BadRequest, badRequest.StatusCode);
        var problem = Assert.IsType<ValidationProblemDetails>(badRequest.Value);
        Assert.Contains("Mã người dùng không hợp lệ.", problem.Errors["id"]);
        Assert.Contains("Email không đúng định dạng.", problem.Errors["email"]);
        Assert.Contains("Số điện thoại phải là số di động Việt Nam dạng 0xxxxxxxxx hoặc +84xxxxxxxxx.", problem.Errors["phone"]);
        Assert.Contains("Họ tên không được để trống.", problem.Errors["fullName"]);
        h.Users.VerifyNoOtherCalls();
    }

    [Fact(DisplayName = "UTCID04 (A) - User không tồn tại hoặc đã xóa → 404")]
    public async Task Handle_UserUnavailable_ReturnsNotFound()
    {
        var h = new Harness();
        h.Users.Setup(x => x.UpdateUserAsync(It.IsAny<UpdateUserValues>(), h.Token))
            .ReturnsAsync((UpdateUserResponse?)null);

        var result = await h.Handle();

        var problem = Assert.IsType<ProblemDetails>(Assert.IsType<NotFoundObjectResult>(result).Value);
        Assert.Equal(404, problem.Status);
        Assert.Equal("Không tìm thấy người dùng.", problem.Title);
        h.Users.Verify(x => x.UpdateUserAsync(It.IsAny<UpdateUserValues>(), h.Token), Times.Once);
    }

    [Fact(DisplayName = "UTCID05 (A) - Email đã được dùng → 409 với errors.email")]
    public async Task Handle_DuplicateEmail_ReturnsConflict()
    {
        var h = new Harness();
        h.Users.Setup(x => x.UpdateUserAsync(It.IsAny<UpdateUserValues>(), h.Token))
            .ThrowsAsync(new UniqueUserFieldException("email"));

        AssertConflict(await h.Handle(), "email", "Email đã được sử dụng.");
    }

    [Fact(DisplayName = "UTCID06 (A) - Phone đã được dùng → 409 với errors.phone")]
    public async Task Handle_DuplicatePhone_ReturnsConflict()
    {
        var h = new Harness();
        h.Users.Setup(x => x.UpdateUserAsync(It.IsAny<UpdateUserValues>(), h.Token))
            .ThrowsAsync(new UniqueUserFieldException("phone"));

        AssertConflict(await h.Handle(), "phone", "Số điện thoại đã được sử dụng.");
    }

    [Fact(DisplayName = "Đổi số điện thoại trùng username tài khoản khác → 409 phone")]
    public async Task Handle_PhoneMatchesOtherUsername_ReturnsConflict()
    {
        var h = new Harness();
        h.Users.Setup(x => x.UpdateUserAsync(It.IsAny<UpdateUserValues>(), h.Token))
            .ThrowsAsync(new UniqueUserFieldException("phone"));

        AssertConflict(await h.Handle(), "phone", "Số điện thoại đã được sử dụng.");
    }

    [Fact(DisplayName = "UTCID07 (A) - Repository lỗi bất ngờ → exception được truyền tiếp")]
    public async Task Handle_RepositoryThrows_PropagatesException()
    {
        var h = new Harness();
        var failure = new InvalidOperationException("DB unavailable");
        h.Users.Setup(x => x.UpdateUserAsync(It.IsAny<UpdateUserValues>(), h.Token)).ThrowsAsync(failure);

        Assert.Same(failure, await Assert.ThrowsAsync<InvalidOperationException>(() => h.Handle()));
        h.Users.Verify(x => x.UpdateUserAsync(It.IsAny<UpdateUserValues>(), h.Token), Times.Once);
    }

    private static void AssertConflict(IActionResult result, string field, string message)
    {
        var response = Assert.IsType<ObjectResult>(result);
        Assert.Equal(StatusCodes.Status409Conflict, response.StatusCode);
        Assert.Contains("application/problem+json", response.ContentTypes);
        var problem = Assert.IsType<ProblemDetails>(response.Value);
        Assert.Equal(409, problem.Status);
        Assert.Equal("Không thể cập nhật người dùng.", problem.Title);
        var errors = Assert.IsType<Dictionary<string, string[]>>(problem.Extensions["errors"]);
        Assert.Equal([message], errors[field]);
    }

    private static UpdateUserCommand Request(Guid id, string email = " User@Example.COM ",
        string phone = " 0912345678 ", string fullName = " Nguyen Van A ", string? address = " Pool Street ") =>
        new() { Id = id, Email = email, Phone = phone, FullName = fullName,
            DateOfBirth = new DateOnly(2000, 1, 2), Address = address };

    private sealed class Harness
    {
        public readonly Guid Id = Guid.NewGuid();
        public readonly Mock<IUserOperations> Users = new();
        public readonly CancellationToken Token = new CancellationTokenSource().Token;
        private readonly UpdateUserHandler _handler;

        public Harness() => _handler = new UpdateUserHandler(Users.Object, new UpdateUserValidator());

        public Task<IActionResult> Handle(UpdateUserCommand? command = null) =>
            _handler.Handle(command ?? Request(Id), Token);
    }
}
