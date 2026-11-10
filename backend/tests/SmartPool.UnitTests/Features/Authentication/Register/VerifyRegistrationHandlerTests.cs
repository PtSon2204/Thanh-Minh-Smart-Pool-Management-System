using System.Linq.Expressions;
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

public sealed class VerifyRegistrationHandlerTests
{
    private static readonly Guid RegistrationId = Guid.Parse("f09d237b-6318-4350-ae6a-7943c40cf87e");
    private const string LeaseId = "verification-lease";
    private const string Otp = "012345";

    [Fact(DisplayName = "UTCID01 (N) - OTP đúng -> Tạo tài khoản CUSTOMER và trả 201")]
    public async Task Handle_ValidOtp_CreatesAccountAndCompletesVerification()
    {
        // Arrange
        var harness = new Harness();

        // Act
        var result = await harness.Handle();

        // Assert
        var response = Assert.IsType<RegisterResponse>(Assert.IsType<ObjectResult>(result).Value);
        Assert.Equal(StatusCodes.Status201Created, Assert.IsType<ObjectResult>(result).StatusCode);
        var user = Assert.Single(harness.CreatedUsers);
        var profile = Assert.Single(harness.CreatedProfiles);
        var notification = Assert.Single(harness.CreatedNotifications);
        Assert.Equal(user.Id, response.Id);
        Assert.Equal("customer@example.com", response.Email);
        Assert.Equal("0912345678", response.Phone);
        Assert.Equal("Nguyễn Văn A", response.FullName);
        Assert.Equal("hashed-password", user.PasswordHash);
        Assert.Equal(harness.CustomerRole.Id, user.RoleId);
        Assert.Equal("Active", user.Status);
        Assert.False(user.IsDeleted);
        Assert.Null(user.Username);
        Assert.Same(profile, user.UserProfile);
        Assert.Same(user, profile.User);
        Assert.Equal(user.Id, profile.UserId);
        Assert.Equal("Nguyễn Văn A", profile.FullName);
        Assert.Equal(new DateOnly(2000, 1, 2), profile.DateOfBirth);
        Assert.Equal("Hà Nội", profile.Address);
        Assert.Equal(harness.Admin.Id, notification.UserId);
        Assert.Equal("CUSTOMER", notification.Type);
        Assert.Contains("customer@example.com", notification.Content);
        harness.OtpStore.Verify(store => store.TryBeginVerificationAsync(
            RegistrationId, harness.Codes.ComputeDigest(RegistrationId, Otp), CancellationToken.None), Times.Once);
        harness.UnitOfWork.Verify(unit => unit.SaveChangesAsync(CancellationToken.None), Times.Once);
        harness.Notifications.Verify(service => service.SendToAllAsync(
            "ReceiveNotification", It.IsAny<object>(), CancellationToken.None), Times.Once);
        harness.OtpStore.Verify(store => store.CompleteVerificationAsync(
            RegistrationId, LeaseId, It.IsAny<CancellationToken>()), Times.Once);
        harness.OtpStore.Verify(store => store.ReleaseVerificationAsync(
            It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact(DisplayName = "UTCID02 (B) - Không có tài khoản admin -> Notification được gắn cho user mới")]
    public async Task Handle_AdminMissing_AssignsNotificationToNewUser()
    {
        var harness = new Harness();
        harness.Users.Setup(repository => repository.FirstOrDefaultAsync(
                It.IsAny<Expression<Func<User, bool>>>(), CancellationToken.None))
            .ReturnsAsync((User?)null);

        Assert.Equal(201, Assert.IsType<ObjectResult>(await harness.Handle()).StatusCode);

        Assert.Equal(Assert.Single(harness.CreatedUsers).Id, Assert.Single(harness.CreatedNotifications).UserId);
        harness.UnitOfWork.Verify(unit => unit.SaveChangesAsync(CancellationToken.None), Times.Once);
    }

    [Fact(DisplayName = "UTCID03 (A) - OTP sai -> Trả 400 INVALID_OTP và không ghi DB")]
    public Task Handle_InvalidOtp_ReturnsBadRequestWithoutWrites() =>
        AssertChallengeFailure(RegistrationOtpStoreStatus.InvalidOtp, 400, "INVALID_OTP");

    [Fact(DisplayName = "UTCID04 (A) - OTP hết hạn -> Trả 410 OTP_EXPIRED và không ghi DB")]
    public Task Handle_ExpiredOtp_ReturnsGoneWithoutWrites() =>
        AssertChallengeFailure(RegistrationOtpStoreStatus.OtpExpired, 410, "OTP_EXPIRED");

    [Fact(DisplayName = "UTCID05 (A) - Phiên đăng ký không còn -> Trả 410 REGISTRATION_UNAVAILABLE và không ghi DB")]
    public Task Handle_RegistrationUnavailable_ReturnsGoneWithoutWrites() =>
        AssertChallengeFailure(RegistrationOtpStoreStatus.Unavailable, 410, "REGISTRATION_UNAVAILABLE");

    [Fact(DisplayName = "UTCID06 (A) - Vượt số lần nhập OTP -> Trả 429 VERIFY_ATTEMPTS_EXCEEDED và không ghi DB")]
    public Task Handle_AttemptsExceeded_ReturnsRateLimitWithoutWrites() =>
        AssertChallengeFailure(RegistrationOtpStoreStatus.AttemptsExceeded, 429, "VERIFY_ATTEMPTS_EXCEEDED");

    [Fact(DisplayName = "UTCID07 (A) - Phiên đã xác minh -> Trả 409 ALREADY_VERIFIED và không ghi DB")]
    public Task Handle_AlreadyVerified_ReturnsConflictWithoutWrites() =>
        AssertChallengeFailure(RegistrationOtpStoreStatus.AlreadyVerified, 409, "ALREADY_VERIFIED");

    [Fact(DisplayName = "UTCID08 (A) - Email bị trùng khi xác minh -> Trả 409 EMAIL_TAKEN, không tạo tài khoản")]
    public async Task Handle_ConflictAfterOtp_ReturnsConflictAndReleasesLease()
    {
        var harness = new Harness();
        harness.Users.Setup(repository => repository.ExistsAsync(
                It.IsAny<Expression<Func<User, bool>>>(), CancellationToken.None))
            .ReturnsAsync((Expression<Func<User, bool>> predicate, CancellationToken _) =>
                predicate.Compile()(new User { Email = "customer@example.com" }));

        AssertProblem(await harness.Handle(), 409, "EMAIL_TAKEN");

        harness.AssertLeaseReleasedWithoutWrites();
    }

    [Fact(DisplayName = "UTCID09 (A) - Thiếu role CUSTOMER khi xác minh -> Trả 500, không tạo tài khoản")]
    public async Task Handle_CustomerRoleMissing_ReturnsConfigurationErrorAndReleasesLease()
    {
        var harness = new Harness();
        harness.Roles.Setup(repository => repository.FirstOrDefaultAsync(
                It.IsAny<Expression<Func<Role, bool>>>(), CancellationToken.None))
            .ReturnsAsync((Role?)null);

        AssertProblem(await harness.Handle(), 500, "CUSTOMER_ROLE_NOT_CONFIGURED");

        harness.AssertLeaseReleasedWithoutWrites();
    }

    [Fact(DisplayName = "UTCID10 (A) - Lưu tài khoản thất bại -> Trả 503 REGISTRATION_SAVE_FAILED")]
    public async Task Handle_SaveFailure_ReturnsServiceUnavailableAndReleasesLease()
    {
        var harness = new Harness();
        harness.UnitOfWork.Setup(unit => unit.SaveChangesAsync(CancellationToken.None))
            .ThrowsAsync(new InvalidOperationException("Database unavailable"));

        AssertProblem(await harness.Handle(), 503, "REGISTRATION_SAVE_FAILED");

        harness.UnitOfWork.Verify(unit => unit.SaveChangesAsync(CancellationToken.None), Times.Once);
        harness.OtpStore.Verify(store => store.ReleaseVerificationAsync(
            RegistrationId, LeaseId, It.IsAny<CancellationToken>()), Times.Once);
        harness.OtpStore.Verify(store => store.CompleteVerificationAsync(
            It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
        harness.Notifications.Verify(service => service.SendToAllAsync(
            It.IsAny<string>(), It.IsAny<object>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    private static async Task AssertChallengeFailure(RegistrationOtpStoreStatus status, int httpStatus, string code)
    {
        var harness = new Harness();
        harness.OtpStore.Setup(store => store.TryBeginVerificationAsync(
                RegistrationId, It.IsAny<string>(), CancellationToken.None))
            .ReturnsAsync(new RegistrationOtpStoreResult(status));

        AssertProblem(await harness.Handle(), httpStatus, code);

        harness.AssertNoWritesOrLeaseCompletion();
    }

    private static void AssertProblem(IActionResult result, int status, string code)
    {
        var objectResult = Assert.IsType<ObjectResult>(result);
        Assert.Equal(status, objectResult.StatusCode);
        var problem = Assert.IsType<ProblemDetails>(objectResult.Value);
        Assert.Equal(status, problem.Status);
        Assert.Equal(code, problem.Extensions["code"]);
    }

    private static VerifiedRegistration Registration() => new(
        string.Empty, "customer@example.com", "0912345678", "hashed-password", "Nguyễn Văn A",
        new DateOnly(2000, 1, 2), "Hà Nội");

    private sealed class Harness
    {
        public Mock<IRepository<User>> Users { get; } = new();
        public Mock<IRepository<UserProfile>> Profiles { get; } = new();
        public Mock<IRepository<Role>> Roles { get; } = new();
        public Mock<IRepository<Notification>> NotificationRepository { get; } = new();
        public Mock<INotificationService> Notifications { get; } = new();
        public Mock<IUnitOfWork> UnitOfWork { get; } = new();
        public Mock<IRegistrationOtpStore> OtpStore { get; } = new();
        public DefaultHttpContext HttpContext { get; } = new();
        public RegistrationOtpCodeService Codes { get; } = new(new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["RegistrationOtp:HmacKey"] = Convert.ToBase64String(new byte[32])
            }).Build());
        public Role CustomerRole { get; } = new() { Id = Guid.NewGuid(), Name = "CUSTOMER" };
        public User Admin { get; } = new() { Id = Guid.NewGuid(), Username = "admin" };
        public List<User> CreatedUsers { get; } = [];
        public List<UserProfile> CreatedProfiles { get; } = [];
        public List<Notification> CreatedNotifications { get; } = [];

        public Harness()
        {
            OtpStore.Setup(store => store.TryBeginVerificationAsync(
                    RegistrationId, It.IsAny<string>(), CancellationToken.None))
                .ReturnsAsync(new RegistrationOtpStoreResult(RegistrationOtpStoreStatus.Success,
                    LeaseId: LeaseId, Registration: Registration()));
            OtpStore.Setup(store => store.CompleteVerificationAsync(
                    RegistrationId, LeaseId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(true);
            OtpStore.Setup(store => store.ReleaseVerificationAsync(
                    RegistrationId, LeaseId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(true);
            Users.Setup(repository => repository.ExistsAsync(
                    It.IsAny<Expression<Func<User, bool>>>(), CancellationToken.None))
                .ReturnsAsync(false);
            Users.Setup(repository => repository.FirstOrDefaultAsync(
                    It.IsAny<Expression<Func<User, bool>>>(), CancellationToken.None))
                .ReturnsAsync(Admin);
            Users.Setup(repository => repository.AddAsync(It.IsAny<User>(), CancellationToken.None))
                .Callback<User, CancellationToken>((user, _) => CreatedUsers.Add(user))
                .Returns(Task.CompletedTask);
            Profiles.Setup(repository => repository.AddAsync(It.IsAny<UserProfile>(), CancellationToken.None))
                .Callback<UserProfile, CancellationToken>((profile, _) => CreatedProfiles.Add(profile))
                .Returns(Task.CompletedTask);
            NotificationRepository.Setup(repository => repository.AddAsync(
                    It.IsAny<Notification>(), CancellationToken.None))
                .Callback<Notification, CancellationToken>((notification, _) => CreatedNotifications.Add(notification))
                .Returns(Task.CompletedTask);
            Roles.Setup(repository => repository.FirstOrDefaultAsync(
                    It.IsAny<Expression<Func<Role, bool>>>(), CancellationToken.None))
                .ReturnsAsync((Expression<Func<Role, bool>> predicate, CancellationToken _) =>
                    predicate.Compile()(CustomerRole) ? CustomerRole : null);
            UnitOfWork.Setup(unit => unit.SaveChangesAsync(CancellationToken.None)).ReturnsAsync(1);
            Notifications.Setup(service => service.SendToAllAsync(
                    "ReceiveNotification", It.IsAny<object>(), CancellationToken.None))
                .Returns(Task.CompletedTask);
        }

        public Task<IActionResult> Handle()
        {
            var handler = new VerifyRegistrationHandler(Users.Object, Profiles.Object, Roles.Object,
                NotificationRepository.Object, Notifications.Object, UnitOfWork.Object, OtpStore.Object,
                Codes, new HttpContextAccessor { HttpContext = HttpContext },
                new VerifyRegistrationValidator(), NullLogger<VerifyRegistrationHandler>.Instance);
            return handler.Handle(new VerifyRegistrationCommand
            {
                RegistrationId = RegistrationId, Otp = Otp
            }, CancellationToken.None);
        }

        public void AssertNoWritesOrLeaseCompletion()
        {
            Assert.Empty(CreatedUsers);
            Assert.Empty(CreatedProfiles);
            Assert.Empty(CreatedNotifications);
            UnitOfWork.Verify(unit => unit.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
            OtpStore.Verify(store => store.CompleteVerificationAsync(
                It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
            Notifications.Verify(service => service.SendToAllAsync(
                It.IsAny<string>(), It.IsAny<object>(), It.IsAny<CancellationToken>()), Times.Never);
        }

        public void AssertLeaseReleasedWithoutWrites()
        {
            AssertNoWritesOrLeaseCompletion();
            OtpStore.Verify(store => store.ReleaseVerificationAsync(
                RegistrationId, LeaseId, It.IsAny<CancellationToken>()), Times.Once);
        }
    }
}
