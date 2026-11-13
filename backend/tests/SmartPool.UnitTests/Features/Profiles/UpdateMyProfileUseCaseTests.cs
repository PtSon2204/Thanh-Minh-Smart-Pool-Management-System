using FluentValidation;
using FluentValidation.Results;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Moq;
using SmartPool.Application.Features.Profiles.Commands.UpdateMyProfile;
using SmartPool.Application.Interfaces.Repositories;
using SmartPool.Domain.Entities;
using System.Linq.Expressions;

namespace SmartPool.UnitTests.Features.Profiles;

public sealed class UpdateMyProfileUseCaseTests
{
    private readonly UpdateMyProfileValidator _validator = new();

    [Fact(DisplayName = "UTCID01 (N) - Validator với thông tin hợp lệ -> Hợp lệ")]
    public void UTCID01_ValidValues_PassesValidation()
    {
        var result = _validator.Validate(Command("Nguyen Van A", "Ha Noi", DateOnly.FromDateTime(DateTime.UtcNow)));
        Assert.True(result.IsValid);
    }

    [Fact(DisplayName = "UTCID02 (B) - FullName dài 255 ký tự, Address dài 500 ký tự và ngày sinh hôm nay -> Hợp lệ")]
    public void UTCID02_MaximumLengthsAndToday_PassValidation()
    {
        var result = _validator.Validate(Command(new string('A', 255), new string('B', 500), DateOnly.FromDateTime(DateTime.UtcNow)));
        Assert.True(result.IsValid);
    }

    [Fact(DisplayName = "UTCID03 (A) - FullName rỗng -> Lỗi FullName")]
    public void UTCID03_EmptyFullName_FailsValidation()
    {
        var result = _validator.Validate(Command(string.Empty, null, null));
        Assert.Contains(result.Errors, e => e.PropertyName == nameof(UpdateMyProfileCommand.FullName) && e.ErrorMessage == "Họ tên không được để trống.");
    }

    [Fact(DisplayName = "UTCID04 (A) - FullName dài 256 ký tự -> Lỗi giới hạn FullName")]
    public void UTCID04_FullNameOver255_FailsValidation()
    {
        var result = _validator.Validate(Command(new string('A', 256), null, null));
        Assert.Contains(result.Errors, e => e.PropertyName == nameof(UpdateMyProfileCommand.FullName) && e.ErrorMessage == "Họ tên không được vượt quá 255 ký tự.");
    }

    [Fact(DisplayName = "UTCID05 (A) - Address dài 501 ký tự -> Lỗi giới hạn Address")]
    public void UTCID05_AddressOver500_FailsValidation()
    {
        var result = _validator.Validate(Command("Nguyen Van A", new string('B', 501), null));
        Assert.Contains(result.Errors, e => e.PropertyName == nameof(UpdateMyProfileCommand.Address) && e.ErrorMessage == "Địa chỉ không được vượt quá 500 ký tự.");
    }

    [Fact(DisplayName = "UTCID06 (A) - DateOfBirth là ngày mai -> Lỗi ngày sinh")]
    public void UTCID06_FutureDateOfBirth_FailsValidation()
    {
        var result = _validator.Validate(Command("Nguyen Van A", null, DateOnly.FromDateTime(DateTime.UtcNow).AddDays(1)));
        Assert.Contains(result.Errors, e => e.PropertyName == nameof(UpdateMyProfileCommand.DateOfBirth) && e.ErrorMessage == "Ngày sinh không hợp lệ.");
    }

    [Fact(DisplayName = "UTCID07 (N) - Address và DateOfBirth null -> Hợp lệ")]
    public void UTCID07_NullOptionalFields_PassValidation()
    {
        var result = _validator.Validate(Command("Nguyen Van A", null, null));
        Assert.True(result.IsValid);
    }

