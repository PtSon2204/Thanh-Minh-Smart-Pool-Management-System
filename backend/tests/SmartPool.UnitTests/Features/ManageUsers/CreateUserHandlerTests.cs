using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Moq;
using SmartPool.Application.Common.Exceptions;
using SmartPool.Application.Features.ManageUsers.Commands.CreateUser;
using SmartPool.Application.Interfaces.Repositories;
using SmartPool.Application.Interfaces.Services;
using SmartPool.Domain.Entities;

namespace SmartPool.UnitTests.Features.ManageUsers;

public sealed class CreateUserHandlerTests
{
    [Fact(DisplayName = "UTCID01 (N) - Dữ liệu hợp lệ → 201, lưu user và profile, hash mật khẩu")]
    public async Task Handle_ValidRequest_CreatesUserAndProfile()
    {
        var h = new Harness();
        var command = Request();

        var result = await h.Handle(command);

        var created = Assert.IsType<ObjectResult>(result);
        Assert.Equal(StatusCodes.Status201Created, created.StatusCode);
        var response = Assert.IsType<CreateUserResponse>(created.Value);
        Assert.Equal(("customer01", "customer@example.com", "0912345678", "Nguyen Van A", "CUSTOMER", "ACTIVE"),
            (response.Username, response.Email, response.Phone, response.FullName, response.Role, response.Status));
        Assert.Equal(new DateOnly(2000, 1, 2), response.DateOfBirth);
        Assert.Equal("Pool Street", response.Address);
        Assert.NotEqual(Guid.Empty, response.Id);
        Assert.NotEqual(default, response.CreatedAt);
        var savedUser = Assert.IsType<User>(h.SavedUser);
        var savedProfile = Assert.IsType<UserProfile>(h.SavedProfile);
        Assert.Equal(response.Id, savedUser.Id);
        Assert.Equal("hashed-password", savedUser.PasswordHash);
        Assert.Equal(h.Role.Id, savedUser.RoleId);
        Assert.False(savedUser.IsDeleted);
        Assert.Equal("ACTIVE", savedUser.Status);
        Assert.Equal(savedUser.Id, savedProfile.UserId);
        Assert.Same(savedUser, savedProfile.User);
        Assert.Same(savedProfile, savedUser.UserProfile);
        Assert.Null(h.SavedNotification);
        h.PasswordHasher.Verify(x => x.HashPassword(savedUser, "Password123"), Times.Once);
        h.Users.Verify(x => x.CreateUserWithProfileAsync(savedUser, savedProfile, null, h.Token), Times.Once);
    }

    [Fact(DisplayName = "UTCID02 (B) - Email/role chữ hoa, phone +84, address trắng → chuẩn hóa khi lưu")]
    public async Task Handle_AlternativeInput_NormalizesFields()
    {
        var h = new Harness();
        var result = await h.Handle(Request(email: " CUSTOMER@Example.COM ", phone: " +84912345678 ",
            role: " customer ", address: "   "));

        var response = Assert.IsType<CreateUserResponse>(Assert.IsType<ObjectResult>(result).Value);
        Assert.Equal("customer@example.com", response.Email);
        Assert.Equal("0912345678", response.Phone);
        Assert.Equal("CUSTOMER", response.Role);
        Assert.Null(response.Address);
        h.Users.Verify(x => x.ExistsByPhoneAsync("0912345678", "+84912345678", h.Token), Times.Once);
        h.Users.Verify(x => x.GetRoleByNameAsync("CUSTOMER", h.Token), Times.Once);
    }

    [Fact(DisplayName = "UTCID03 (A) - Input không hợp lệ → 400 lỗi theo trường, không truy vấn")]
    public async Task Handle_InvalidRequest_ReturnsValidationProblem()
    {
        var h = new Harness();
        var result = await h.Handle(Request(username: "", email: "bad-email", password: "short", confirmPassword: "short"));

        var badRequest = Assert.IsType<BadRequestObjectResult>(result);
        Assert.Equal(StatusCodes.Status400BadRequest, badRequest.StatusCode);
        var problem = Assert.IsType<ValidationProblemDetails>(badRequest.Value);
        Assert.Contains("Tên đăng nhập không được để trống.", problem.Errors["username"]);
        Assert.Contains("Email không đúng định dạng.", problem.Errors["email"]);
        Assert.Contains("Mật khẩu phải dài từ 8 đến 128 ký tự.", problem.Errors["password"]);
        h.Users.VerifyNoOtherCalls();
        h.PasswordHasher.VerifyNoOtherCalls();
    }

