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
        var users = new Mock<IRepository<User>>();
        var roles = new Mock<IRepository<Role>>();
        var passwordHasher = new Mock<IPasswordHasher>();
        var otpStore = new Mock<IRegistrationOtpStore>();
        var emailService = new Mock<IEmailService>();
        var httpContext = new DefaultHttpContext();
        httpContext.Connection.RemoteIpAddress = IPAddress.Loopback;
        var accessor = new HttpContextAccessor { HttpContext = httpContext };
        var codeService = new RegistrationOtpCodeService(new ConfigurationBuilder().AddInMemoryCollection(
            new Dictionary<string, string?> { ["RegistrationOtp:HmacKey"] = Convert.ToBase64String(new byte[32]) }).Build());

        users.Setup(repository => repository.ExistsAsync(
                It.IsAny<Expression<Func<User, bool>>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);
        roles.Setup(repository => repository.FirstOrDefaultAsync(
                It.IsAny<Expression<Func<Role, bool>>>(), It.IsAny<CancellationToken>()))
            .Returns((Expression<Func<Role, bool>> predicate, CancellationToken _) =>
                Task.FromResult<Role?>(predicate.Compile()(role) ? role : null));
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

        var handler = new RegisterHandler(users.Object, roles.Object, passwordHasher.Object,
            otpStore.Object, codeService, emailService.Object, accessor, new RegisterValidator(),
            NullLogger<RegisterHandler>.Instance);
        var request = new RegisterCommand
        {
            Username = "role.test",
            Email = "role.test@example.com",
            Phone = "0912345678",
            Password = "Registration123",
            ConfirmPassword = "Registration123",
            FullName = "Role Test"
        };

        var result = Assert.IsType<ObjectResult>(await handler.Handle(request, CancellationToken.None));

        Assert.Equal(expectedStatus, result.StatusCode);
        users.Verify(repository => repository.AddAsync(It.IsAny<User>(), It.IsAny<CancellationToken>()), Times.Never());
        otpStore.Verify(store => store.TryStartAsync(It.IsAny<PendingRegistration>(), It.IsAny<string>(),
            It.IsAny<CancellationToken>()), expectedStatus == 202 ? Times.Once() : Times.Never());
    }
}