    [Fact(DisplayName = "UTCID08 (N) - Profile đã tồn tại -> Cập nhật profile và lưu một lần")]
    public async Task UTCID08_ExistingProfile_UpdatesAndSaves()
    {
        var userId = Guid.NewGuid();
        var profile = new UserProfile { UserId = userId, FullName = "Old", Address = "Old address", DateOfBirth = new DateOnly(1990, 1, 1) };
        var ops = Operations(profile, null);
        using var cts = new CancellationTokenSource();
        var command = Command("New name", "New address", new DateOnly(2000, 2, 3), userId);

        var result = await Handler(ops.Object).Handle(command, cts.Token);

        Assert.IsType<OkObjectResult>(result);
        Assert.Equal("New name", profile.FullName);
        Assert.Equal("New address", profile.Address);
        Assert.Equal(new DateOnly(2000, 2, 3), profile.DateOfBirth);
        Assert.NotNull(profile.UpdatedAt);
        Assert.InRange(profile.UpdatedAt.Value, DateTime.UtcNow.AddMinutes(-1), DateTime.UtcNow.AddMinutes(1));
        ops.Verify(r => r.UpdateUserProfileAsync(profile, cts.Token), Times.Once);
        ops.Verify(r => r.AddUserProfileAsync(It.IsAny<UserProfile>(), It.IsAny<CancellationToken>()), Times.Never);
        ops.Verify(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact(DisplayName = "UTCID09 (N) - Chưa có profile nhưng User tồn tại -> Tạo profile và lưu một lần")]
    public async Task UTCID09_MissingProfileAndExistingUser_CreatesAndSaves()
    {
        var userId = Guid.NewGuid();
        var user = new User { Id = userId };
        var ops = Operations(null, user);
        UserProfile? added = null;
        ops.Setup(r => r.AddUserProfileAsync(It.IsAny<UserProfile>(), It.IsAny<CancellationToken>()))
            .Callback<UserProfile, CancellationToken>((entity, _) => added = entity).Returns(Task.CompletedTask);

        var result = await Handler(ops.Object)
            .Handle(Command("New name", "New address", new DateOnly(2000, 2, 3), userId), default);

        Assert.IsType<OkObjectResult>(result);
        Assert.NotNull(added);
        Assert.Equal(userId, added.UserId);
        Assert.Equal("New name", added.FullName);
        Assert.Equal("New address", added.Address);
        Assert.Equal(new DateOnly(2000, 2, 3), added.DateOfBirth);
        ops.Verify(r => r.AddUserProfileAsync(It.IsAny<UserProfile>(), It.IsAny<CancellationToken>()), Times.Once);
        ops.Verify(r => r.UpdateUserProfileAsync(It.IsAny<UserProfile>(), It.IsAny<CancellationToken>()), Times.Never);
        ops.Verify(r => r.GetByIdAsync(userId, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact(DisplayName = "UTCID10 (A) - Chưa có profile và User không tồn tại -> 404, không ghi dữ liệu")]
    public async Task UTCID10_MissingProfileAndUser_ReturnsNotFoundWithoutWrites()
    {
        var ops = Operations(null, null);

        var result = await Handler(ops.Object)
            .Handle(Command("New name", null, null, Guid.NewGuid()), default);

        var notFound = Assert.IsType<NotFoundObjectResult>(result);
        Assert.Equal(StatusCodes.Status404NotFound, notFound.StatusCode);
        ops.Verify(r => r.AddUserProfileAsync(It.IsAny<UserProfile>(), It.IsAny<CancellationToken>()), Times.Never);
        ops.Verify(r => r.UpdateUserProfileAsync(It.IsAny<UserProfile>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact(DisplayName = "UTCID11 (A) - Handler nhận FullName rỗng -> 400 và không gọi repository")]
    public async Task UTCID11_InvalidCommand_ReturnsBadRequestWithoutRepositoryCalls()
    {
        var ops = Operations(null, null);

        var result = await Handler(ops.Object)
            .Handle(Command(string.Empty, null, null, Guid.NewGuid()), default);

        var badRequest = Assert.IsType<BadRequestObjectResult>(result);
        var details = Assert.IsType<ValidationProblemDetails>(badRequest.Value);
        Assert.Equal(StatusCodes.Status400BadRequest, details.Status);
        Assert.Contains("fullName", details.Errors.Keys);
        ops.Verify(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never);
        ops.Verify(r => r.GetUserProfileByUserIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact(DisplayName = "UTCID12 (A) - SaveChangesAsync ném lỗi -> Handler truyền tiếp exception")]
    public async Task UTCID12_SaveFailure_IsPropagated()
    {
        var profile = new UserProfile { UserId = Guid.NewGuid(), FullName = "Old" };
        var ops = Operations(profile, null);
        var failure = new InvalidOperationException("Save failed");
        ops.Setup(u => u.UpdateUserProfileAsync(It.IsAny<UserProfile>(), It.IsAny<CancellationToken>())).ThrowsAsync(failure);

        var actual = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            Handler(ops.Object)
                .Handle(Command("New name", null, null, profile.UserId), default));

        Assert.Same(failure, actual);
        ops.Verify(r => r.UpdateUserProfileAsync(profile, It.IsAny<CancellationToken>()), Times.Once);
    }

    private static UpdateMyProfileCommand Command(string fullName, string? address, DateOnly? dateOfBirth, Guid? userId = null) => new()
    {
        UserId = userId ?? Guid.NewGuid(), FullName = fullName, Address = address, DateOfBirth = dateOfBirth
    };

    private static UpdateMyProfileHandler Handler(IUserOperations userOperations) =>
        new(userOperations, new UpdateMyProfileValidator());

    private static Mock<IUserOperations> Operations(UserProfile? profile, User? user)
    {
        var ops = new Mock<IUserOperations>();
        ops.Setup(r => r.GetUserProfileByUserIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(profile);
        ops.Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(user);
        ops.Setup(r => r.AddUserProfileAsync(It.IsAny<UserProfile>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        ops.Setup(r => r.UpdateUserProfileAsync(It.IsAny<UserProfile>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        return ops;
    }
}
