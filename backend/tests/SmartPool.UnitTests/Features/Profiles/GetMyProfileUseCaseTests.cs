using System.Linq.Expressions;
using System.Security.Claims;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Moq;
using SmartPool.API.Authorization;
using SmartPool.API.Controllers;
using SmartPool.Application.Common.Authorization;
using SmartPool.Application.Features.Profiles.Queries.GetMyProfile;
using SmartPool.Application.Interfaces.Repositories;
using SmartPool.Domain.Entities;

namespace SmartPool.UnitTests.Features.Profiles;

public sealed class GetMyProfileUseCaseTests
{
    [Fact(DisplayName = "UTCID01 (N) - User và profile tồn tại -> Trả đúng thông tin theo UserId")]
    public async Task UTCID01_UserAndProfileExist_ReturnsMappedProfile()
    {
        var userId = Guid.NewGuid();
        var user = NewUser(userId);
        var profile = new UserProfile
        {
            UserId = userId,
            FullName = "Nguyen Van A",
            AvatarUrl = "https://example.test/avatar.png",
            Address = "Ha Noi",
            DateOfBirth = new DateOnly(2000, 1, 2)
        };
        var ops = Operations([user], [profile]);
        using var cts = new CancellationTokenSource();

        var result = await Handler(ops.Object).Handle(new() { UserId = userId }, cts.Token);

        Assert.Equal(userId, result.Id);
        Assert.Equal(user.Username, result.Username);
        Assert.Equal(user.Email, result.Email);
        Assert.Equal(user.Phone, result.Phone);
        Assert.Equal(profile.FullName, result.FullName);
        Assert.Equal(profile.AvatarUrl, result.AvatarUrl);
        Assert.Equal(profile.Address, result.Address);
        Assert.Equal(profile.DateOfBirth, result.DateOfBirth);
        ops.Verify(r => r.GetByIdAsync(userId, cts.Token), Times.Once);
        ops.Verify(r => r.GetUserProfileByUserIdAsync(userId, cts.Token), Times.Once);
    }

