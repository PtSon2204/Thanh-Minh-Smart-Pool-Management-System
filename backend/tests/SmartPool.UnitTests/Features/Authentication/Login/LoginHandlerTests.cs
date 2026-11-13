using FluentValidation;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Moq;
using SmartPool.Application.Features.Authentication.Commands.Login;
using SmartPool.Application.Features.Authentication.Models;
using SmartPool.Application.Interfaces.Repositories;
using SmartPool.Application.Interfaces.Services;
using SmartPool.Domain.Entities;

namespace SmartPool.UnitTests.Features.Authentication.Login;

public sealed class LoginHandlerTests
{
    private static readonly DateTime AccessExpiresAtUtc = new(2026, 11, 10, 2, 15, 0, DateTimeKind.Utc);
    private static readonly DateTime RefreshExpiresAtUtc = new(2026, 11, 17, 2, 0, 0, DateTimeKind.Utc);

    [Fact(DisplayName = "UTCID01 (N) - Đăng nhập bằng username -> Trả 200 với token và role")]
    public async Task Handle_ValidUsername_ReturnsOkWithTokens()
    {
        var harness = new Harness();
        var request = new LoginCommand { Identifier = "customer", Password = "Password123" };

        var result = await harness.Handle(request);

        var response = AssertSuccessResponse(result, "Admin");
        Assert.Equal(harness.DefaultUsers[0].Id, response.Id);
        Assert.Equal("customer", response.Username);
        Assert.Equal(new[] { "Admin" }, response.Roles);
        harness.PasswordHasher.Verify(h => h.VerifyPassword(
            It.Is<User>(u => u.Username == "customer"), "hashed", "Password123"), Times.Once);
        harness.AccessTokenService.Verify(s => s.CreateToken(It.IsAny<User>(), "Admin", It.IsAny<IReadOnlyCollection<string>>()), Times.Once);
        harness.RefreshTokenService.Verify(s => s.CreateAsync(
            harness.DefaultUsers[0].Id, "hashed", CancellationToken.None), Times.Once);
        harness.Membership.Verify(m => m.GetActiveRolesAsync(
            harness.DefaultUsers[0].Id, "Admin", CancellationToken.None), Times.Once);
    }

    [Fact(DisplayName = "UTCID02 (N) - Đăng nhập bằng email có chữ hoa, khoảng trắng -> Trả 200")]
    public async Task Handle_ValidEmail_ReturnsOkWithTokens()
    {
        var harness = new Harness();
        var request = new LoginCommand { Identifier = " CuStomer@Example.com ", Password = "Password123" };

        var result = await harness.Handle(request);

        AssertSuccessResponse(result, "Admin");
        harness.PasswordHasher.Verify(h => h.VerifyPassword(
            It.Is<User>(u => u.Email == "customer@example.com"), "hashed", "Password123"), Times.Once);
    }

    [Fact(DisplayName = "UTCID03 (N) - Đăng nhập bằng số điện thoại 0... -> Trả 200")]
    public async Task Handle_ValidLocalPhone_ReturnsOkWithTokens()
    {
        var harness = new Harness();
        var request = new LoginCommand { Identifier = "0912345678", Password = "Password123" };

        var result = await harness.Handle(request);

        AssertSuccessResponse(result, "Admin");
        harness.PasswordHasher.Verify(h => h.VerifyPassword(
            It.Is<User>(u => u.Phone == "0912345678"), "hashed", "Password123"), Times.Once);
    }

    [Fact(DisplayName = "UTCID04 (N) - Đăng nhập bằng số điện thoại +84... -> Trả 200")]
    public async Task Handle_ValidInternationalPhone_ReturnsOkWithTokens()
    {
        var harness = new Harness();
        var request = new LoginCommand { Identifier = "+84912345678", Password = "Password123" };

        var result = await harness.Handle(request);

        AssertSuccessResponse(result, "Admin");
        harness.PasswordHasher.Verify(h => h.VerifyPassword(
            It.Is<User>(u => u.Phone == "0912345678"), "hashed", "Password123"), Times.Once);
    }

    [Fact(DisplayName = "UTCID05 (A) - Không tìm thấy tài khoản → Trả 401, không tạo token")]
    public async Task Handle_AccountNotFound_ReturnsUnauthorized()
    {
        var harness = new Harness();
        
        var result = await harness.Handle(new LoginCommand { Identifier = "wronguser", Password = "Password123" });

        AssertProblem(result, StatusCodes.Status401Unauthorized);
        harness.AssertNoTokensCreated();
    }