    [Fact(DisplayName = "UTCID04 (A) - Username đã tồn tại → 409, dừng trước kiểm tra email")]
    public async Task Handle_DuplicateUsername_ReturnsConflict()
    {
        var h = new Harness();
        h.Users.Setup(x => x.ExistsByUsernameAsync("customer01", h.Token)).ReturnsAsync(true);

        AssertConflict(await h.Handle(), "username", "Tên đăng nhập đã được sử dụng.");
        h.Users.Verify(x => x.ExistsByEmailAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
        h.AssertNoCreate();
    }

    [Fact(DisplayName = "UTCID05 (A) - Email đã tồn tại → 409, dừng trước kiểm tra phone")]
    public async Task Handle_DuplicateEmail_ReturnsConflict()
    {
        var h = new Harness();
        h.Users.Setup(x => x.ExistsByEmailAsync("customer@example.com", h.Token)).ReturnsAsync(true);

        AssertConflict(await h.Handle(), "email", "Email đã được sử dụng.");
        h.Users.Verify(x => x.ExistsByPhoneAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
        h.AssertNoCreate();
    }

    [Fact(DisplayName = "UTCID06 (A) - Số điện thoại đã tồn tại → 409, không hash/lưu")]
    public async Task Handle_DuplicatePhone_ReturnsConflict()
    {
        var h = new Harness();
        h.Users.Setup(x => x.ExistsByPhoneAsync("0912345678", "+84912345678", h.Token)).ReturnsAsync(true);

        AssertConflict(await h.Handle(), "phone", "Số điện thoại đã được sử dụng.");
        h.AssertNoCreate();
    }

    [Fact(DisplayName = "Username trùng số điện thoại tài khoản khác → 409 username")]
    public async Task Handle_UsernameMatchesExistingPhone_ReturnsConflict()
    {
        var h = new Harness();
        h.Users.Setup(x => x.ExistsByUsernameAsync("0912345678", h.Token)).ReturnsAsync(true);

        AssertConflict(await h.Handle(Request(username: "0912345678", phone: "0987654321")),
            "username", "Tên đăng nhập đã được sử dụng.");
        h.AssertNoCreate();
    }

    [Fact(DisplayName = "Số điện thoại trùng username tài khoản khác → 409 phone")]
    public async Task Handle_PhoneMatchesExistingUsername_ReturnsConflict()
    {
        var h = new Harness();
        h.Users.Setup(x => x.ExistsByPhoneAsync("0912345678", "+84912345678", h.Token))
            .ReturnsAsync(true);

        AssertConflict(await h.Handle(), "phone", "Số điện thoại đã được sử dụng.");
        h.AssertNoCreate();
    }

    [Fact(DisplayName = "UTCID07 (A) - Role chưa cấu hình → 409, không hash/lưu")]
    public async Task Handle_RoleUnavailable_ReturnsConflict()
    {
        var h = new Harness();
        h.Users.Setup(x => x.GetRoleByNameAsync("CUSTOMER", h.Token)).ReturnsAsync((Role?)null);

        AssertConflict(await h.Handle(), "role", "Vai trò chưa được cấu hình hoặc đã ngừng sử dụng.");
        h.AssertNoCreate();
    }

    [Fact(DisplayName = "UTCID08 (A) - Trùng email khi lưu → 409, không trả 201")]
    public async Task Handle_ConcurrentDuplicateAtSave_ReturnsConflict()
    {
        var h = new Harness();
        h.Users.Setup(x => x.CreateUserWithProfileAsync(It.IsAny<User>(), It.IsAny<UserProfile>(), null, h.Token))
            .ThrowsAsync(new UniqueUserFieldException("email"));

        AssertConflict(await h.Handle(), "email", "Email đã được sử dụng.");
        h.Users.Verify(x => x.CreateUserWithProfileAsync(It.IsAny<User>(), It.IsAny<UserProfile>(), null, h.Token), Times.Once);
    }

    [Fact(DisplayName = "UTCID09 (A) - Lỗi DB khi lưu → exception được truyền tiếp")]
    public async Task Handle_SaveThrows_PropagatesException()
    {
        var h = new Harness();
        var failure = new InvalidOperationException("DB unavailable");
        h.Users.Setup(x => x.CreateUserWithProfileAsync(It.IsAny<User>(), It.IsAny<UserProfile>(), null, h.Token))
            .ThrowsAsync(failure);

        Assert.Same(failure, await Assert.ThrowsAsync<InvalidOperationException>(() => h.Handle()));
        h.Users.Verify(x => x.CreateUserWithProfileAsync(It.IsAny<User>(), It.IsAny<UserProfile>(), null, h.Token), Times.Once);
    }

    [Fact(DisplayName = "UTCID10 (A) - Lỗi kiểm tra trùng → exception được truyền tiếp, không lưu")]
    public async Task Handle_ExistenceCheckThrows_PropagatesException()
    {
        var h = new Harness();
        var failure = new InvalidOperationException("DB unavailable");
        h.Users.Setup(x => x.ExistsByUsernameAsync("customer01", h.Token)).ThrowsAsync(failure);

        Assert.Same(failure, await Assert.ThrowsAsync<InvalidOperationException>(() => h.Handle()));
        h.AssertNoCreate();
    }

    [Fact(DisplayName = "UTCID11 (A) - Hash mật khẩu lỗi → exception được truyền tiếp, không lưu")]
    public async Task Handle_HashThrows_PropagatesException()
    {
        var h = new Harness();
        var failure = new InvalidOperationException("Hasher unavailable");
        h.PasswordHasher.Setup(x => x.HashPassword(It.IsAny<User>(), "Password123")).Throws(failure);

        Assert.Same(failure, await Assert.ThrowsAsync<InvalidOperationException>(() => h.Handle()));
        h.Users.Verify(x => x.CreateUserWithProfileAsync(It.IsAny<User>(), It.IsAny<UserProfile>(), It.IsAny<Notification?>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    private static void AssertConflict(IActionResult result, string field, string message)
    {
        var response = Assert.IsType<ObjectResult>(result);
        Assert.Equal(StatusCodes.Status409Conflict, response.StatusCode);
        var problem = Assert.IsType<ProblemDetails>(response.Value);
        Assert.Equal(StatusCodes.Status409Conflict, problem.Status);
        Assert.Equal("Không thể tạo tài khoản.", problem.Title);
        var errors = Assert.IsType<Dictionary<string, string[]>>(problem.Extensions["errors"]);
        Assert.Equal([message], errors[field]);
    }

    private static CreateUserCommand Request(string username = "Customer01", string email = "Customer@Example.com",
        string phone = "0912345678", string password = "Password123", string confirmPassword = "Password123",
        string role = "CUSTOMER", string? address = " Pool Street ") => new()
        {
            Username = username, Email = email, Phone = phone, Password = password,
            ConfirmPassword = confirmPassword, Role = role, FullName = " Nguyen Van A ",
            DateOfBirth = new DateOnly(2000, 1, 2), Address = address
        };

    private sealed class Harness
    {
        public readonly Mock<IUserOperations> Users = new();
        public readonly Mock<IPasswordHasher> PasswordHasher = new();
        public readonly Role Role = new() { Id = Guid.NewGuid(), Name = "CUSTOMER" };
        public readonly CancellationToken Token = new CancellationTokenSource().Token;
        public User? SavedUser;
        public UserProfile? SavedProfile;
        public Notification? SavedNotification;
        private readonly CreateUserHandler _handler;

        public Harness()
        {
            Users.Setup(x => x.GetRoleByNameAsync("CUSTOMER", Token)).ReturnsAsync(Role);
            Users.Setup(x => x.CreateUserWithProfileAsync(It.IsAny<User>(), It.IsAny<UserProfile>(),
                    It.IsAny<Notification?>(), Token))
                .Callback<User, UserProfile, Notification?, CancellationToken>((user, profile, notification, _) =>
                {
                    SavedUser = user; SavedProfile = profile; SavedNotification = notification;
                }).Returns(Task.CompletedTask);
            PasswordHasher.Setup(x => x.HashPassword(It.IsAny<User>(), "Password123")).Returns("hashed-password");
            _handler = new CreateUserHandler(Users.Object, PasswordHasher.Object, new CreateUserValidator());
        }

        public Task<IActionResult> Handle(CreateUserCommand? request = null) =>
            _handler.Handle(request ?? Request(), Token);

        public void AssertNoCreate()
        {
            PasswordHasher.VerifyNoOtherCalls();
            Users.Verify(x => x.CreateUserWithProfileAsync(It.IsAny<User>(), It.IsAny<UserProfile>(),
                It.IsAny<Notification?>(), It.IsAny<CancellationToken>()), Times.Never);
            Assert.Null(SavedUser);
        }
    }
}
