using SmartPool.Application.Features.Authentication.Models;
using System.Linq.Expressions;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using System.Net;
using SmartPool.Application.Features.Authentication.Commands.Register;
using SmartPool.Application.Interfaces.Repositories;
using SmartPool.Application.Interfaces.Services;
using SmartPool.Domain.Entities;
using SmartPool.Application.Features.Authentication.Commands.Register.VerifyOTP;

namespace SmartPool.UnitTests.Features;

public sealed class RegisterHandlerRoleTests
{
    [Theory]
    [InlineData("CUSTOMER", false, 202)]
    [InlineData("Customer", false, 202)]
    [InlineData("customer", false, 202)]
    [InlineData("cUsToMeR", false, 202)]
    [InlineData("Customer", null, 202)]
    [InlineData("CUSTOMER", true, 500)]
    [InlineData("Customer", true, 500)]
    [InlineData("customer", true, 500)]
    [InlineData("Admin", false, 500)]
    public async Task Registration_UsesOnlyNonDeletedCustomerRoles(
        string roleName, bool? isDeleted, int expectedStatus)
    {
        var role = new Role { Id = Guid.NewGuid(), Name = roleName, IsDeleted = isDeleted };
        var userOperations = new Mock<IUserOperations>();
        var passwordHasher = new Mock<IPasswordHasher>();
        var otpStore = new Mock<IRegistrationOtpStore>();
        var emailService = new Mock<IEmailService>();
        var httpContext = new DefaultHttpContext();
        httpContext.Connection.RemoteIpAddress = IPAddress.Loopback;
        var accessor = new HttpContextAccessor { HttpContext = httpContext };
        var codeService = new RegistrationOtpCodeService(new ConfigurationBuilder().AddInMemoryCollection(
            new Dictionary<string, string?> { ["RegistrationOtp:HmacKey"] = Convert.ToBase64String(new byte[32]) }).Build());

        userOperations.Setup(ops => ops.ExistsByUsernameAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);
        userOperations.Setup(ops => ops.ExistsByEmailAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);
        userOperations.Setup(ops => ops.ExistsByPhoneAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);
        userOperations.Setup(ops => ops.GetRoleByNameAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((string name, CancellationToken _) =>
            {
                if (string.Equals(roleName, name, StringComparison.OrdinalIgnoreCase) && isDeleted != true)
                    return role;
                return null;
            });
        passwordHasher.Setup(hasher => hasher.HashPassword(It.IsAny<User>(), It.IsAny<string>()))
            .Returns("test-password-hash");
        otpStore.Setup(store => store.TryConsumeStartQuotaAsync(It.IsAny<string>(), It.IsAny<string>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new RegistrationOtpStoreResult(RegistrationOtpStoreStatus.Success));
        var now = DateTimeOffset.UtcNow;
        otpStore.Setup(store => store.TryStartAsync(It.IsAny<PendingRegistration>(), It.IsAny<string>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new RegistrationOtpStoreResult(RegistrationOtpStoreStatus.Success,
                now.AddMinutes(5), now.AddMinutes(30), now.AddMinutes(1)));
        emailService.Setup(service => service.SendAsync(It.IsAny<string>(), It.IsAny<string>(),
                It.IsAny<string>(), It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);

        var handler = new RegisterHandler(userOperations.Object, passwordHasher.Object,
            otpStore.Object, codeService, emailService.Object, accessor, new RegisterValidator(),
            NullLogger<RegisterHandler>.Instance);
        var request = new RegisterCommand
        {
            Email = "role.test@example.com",
            Phone = "0912345678",
            Password = "Registration123"
        };

        var result = Assert.IsType<ObjectResult>(await handler.Handle(request, CancellationToken.None));

        Assert.Equal(expectedStatus, result.StatusCode);
        if (expectedStatus == 202)
        {
            otpStore.Verify(store => store.TryStartAsync(
                It.Is<PendingRegistration>(pending => pending.Username == string.Empty &&
                    pending.Email == request.Email && pending.FullName == request.Email),
                It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Once());
            emailService.Verify(service => service.SendAsync(request.Email,
                It.IsAny<string>(), It.Is<string>(body => body.Contains("Mã xác minh")),
                It.IsAny<CancellationToken>()), Times.Once());
        }
        otpStore.Verify(store => store.TryStartAsync(It.IsAny<PendingRegistration>(), It.IsAny<string>(),
            It.IsAny<CancellationToken>()), expectedStatus == 202 ? Times.Once() : Times.Never());
    }
}