    [Fact(DisplayName = "UTCID06 (A) - Sai mật khẩu → Trả 401, không tạo token")]
    public async Task Handle_WrongPassword_ReturnsUnauthorized()
    {
        var harness = new Harness();
        harness.PasswordHasher.Setup(h => h.VerifyPassword(It.IsAny<User>(), It.IsAny<string>(), "WrongPassword"))
            .Returns(false);

        var result = await harness.Handle(new LoginCommand { Identifier = "customer", Password = "WrongPassword" });

        AssertProblem(result, StatusCodes.Status401Unauthorized);
        harness.AssertNoTokensCreated();
    }

    [Fact(DisplayName = "UTCID07 (A) - Tài khoản không Active → Trả 401")]
    public async Task Handle_AccountNotActive_ReturnsUnauthorized()
    {
        var harness = new Harness();
        harness.DefaultUsers[0].Status = "Inactive";

        var result = await harness.Handle();

        AssertProblem(result, StatusCodes.Status401Unauthorized);
        harness.AssertNoTokensCreated();
    }

    [Fact(DisplayName = "UTCID08 (A) - Tài khoản đã xóa → Trả 401")]
    public async Task Handle_AccountDeleted_ReturnsUnauthorized()
    {
        var harness = new Harness();
        harness.DefaultUsers[0].IsDeleted = true;

        var result = await harness.Handle();

        AssertProblem(result, StatusCodes.Status401Unauthorized);
        harness.AssertNoTokensCreated();
    }

    [Fact(DisplayName = "UTCID09 (A) - Tài khoản không có password hash → Trả 401")]
    public async Task Handle_NoPasswordHash_ReturnsUnauthorized()
    {
        var harness = new Harness();
        harness.DefaultUsers[0].PasswordHash = null;

        var result = await harness.Handle();

        AssertProblem(result, StatusCodes.Status401Unauthorized);
        harness.AssertNoTokensCreated();
    }

    [Fact(DisplayName = "UTCID10 (A) - Thiếu RoleId → Trả 401")]
    public async Task Handle_MissingRoleId_ReturnsUnauthorized()
    {
        var harness = new Harness();
        harness.DefaultUsers[0].RoleId = null;

        var result = await harness.Handle();

        AssertProblem(result, StatusCodes.Status401Unauthorized);
        harness.AssertNoTokensCreated();
    }

    [Fact(DisplayName = "UTCID11 (A) - Không tìm thấy role → Trả 401")]
    public async Task Handle_RoleNotFound_ReturnsUnauthorized()
    {
        var harness = new Harness();
        harness.DefaultUsers[0].RoleId = Guid.NewGuid();

        var result = await harness.Handle();

        AssertProblem(result, StatusCodes.Status401Unauthorized);
        harness.AssertNoTokensCreated();
    }

    [Fact(DisplayName = "UTCID12 (A) - Role đã xóa → Trả 401")]
    public async Task Handle_RoleDeleted_ReturnsUnauthorized()
    {
        var harness = new Harness();
        harness.DefaultRoles[0].IsDeleted = true;

        var result = await harness.Handle();

        AssertProblem(result, StatusCodes.Status401Unauthorized);
        harness.AssertNoTokensCreated();
    }

