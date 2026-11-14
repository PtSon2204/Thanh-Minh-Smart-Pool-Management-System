using System.Net;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using SmartPool.Application.Features.Authentication.Commands.Register;
using SmartPool.Application.Features.Authentication.Commands.Register.VerifyOTP;
using SmartPool.Application.Features.Authentication.Models;
using SmartPool.Application.Interfaces.Repositories;
using SmartPool.Application.Interfaces.Services;
using SmartPool.Domain.Entities;

namespace SmartPool.UnitTests.Features.Authentication.Register;

public sealed class RegisterHandlerTests
{
    private static readonly DateTimeOffset OtpExpires = new(2026, 11, 9, 12, 5, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset RegistrationExpires = OtpExpires.AddMinutes(25);
    private static readonly DateTimeOffset ResendAvailable = OtpExpires.AddMinutes(-4);

    [Fact(DisplayName = "UTCID01 (N) - Dữ liệu hợp lệ -> Tạo phiên OTP, gửi email và trả 202; không tạo User")]
    public async Task Handle_ValidRegistration_ReturnsPendingResponseWithoutCreatingUser()
    {
        // Arrange
        var harness = new Harness();
        var request = new RegisterCommand
        {
            Email = "  Customer@Example.com  ",
            Phone = " +84912345678 ",
            Password = "Password123"
        };
        PendingRegistration? pending = null;
        harness.OtpStore.Setup(store => store.TryStartAsync(
                It.IsAny<PendingRegistration>(), "127.0.0.1", CancellationToken.None))
            .Callback<PendingRegistration, string, CancellationToken>((value, _, _) => pending = value)
            .ReturnsAsync(SuccessfulStart());

        // Act
        var result = await harness.Handle(request);

        // Assert
        var response = Assert.IsType<RegisterPendingResponse>(Assert.IsType<ObjectResult>(result).Value);
        Assert.Equal(StatusCodes.Status202Accepted, Assert.IsType<ObjectResult>(result).StatusCode);
        Assert.NotEqual(Guid.Empty, response.RegistrationId);
        Assert.Equal(OtpExpires, response.OtpExpiresAtUtc);
        Assert.Equal(RegistrationExpires, response.RegistrationExpiresAtUtc);
        Assert.Equal(ResendAvailable, response.ResendAvailableAtUtc);
        Assert.NotNull(pending);
        Assert.Equal(response.RegistrationId, pending.RegistrationId);
        Assert.Equal("customer@example.com", pending.Email);
        Assert.Equal("0912345678", pending.Phone);
        Assert.Equal(string.Empty, pending.Username);
        Assert.Equal(pending.Email, pending.FullName);
        Assert.Equal("hashed-password", pending.PasswordHash);
        Assert.False(string.IsNullOrWhiteSpace(pending.OtpDigest));
        harness.OtpStore.Verify(store => store.TryConsumeStartQuotaAsync(
            "customer@example.com", "127.0.0.1", CancellationToken.None), Times.Once);
        harness.PasswordHasher.Verify(hasher => hasher.HashPassword(
            It.IsAny<User>(), request.Password), Times.Once);
        harness.Email.Verify(service => service.SendAsync(
            "customer@example.com", It.IsAny<string>(),
            It.Is<string>(body => body.Contains("Mã xác minh") && body.Contains("<strong>")),
            CancellationToken.None), Times.Once);
        harness.Users.Verify(repository => repository.CreateUserWithProfileAsync(
            It.IsAny<User>(), It.IsAny<UserProfile>(), It.IsAny<Notification>(), It.IsAny<CancellationToken>()), Times.Never);
        harness.OtpStore.Verify(store => store.CancelStartAsync(
            It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact(DisplayName = "UTCID02 (A) - Email đã tồn tại -> Trả 409 EMAIL_TAKEN, không lưu phiên hoặc gửi email")]
    public async Task Handle_DuplicateEmail_ReturnsConflictWithoutStartingOtp()
    {
        var harness = new Harness();
        harness.Users.Setup(repository => repository.ExistsByEmailAsync("customer@example.com", CancellationToken.None))
            .ReturnsAsync(true);

        AssertProblem(await harness.Handle(new RegisterCommand
        {
            Email = " Customer@Example.com ", Phone = "0912345678", Password = "Password123"
        }), 409, "EMAIL_TAKEN");

        harness.AssertNoOtpStartOrEmail();
        harness.Users.Verify(repository => repository.GetRoleByNameAsync(
            It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact(DisplayName = "UTCID03 (A) - Số điện thoại đã tồn tại dạng +84 -> Trả 409 PHONE_TAKEN, không lưu phiên hoặc gửi email")]
    public async Task Handle_DuplicatePhone_ReturnsConflictWithoutStartingOtp()
    {
        var harness = new Harness();
        harness.Users.Setup(repository => repository.ExistsByPhoneAsync(
                "0912345678", "+84912345678", CancellationToken.None))
            .ReturnsAsync(true);

        AssertProblem(await harness.Handle(), 409, "PHONE_TAKEN");

        harness.AssertNoOtpStartOrEmail();
        harness.Users.Verify(repository => repository.GetRoleByNameAsync(
            It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact(DisplayName = "UTCID04 (A) - Thiếu role CUSTOMER -> Trả 500 CUSTOMER_ROLE_NOT_CONFIGURED")]
    public async Task Handle_CustomerRoleMissing_ReturnsConfigurationError()
    {
        var harness = new Harness();
        harness.Users.Setup(repository => repository.GetRoleByNameAsync(
                "CUSTOMER", CancellationToken.None))
            .ReturnsAsync((Role?)null);

        AssertProblem(await harness.Handle(), 500, "CUSTOMER_ROLE_NOT_CONFIGURED");

        harness.AssertNoOtpStartOrEmail();
        harness.PasswordHasher.VerifyNoOtherCalls();
    }

    [Fact(DisplayName = "UTCID05 (A) - Email đang có phiên đăng ký chờ -> Trả 409 PENDING_REGISTRATION")]
    public async Task Handle_PendingRegistration_ReturnsConflictWithoutEmail()
    {
        var harness = new Harness();
        harness.OtpStore.Setup(store => store.TryStartAsync(
                It.IsAny<PendingRegistration>(), It.IsAny<string>(), CancellationToken.None))
            .ReturnsAsync(new RegistrationOtpStoreResult(RegistrationOtpStoreStatus.PendingRegistration));

        AssertProblem(await harness.Handle(), 409, "PENDING_REGISTRATION");

        harness.Email.VerifyNoOtherCalls();
        harness.OtpStore.Verify(store => store.CancelStartAsync(
            It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
        harness.Users.Verify(repository => repository.CreateUserWithProfileAsync(
            It.IsAny<User>(), It.IsAny<UserProfile>(), It.IsAny<Notification>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact(DisplayName = "UTCID06 (A) - Gửi email OTP lỗi -> Hủy phiên và trả 503 EMAIL_SEND_FAILED")]
    public async Task Handle_EmailSendFailure_CancelsPendingSession()
    {
        var harness = new Harness();
        Guid registrationId = Guid.Empty;
        harness.OtpStore.Setup(store => store.TryStartAsync(
                It.IsAny<PendingRegistration>(), It.IsAny<string>(), CancellationToken.None))
            .Callback<PendingRegistration, string, CancellationToken>((pending, _, _) => registrationId = pending.RegistrationId)
            .ReturnsAsync(SuccessfulStart());
        harness.Email.Setup(service => service.SendAsync(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), CancellationToken.None))
            .ThrowsAsync(new InvalidOperationException("SMTP failure"));

        AssertProblem(await harness.Handle(), 503, "EMAIL_SEND_FAILED");

        Assert.NotEqual(Guid.Empty, registrationId);
        harness.Email.Verify(service => service.SendAsync(
            "customer@example.com", It.IsAny<string>(), It.IsAny<string>(), CancellationToken.None), Times.Once);
        harness.OtpStore.Verify(store => store.CancelStartAsync(
            registrationId, "customer@example.com", It.IsAny<CancellationToken>()), Times.Once);
        harness.Users.Verify(repository => repository.CreateUserWithProfileAsync(
            It.IsAny<User>(), It.IsAny<UserProfile>(), It.IsAny<Notification>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    private static RegistrationOtpStoreResult SuccessfulStart() => new(
        RegistrationOtpStoreStatus.Success, OtpExpires, RegistrationExpires, ResendAvailable);

    private static void AssertProblem(IActionResult result, int status, string code)
    {
        var objectResult = Assert.IsType<ObjectResult>(result);
        Assert.Equal(status, objectResult.StatusCode);
        var problem = Assert.IsType<ProblemDetails>(objectResult.Value);
        Assert.Equal(status, problem.Status);
        Assert.Equal(code, problem.Extensions["code"]);
    }

    private sealed class Harness
    {
        public Mock<IUserOperations> Users { get; } = new();
        public Mock<IPasswordHasher> PasswordHasher { get; } = new();
        public Mock<IRegistrationOtpStore> OtpStore { get; } = new();
        public Mock<IEmailService> Email { get; } = new();
        public DefaultHttpContext HttpContext { get; } = new();

        public Harness()
        {
            HttpContext.Connection.RemoteIpAddress = IPAddress.Loopback;
            var role = new Role { Id = Guid.NewGuid(), Name = "CUSTOMER", IsDeleted = false };
            Users.Setup(repository => repository.GetRoleByNameAsync(
                    "CUSTOMER", CancellationToken.None))
                .ReturnsAsync(role);
            PasswordHasher.Setup(hasher => hasher.HashPassword(It.IsAny<User>(), It.IsAny<string>()))
                .Returns("hashed-password");
            OtpStore.Setup(store => store.TryConsumeStartQuotaAsync(
                    It.IsAny<string>(), It.IsAny<string>(), CancellationToken.None))
                .ReturnsAsync(new RegistrationOtpStoreResult(RegistrationOtpStoreStatus.Success));
            OtpStore.Setup(store => store.TryStartAsync(
                    It.IsAny<PendingRegistration>(), It.IsAny<string>(), CancellationToken.None))
                .ReturnsAsync(SuccessfulStart());
            OtpStore.Setup(store => store.CancelStartAsync(
                    It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(true);
            Email.Setup(service => service.SendAsync(
                    It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), CancellationToken.None))
                .Returns(Task.CompletedTask);
        }

        public Task<IActionResult> Handle(RegisterCommand? request = null)
        {
            var codes = new RegistrationOtpCodeService(new ConfigurationBuilder()
                .AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["RegistrationOtp:HmacKey"] = Convert.ToBase64String(new byte[32])
                }).Build());
            var handler = new RegisterHandler(Users.Object, PasswordHasher.Object,
                OtpStore.Object, codes, Email.Object,
                new HttpContextAccessor { HttpContext = HttpContext }, new RegisterValidator(),
                NullLogger<RegisterHandler>.Instance);
            return handler.Handle(request ?? new RegisterCommand
            {
                Email = "customer@example.com", Phone = "0912345678", Password = "Password123"
            }, CancellationToken.None);
        }

        public void AssertNoOtpStartOrEmail()
        {
            OtpStore.Verify(store => store.TryStartAsync(
                It.IsAny<PendingRegistration>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
            Email.VerifyNoOtherCalls();
            Users.Verify(repository => repository.CreateUserWithProfileAsync(
                It.IsAny<User>(), It.IsAny<UserProfile>(), It.IsAny<Notification>(), It.IsAny<CancellationToken>()), Times.Never);
        }
    }
}