    [Fact(DisplayName = "UTCID02 (B) - User có nhưng chưa có profile -> Trả FullName rỗng và các trường profile null")]
    public async Task UTCID02_ProfileMissing_ReturnsUserAndProfileDefaults()
    {
        var userId = Guid.NewGuid();
        var user = NewUser(userId);
        var ops = Operations([user], []);

        var result = await Handler(ops.Object).Handle(new() { UserId = userId }, default);

        Assert.Equal(userId, result.Id);
        Assert.Equal(user.Username, result.Username);
        Assert.Equal(string.Empty, result.FullName);
        Assert.Null(result.AvatarUrl);
        Assert.Null(result.Address);
        Assert.Null(result.DateOfBirth);
        ops.Verify(r => r.GetUserProfileByUserIdAsync(userId, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact(DisplayName = "UTCID03 (B) - Username, Email và Phone của User là null -> Trả chuỗi rỗng")]
    public async Task UTCID03_NullUserContactFields_ReturnsEmptyStrings()
    {
        var userId = Guid.NewGuid();
        var user = new User { Id = userId, Username = null, Email = null, Phone = null };
        var ops = Operations([user], []);

        var result = await Handler(ops.Object).Handle(new() { UserId = userId }, default);

        Assert.Equal(string.Empty, result.Username);
        Assert.Equal(string.Empty, result.Email);
        Assert.Equal(string.Empty, result.Phone);
    }

    [Fact(DisplayName = "UTCID04 (A) - UserId không tồn tại -> KeyNotFoundException và không truy vấn profile")]
    public async Task UTCID04_UserMissing_ThrowsNotFound()
    {
        var ops = Operations([], []);

        var exception = await Assert.ThrowsAsync<KeyNotFoundException>(() =>
            Handler(ops.Object).Handle(new() { UserId = Guid.NewGuid() }, default));

        Assert.Equal("Tài khoản không tồn tại.", exception.Message);
        ops.Verify(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Once);
        ops.Verify(r => r.GetUserProfileByUserIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact(DisplayName = "UTCID05 (A) - Lỗi repository khi đọc User -> Truyền exception và không đọc profile")]
    public async Task UTCID05_UserRepositoryFailure_IsPropagated()
    {
        var ops = new Mock<IUserOperations>();
        var failure = new InvalidOperationException("User read failed");
        ops.Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(failure);

        var actual = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            Handler(ops.Object).Handle(new() { UserId = Guid.NewGuid() }, default));

        Assert.Same(failure, actual);
        ops.Verify(r => r.GetUserProfileByUserIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact(DisplayName = "UTCID06 (A) - Lỗi repository khi đọc profile -> Truyền exception")]
    public async Task UTCID06_ProfileRepositoryFailure_IsPropagated()
    {
        var userId = Guid.NewGuid();
        var ops = Operations([NewUser(userId)], []);
        var failure = new InvalidOperationException("Profile read failed");
        ops.Setup(r => r.GetUserProfileByUserIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(failure);

        var actual = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            Handler(ops.Object).Handle(new() { UserId = userId }, default));

        Assert.Same(failure, actual);
    }

    [Fact(DisplayName = "UTCID07 (N) - GET /api/profiles/me có claim UserId hợp lệ -> Gửi đúng query và trả 200")]
    public async Task UTCID07_ControllerUsesAuthenticatedClaimAndReturnsOk()
    {
        var userId = Guid.NewGuid();
        var expected = new GetMyProfileResponse { Id = userId, Username = "member" };
        GetMyProfileQuery? sent = null;
        using var cts = new CancellationTokenSource();
        var sender = new Mock<ISender>();
        sender.Setup(s => s.Send(It.IsAny<GetMyProfileQuery>(), cts.Token))
            .Callback<IRequest<GetMyProfileResponse>, CancellationToken>((request, _) => sent = (GetMyProfileQuery)request)
            .ReturnsAsync(expected);
        var controller = Controller(sender.Object, userId.ToString());

        var result = await controller.GetMyProfile(cts.Token);

        var ok = Assert.IsType<OkObjectResult>(result);
        Assert.Same(expected, ok.Value);
        Assert.NotNull(sent);
        Assert.Equal(userId, sent.UserId);
        sender.Verify(s => s.Send(It.IsAny<GetMyProfileQuery>(), cts.Token), Times.Once);
    }

    [Fact(DisplayName = "UTCID08 (A) - Claim UserId thiếu, không hợp lệ hoặc Guid.Empty -> Forbid và không gửi query")]
    public async Task UTCID08_InvalidUserIdClaim_ForbidsWithoutDispatch()
    {
        foreach (var claimValue in new string?[] { null, "not-a-guid", Guid.Empty.ToString() })
        {
            var sender = new Mock<ISender>();
            var result = await Controller(sender.Object, claimValue).GetMyProfile(default);

            Assert.IsType<ForbidResult>(result);
            sender.Verify(s => s.Send(It.IsAny<GetMyProfileQuery>(), It.IsAny<CancellationToken>()), Times.Never);
        }
    }

    [Fact(DisplayName = "UTCID09 (A) - Handler báo không tìm thấy tài khoản -> Controller trả 404")]
    public async Task UTCID09_HandlerNotFound_ReturnsNotFound()
    {
        var sender = new Mock<ISender>();
        sender.Setup(s => s.Send(It.IsAny<GetMyProfileQuery>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new KeyNotFoundException("Tài khoản không tồn tại."));

        var result = await Controller(sender.Object, Guid.NewGuid().ToString()).GetMyProfile(default);

        Assert.IsType<NotFoundResult>(result);
    }

    [Fact(DisplayName = "UTCID10 (N) - Endpoint yêu cầu đăng nhập và quyền profiles.read")]
    public void UTCID10_EndpointDeclaresAuthenticationAndReadPermission()
    {
        var authorize = Assert.Single(Attribute.GetCustomAttributes(typeof(ProfilesController)).OfType<AuthorizeAttribute>());
        var action = typeof(ProfilesController).GetMethod(nameof(ProfilesController.GetMyProfile))!;
        var permission = Assert.Single(Attribute.GetCustomAttributes(action).OfType<RequirePermissionAttribute>());

        Assert.Null(authorize.Policy);
        Assert.Equal(PermissionCatalog.Policy("profiles", "read"), permission.Policy);
    }

    private static GetMyProfileHandler Handler(IUserOperations userOperations) => new(userOperations);

    private static Mock<IUserOperations> Operations(User[] userRows, UserProfile[] profileRows)
    {
        var ops = new Mock<IUserOperations>();
        ops.Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .Returns((Guid id, CancellationToken _) =>
                Task.FromResult(userRows.FirstOrDefault(u => u.Id == id)));
        ops.Setup(r => r.GetUserProfileByUserIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .Returns((Guid userId, CancellationToken _) =>
                Task.FromResult(profileRows.FirstOrDefault(p => p.UserId == userId)));
        return ops;
    }

    private static User NewUser(Guid id) => new()
    {
        Id = id,
        Username = "member",
        Email = "member@smartpool.vn",
        Phone = "0900000000"
    };

    private static ProfilesController Controller(ISender sender, string? userIdClaim)
    {
        var identity = new ClaimsIdentity("unit-test");
        if (userIdClaim is not null) identity.AddClaim(new Claim(ClaimTypes.NameIdentifier, userIdClaim));
        return new ProfilesController(sender)
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext { User = new ClaimsPrincipal(identity) }
            }
        };
    }
}
