using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using SmartPool.Application.Features.Authentication.Commands.Register;
using SmartPool.Application.Features.Authentication.Commands.Register.ResendOTP;
using SmartPool.Application.Features.Authentication.Commands.Register.VerifyOTP;
using SmartPool.Application.Features.Authentication.Models;
using SmartPool.Application.Interfaces.Services;

namespace SmartPool.UnitTests.Features.Authentication.Register;

public sealed class ResendRegistrationHandlerTests
{
    private static readonly Guid RegistrationId = Guid.Parse("e3808a36-535e-44a6-b3d5-934586e91039");
    private const string LeaseId = "resend-lease";
    private const string EmailAddress = "customer@example.com";
    private static readonly DateTimeOffset OtpExpires = new(2026, 11, 9, 12, 5, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset RegistrationExpires = OtpExpires.AddMinutes(25);
    private static readonly DateTimeOffset ResendAvailable = OtpExpires.AddMinutes(-4);

    [Fact(DisplayName = "UTCID01 (N) - Gửi lại OTP thành công -> Gửi email và trả 202 cùng ba thời hạn")]
    public async Task Handle_ValidResend_SendsEmailThenCompletesDigestAndReturnsAccepted()
    {
        // Arrange
        var harness = new Harness();
        var calls = new List<string>();
        string? emailBody = null;
        string? completedDigest = null;
        harness.Email.Setup(service => service.SendAsync(
                EmailAddress, It.IsAny<string>(), It.IsAny<string>(), CancellationToken.None))
            .Callback<string, string, string, CancellationToken>((_, _, body, _) =>
            {
                calls.Add("email");
                emailBody = body;
            })
            .Returns(Task.CompletedTask);
        harness.OtpStore.Setup(store => store.CompleteResendAsync(
                RegistrationId, LeaseId, It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Callback<Guid, string, string, CancellationToken>((_, _, digest, _) =>
            {
                calls.Add("complete");
                completedDigest = digest;
            })
            .ReturnsAsync(SuccessfulCompletion());

        // Act
        var result = await harness.Handle();

        // Assert
        var objectResult = Assert.IsType<ObjectResult>(result);
        Assert.Equal(202, objectResult.StatusCode);
        var response = Assert.IsType<ResendRegistrationResponse>(objectResult.Value);
        Assert.Equal(OtpExpires, response.OtpExpiresAtUtc);
        Assert.Equal(RegistrationExpires, response.RegistrationExpiresAtUtc);
        Assert.Equal(ResendAvailable, response.ResendAvailableAtUtc);
        Assert.Equal(new[] { "email", "complete" }, calls);
        Assert.NotNull(emailBody);
        var otpMatch = Regex.Match(emailBody, @"<strong>([0-9]{6})</strong>");
        Assert.True(otpMatch.Success);
        Assert.Equal(harness.Codes.ComputeDigest(RegistrationId, otpMatch.Groups[1].Value), completedDigest);
        Assert.NotEqual(otpMatch.Groups[1].Value, completedDigest);
        harness.OtpStore.Verify(store => store.TryBeginResendAsync(RegistrationId, CancellationToken.None), Times.Once);
        harness.Email.Verify(service => service.SendAsync(
            EmailAddress, It.IsAny<string>(), It.IsAny<string>(), CancellationToken.None), Times.Once);
        harness.OtpStore.Verify(store => store.CompleteResendAsync(
            RegistrationId, LeaseId, completedDigest!, It.IsAny<CancellationToken>()), Times.Once);
        harness.OtpStore.Verify(store => store.CancelResendAsync(
            It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact(DisplayName = "UTCID02 (A) - Phiên đăng ký không còn -> Trả 410 REGISTRATION_UNAVAILABLE, không gửi email")]
    public Task Handle_RegistrationUnavailable_ReturnsGoneWithoutSending() =>
        AssertBeginFailure(RegistrationOtpStoreStatus.Unavailable, 410, "REGISTRATION_UNAVAILABLE");

    [Fact(DisplayName = "UTCID03 (B) - Chưa hết thời gian chờ gửi lại -> Trả 429 RESEND_COOLDOWN và Retry-After")]
    public async Task Handle_ResendCooldown_ReturnsRetryAfterWithoutSending()
    {
        var harness = new Harness();
        harness.OtpStore.Setup(store => store.TryBeginResendAsync(RegistrationId, CancellationToken.None))
            .ReturnsAsync(new RegistrationOtpStoreResult(
                RegistrationOtpStoreStatus.ResendCooldown, RetryAfter: TimeSpan.FromSeconds(1.2)));

        AssertProblem(await harness.Handle(), 429, "RESEND_COOLDOWN");

        Assert.Equal("2", harness.HttpContext.Response.Headers.RetryAfter.ToString());
        harness.AssertNoEmailOrCompletion();
    }

    [Fact(DisplayName = "UTCID04 (A) - Hết lượt gửi lại -> Trả 429 RESEND_LIMIT_REACHED, không gửi email")]
    public Task Handle_ResendLimitReached_ReturnsRateLimitWithoutSending() =>
        AssertBeginFailure(RegistrationOtpStoreStatus.ResendLimitReached, 429, "RESEND_LIMIT_REACHED");

    [Fact(DisplayName = "UTCID05 (A) - Vượt lượt xác minh -> Trả 429 VERIFY_ATTEMPTS_EXCEEDED, không gửi email")]
    public Task Handle_VerificationAttemptsExceeded_ReturnsRateLimitWithoutSending() =>
        AssertBeginFailure(RegistrationOtpStoreStatus.AttemptsExceeded, 429, "VERIFY_ATTEMPTS_EXCEEDED");

    [Fact(DisplayName = "UTCID06 (A) - Phiên đã xác minh -> Trả 409 ALREADY_VERIFIED, không gửi email")]
    public Task Handle_AlreadyVerified_ReturnsConflictWithoutSending() =>
        AssertBeginFailure(RegistrationOtpStoreStatus.AlreadyVerified, 409, "ALREADY_VERIFIED");

    [Fact(DisplayName = "UTCID07 (A) - Gửi email lỗi -> Trả 503 EMAIL_SEND_FAILED")]
    public async Task Handle_EmailSendFailure_CancelsLease()
    {
        var harness = new Harness();
        harness.Email.Setup(service => service.SendAsync(
                EmailAddress, It.IsAny<string>(), It.IsAny<string>(), CancellationToken.None))
            .ThrowsAsync(new InvalidOperationException("SMTP failure"));

        AssertProblem(await harness.Handle(), 503, "EMAIL_SEND_FAILED");

        harness.Email.Verify(service => service.SendAsync(
            EmailAddress, It.IsAny<string>(), It.IsAny<string>(), CancellationToken.None), Times.Once);
        harness.OtpStore.Verify(store => store.CancelResendAsync(
            RegistrationId, LeaseId, It.IsAny<CancellationToken>()), Times.Once);
        harness.OtpStore.Verify(store => store.CompleteResendAsync(
            It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    private static async Task AssertBeginFailure(RegistrationOtpStoreStatus status, int httpStatus, string code)
    {
        var harness = new Harness();
        harness.OtpStore.Setup(store => store.TryBeginResendAsync(RegistrationId, CancellationToken.None))
            .ReturnsAsync(new RegistrationOtpStoreResult(status));

        AssertProblem(await harness.Handle(), httpStatus, code);

        harness.AssertNoEmailOrCompletion();
    }

    private static void AssertProblem(IActionResult result, int status, string code)
    {
        var objectResult = Assert.IsType<ObjectResult>(result);
        Assert.Equal(status, objectResult.StatusCode);
        var problem = Assert.IsType<ProblemDetails>(objectResult.Value);
        Assert.Equal(status, problem.Status);
        Assert.Equal(code, problem.Extensions["code"]);
    }

    private static RegistrationOtpStoreResult SuccessfulCompletion() => new(
        RegistrationOtpStoreStatus.Success, OtpExpires, RegistrationExpires, ResendAvailable);

    private sealed class Harness
    {
        public Mock<IRegistrationOtpStore> OtpStore { get; } = new();
        public Mock<IEmailService> Email { get; } = new();
        public DefaultHttpContext HttpContext { get; } = new();
        public RegistrationOtpCodeService Codes { get; } = new(new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["RegistrationOtp:HmacKey"] = Convert.ToBase64String(new byte[32])
            }).Build());

        public Harness()
        {
            OtpStore.Setup(store => store.TryBeginResendAsync(RegistrationId, CancellationToken.None))
                .ReturnsAsync(new RegistrationOtpStoreResult(RegistrationOtpStoreStatus.Success,
                    LeaseId: LeaseId, Email: EmailAddress));
            OtpStore.Setup(store => store.CompleteResendAsync(
                    RegistrationId, LeaseId, It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(SuccessfulCompletion());
            OtpStore.Setup(store => store.CancelResendAsync(
                    RegistrationId, LeaseId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(true);
            Email.Setup(service => service.SendAsync(
                    EmailAddress, It.IsAny<string>(), It.IsAny<string>(), CancellationToken.None))
                .Returns(Task.CompletedTask);
        }

        public Task<IActionResult> Handle()
        {
            var handler = new ResendRegistrationHandler(OtpStore.Object, Codes, Email.Object,
                new HttpContextAccessor { HttpContext = HttpContext }, new ResendRegistrationValidator(),
                NullLogger<ResendRegistrationHandler>.Instance);
            return handler.Handle(new ResendRegistrationCommand { RegistrationId = RegistrationId },
                CancellationToken.None);
        }

        public void AssertNoEmailOrCompletion()
        {
            Email.Verify(service => service.SendAsync(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
            OtpStore.Verify(store => store.CompleteResendAsync(
                It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
        }
    }
}