    [Fact(DisplayName = "UTCID13 (A) - Không tạo được refresh token → Trả 401, không tạo access token")]
    public async Task Handle_RefreshTokenCreationFails_ReturnsUnauthorized()
    {
        var harness = new Harness();
        harness.RefreshTokenService.Setup(s => s.CreateAsync(
                It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((RefreshTokenResult?)null);

        var result = await harness.Handle();

        AssertProblem(result, StatusCodes.Status401Unauthorized);
        harness.AccessTokenService.VerifyNoOtherCalls();
    }

    [Fact(DisplayName = "UTCID14 (B) - SDT trùng Username, chỉ 1 tài khoản khớp mật khẩu → Chọn đúng tài khoản")]
    public async Task Handle_OverlappingPhoneAndUsername_OnlyOneMatchesPassword_ReturnsOk()
    {
        var harness = new Harness();
        var user2 = new User
        {
            Id = Guid.NewGuid(), Username = "0912345678", Status = "Active", 
            IsDeleted = false, PasswordHash = "hashed2", RoleId = harness.DefaultRoles[0].Id
        };
        harness.DefaultUsers.Add(user2);

        harness.PasswordHasher.Setup(h => h.VerifyPassword(harness.DefaultUsers[0], "hashed", "Password123")).Returns(true);
        harness.PasswordHasher.Setup(h => h.VerifyPassword(user2, "hashed2", "Password123")).Returns(false);

        var result = await harness.Handle(new LoginCommand { Identifier = "0912345678", Password = "Password123" });

        var okResult = Assert.IsType<OkObjectResult>(result);
        var response = Assert.IsType<LoginResponse>(okResult.Value);
        Assert.Equal(harness.DefaultUsers[0].Id, response.Id);
    }

    [Fact(DisplayName = "UTCID15 (B) - SDT trùng Username, cả 2 tài khoản khớp mật khẩu → Trả 401")]
    public async Task Handle_OverlappingPhoneAndUsername_BothMatchPassword_ReturnsUnauthorized()
    {
        var harness = new Harness();
        var user2 = new User
        {
            Id = Guid.NewGuid(), Username = "0912345678", Status = "Active", 
            IsDeleted = false, PasswordHash = "hashed2", RoleId = harness.DefaultRoles[0].Id
        };
        harness.DefaultUsers.Add(user2);

        harness.PasswordHasher.Setup(h => h.VerifyPassword(harness.DefaultUsers[0], "hashed", "Password123")).Returns(true);
        harness.PasswordHasher.Setup(h => h.VerifyPassword(user2, "hashed2", "Password123")).Returns(true);

        var result = await harness.Handle(new LoginCommand { Identifier = "0912345678", Password = "Password123" });

        AssertProblem(result, StatusCodes.Status401Unauthorized);
        harness.AssertNoTokensCreated();
    }

    [Fact(DisplayName = "UTCID16 (N) - User có nhiều role hợp lệ → Roles và access token nhận đúng danh sách")]
    public async Task Handle_UserWithMultipleRoles_ReturnsAllRoles()
    {
        var harness = new Harness();
        var extraRoles = new List<string> { "Admin", "Manager" };
        harness.Membership.Setup(m => m.GetActiveRolesAsync(
                It.IsAny<Guid>(), "Admin", It.IsAny<CancellationToken>()))
            .ReturnsAsync(extraRoles);

        var result = await harness.Handle();

        var okResult = Assert.IsType<OkObjectResult>(result);
        var response = Assert.IsType<LoginResponse>(okResult.Value);
        Assert.Equal(extraRoles, response.Roles);
        harness.AccessTokenService.Verify(s => s.CreateToken(It.IsAny<User>(), "Admin", extraRoles), Times.Once);
        harness.Membership.Verify(m => m.GetActiveRolesAsync(
            harness.DefaultUsers[0].Id, "Admin", CancellationToken.None), Times.Once);
    }

    [Fact(DisplayName = "UTCID17 (A) - User Repository ném exception → Truyền tiếp exception")]
    public async Task Handle_UserRepositoryThrowsException_ThrowsException()
    {
        var harness = new Harness();
        harness.Users.Setup(r => r.FindUsersByIdentifierAsync(
                It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("DB Failure"));

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() => harness.Handle());
        Assert.Equal("DB Failure", exception.Message);
        harness.AssertNoTokensCreated();
    }

    [Fact(DisplayName = "UTCID18 (A) - Refresh Token Service ném exception → Truyền tiếp exception")]
    public async Task Handle_RefreshTokenServiceThrowsException_ThrowsException()
    {
        var harness = new Harness();
        harness.RefreshTokenService.Setup(s => s.CreateAsync(
                It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("Redis Failure"));

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() => harness.Handle());
        Assert.Equal("Redis Failure", exception.Message);
        harness.AccessTokenService.VerifyNoOtherCalls();
    }

    [Fact(DisplayName = "UTCID19 (A) - Identifier rỗng → Handler trả 400 BadRequest với key 'identifier'")]
    public async Task Handle_EmptyIdentifier_ReturnsBadRequestWithValidationErrors()
    {
        var harness = new Harness();
        var request = new LoginCommand { Identifier = "", Password = "Password123" };

        var result = await harness.Handle(request);

        AssertValidationProblem(result, "identifier", "Vui lòng nhập tên đăng nhập, email hoặc số điện thoại.");
        harness.AssertNoTokensCreated();
    }

    [Fact(DisplayName = "UTCID20 (B) - Identifier chỉ có khoảng trắng → Handler trả 400 BadRequest với key 'identifier'")]
    public async Task Handle_WhitespaceIdentifier_ReturnsBadRequestWithValidationErrors()
    {
        var harness = new Harness();
        var request = new LoginCommand { Identifier = "   ", Password = "Password123" };

        var result = await harness.Handle(request);

        AssertValidationProblem(result, "identifier", "Vui lòng nhập tên đăng nhập, email hoặc số điện thoại.");
        harness.AssertNoTokensCreated();
    }

    [Fact(DisplayName = "UTCID21 (B) - Identifier vượt 150 ký tự → Handler trả 400 BadRequest với key 'identifier'")]
    public async Task Handle_IdentifierExceeds150Chars_ReturnsBadRequestWithValidationErrors()
    {
        var harness = new Harness();
        var request = new LoginCommand { Identifier = new string('a', 151), Password = "Password123" };

        var result = await harness.Handle(request);

        AssertValidationProblem(result, "identifier", "Thông tin đăng nhập không được vượt quá 150 ký tự.");
        harness.AssertNoTokensCreated();
    }

    [Fact(DisplayName = "UTCID22 (A) - Mật khẩu rỗng → Handler trả 400 BadRequest với key 'password'")]
    public async Task Handle_EmptyPassword_ReturnsBadRequestWithValidationErrors()
    {
        var harness = new Harness();
        var request = new LoginCommand { Identifier = "customer", Password = "" };

        var result = await harness.Handle(request);

        AssertValidationProblem(result, "password", "Vui lòng nhập mật khẩu.");
        harness.AssertNoTokensCreated();
    }

    [Fact(DisplayName = "UTCID23 (B) - Identifier đúng 150 ký tự và mật khẩu hợp lệ → Đăng nhập thành công 200 OK")]
    public async Task Handle_Identifier150CharsValid_ReturnsOkWithTokens()
    {
        var harness = new Harness();
        var valid150Identifier = new string('a', 150);
        harness.DefaultUsers[0].Username = valid150Identifier;
        var request = new LoginCommand { Identifier = valid150Identifier, Password = "Password123" };

        var result = await harness.Handle(request);

        AssertSuccessResponse(result, "Admin");
    }

    [Fact(DisplayName = "UTCID24 (N) - Không có membership service -> Dùng role chính để tạo access token")]
    public async Task Handle_WithoutMembership_UsesPrimaryRole()
    {
        var harness = new Harness();

        var result = await harness.Handle(includeMembership: false);

        var response = AssertSuccessResponse(result, "Admin");
        Assert.Equal(new[] { "Admin" }, response.Roles);
        harness.AccessTokenService.Verify(s => s.CreateToken(
            harness.DefaultUsers[0], "Admin"), Times.Once);
        harness.AccessTokenService.Verify(s => s.CreateToken(
            It.IsAny<User>(), It.IsAny<string>(), It.IsAny<IReadOnlyCollection<string>>()), Times.Never);
        harness.Membership.VerifyNoOtherCalls();
    }

    [Fact(DisplayName = "UTCID25 (B) - Tài khoản chỉ có email -> Username trong response là chuỗi rỗng")]
    public async Task Handle_EmailOnlyAccount_UsesEmptyUsername()
    {
        var harness = new Harness();
        harness.DefaultUsers[0].Username = null;

        var result = await harness.Handle(new LoginCommand
        {
            Identifier = "customer@example.com", Password = "Password123"
        });

        var response = AssertSuccessResponse(result, "Admin");
        Assert.Equal(harness.DefaultUsers[0].Id, response.Id);
        Assert.Equal(string.Empty, response.Username);
    }

    [Fact(DisplayName = "UTCID26 (B) - Trạng thái active viết thường -> Vẫn đăng nhập thành công")]
    public async Task Handle_LowercaseActiveStatus_ReturnsOk()
    {
        var harness = new Harness();
        harness.DefaultUsers[0].Status = "active";

        var result = await harness.Handle();

        var response = AssertSuccessResponse(result, "Admin");
        Assert.Equal(harness.DefaultUsers[0].Id, response.Id);
    }

    private static LoginResponse AssertSuccessResponse(IActionResult result, string expectedRole)
    {
        var objectResult = Assert.IsType<OkObjectResult>(result);
        var response = Assert.IsType<LoginResponse>(objectResult.Value);
        Assert.NotEqual(Guid.Empty, response.Id);
        Assert.Equal("access-token", response.AccessToken);
        Assert.Equal(AccessExpiresAtUtc, response.ExpiresAtUtc);
        Assert.Equal("refresh-token", response.RefreshToken);
        Assert.Equal(RefreshExpiresAtUtc, response.RefreshTokenExpiresAtUtc);
        Assert.Equal(expectedRole, response.Role);
        Assert.Contains(expectedRole, response.Roles);
        return response;
    }

    private static void AssertProblem(IActionResult result, int status)
    {
        var objectResult = Assert.IsType<ObjectResult>(result);
        Assert.Equal(status, objectResult.StatusCode);
        var problem = Assert.IsType<ProblemDetails>(objectResult.Value);
        Assert.Equal(status, problem.Status);
        if (status == StatusCodes.Status401Unauthorized)
            Assert.Equal("Thông tin đăng nhập không hợp lệ.", problem.Title);
    }

    private static void AssertValidationProblem(IActionResult result, string expectedProperty, string expectedErrorMessage)
    {
        var badRequestResult = Assert.IsType<BadRequestObjectResult>(result);
        Assert.Equal(StatusCodes.Status400BadRequest, badRequestResult.StatusCode);

        var problem = Assert.IsType<ValidationProblemDetails>(badRequestResult.Value);
        Assert.True(problem.Errors.ContainsKey(expectedProperty), $"Validation errors must contain key '{expectedProperty}'.");
        Assert.Contains(expectedErrorMessage, problem.Errors[expectedProperty]);
    }

    private sealed class Harness
    {
        public Mock<IUserOperations> Users { get; } = new();
        public Mock<IPasswordHasher> PasswordHasher { get; } = new();
        public Mock<IAccessTokenService> AccessTokenService { get; } = new();
        public Mock<IRefreshTokenService> RefreshTokenService { get; } = new();
        public Mock<IUserRoleMembershipService> Membership { get; } = new();

        public List<User> DefaultUsers { get; } = new();
        public List<Role> DefaultRoles { get; } = new();

        public Harness()
        {
            var roleId = Guid.NewGuid();
            DefaultRoles.Add(new Role { Id = roleId, Name = "Admin", IsDeleted = false });

            DefaultUsers.Add(new User
            {
                Id = Guid.NewGuid(),
                Username = "customer",
                Email = "customer@example.com",
                Phone = "0912345678",
                PasswordHash = "hashed",
                Status = "Active",
                IsDeleted = false,
                RoleId = roleId
            });

            Users.Setup(repository => repository.FindUsersByIdentifierAsync(
                    It.IsAny<string>(), CancellationToken.None))
                .ReturnsAsync((string identifier, CancellationToken _) =>
                    DefaultUsers.Where(u => string.Equals(u.Username, identifier, StringComparison.OrdinalIgnoreCase)
                        || string.Equals(u.Email, identifier, StringComparison.OrdinalIgnoreCase)
                        || u.Phone == identifier
                        || (identifier.StartsWith("+84", StringComparison.Ordinal)
                            && u.Phone == "0" + identifier[3..])).ToList());

            Users.Setup(repository => repository.GetRoleByIdAsync(
                    It.IsAny<Guid>(), CancellationToken.None))
                .ReturnsAsync((Guid id, CancellationToken _) =>
                    DefaultRoles.FirstOrDefault(r => r.Id == id));

            PasswordHasher.Setup(hasher => hasher.VerifyPassword(
                    It.IsAny<User>(), It.IsAny<string>(), "Password123"))
                .Returns(true);

            Membership.Setup(service => service.GetActiveRolesAsync(
                    It.IsAny<Guid>(), It.IsAny<string>(), CancellationToken.None))
                .ReturnsAsync((Guid _, string primaryRole, CancellationToken _) =>
                    (IReadOnlyList<string>)[primaryRole]);

            AccessTokenService.Setup(service => service.CreateToken(
                    It.IsAny<User>(), It.IsAny<string>()))
                .Returns(new AccessTokenResult("access-token", AccessExpiresAtUtc));
            
            AccessTokenService.Setup(service => service.CreateToken(
                    It.IsAny<User>(), It.IsAny<string>(), It.IsAny<IReadOnlyCollection<string>>()))
                .Returns(new AccessTokenResult("access-token", AccessExpiresAtUtc));

            RefreshTokenService.Setup(service => service.CreateAsync(
                    It.IsAny<Guid>(), It.IsAny<string>(), CancellationToken.None))
                .ReturnsAsync(new RefreshTokenResult("refresh-token", RefreshExpiresAtUtc));
        }

        public Task<IActionResult> Handle(LoginCommand? request = null, bool includeMembership = true)
        {
            var handler = new LoginHandler(
                Users.Object,
                PasswordHasher.Object,
                AccessTokenService.Object,
                RefreshTokenService.Object,
                new LoginValidator(),
                includeMembership ? Membership.Object : null);

            return handler.Handle(request ?? new LoginCommand
            {
                Identifier = "customer", 
                Password = "Password123"
            }, CancellationToken.None);
        }

        public void AssertNoTokensCreated()
        {
            AccessTokenService.VerifyNoOtherCalls();
            RefreshTokenService.VerifyNoOtherCalls();
        }
    }
}
