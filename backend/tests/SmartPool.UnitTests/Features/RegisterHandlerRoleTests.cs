using System.Linq.Expressions;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using SmartPool.Application.Features.Authentication.Commands.Register;
using SmartPool.Application.Interfaces.Repositories;
using SmartPool.Application.Interfaces.Services;
using SmartPool.Domain.Entities;

namespace SmartPool.UnitTests.Features;

public sealed class RegisterHandlerRoleTests
{
    [Theory]
    [InlineData("CUSTOMER", false, 201)]
    [InlineData("Customer", false, 201)]
    [InlineData("customer", false, 201)]
    [InlineData("cUsToMeR", false, 201)]
    [InlineData("Customer", null, 201)]
    [InlineData("CUSTOMER", true, 500)]
    [InlineData("Customer", true, 500)]
    [InlineData("customer", true, 500)]
    [InlineData("Admin", false, 500)]
    public async Task Registration_UsesOnlyNonDeletedCustomerRoles(
        string roleName, bool? isDeleted, int expectedStatus)
    {
        var role = new Role { Id = Guid.NewGuid(), Name = roleName, IsDeleted = isDeleted };
        var users = new Mock<IRepository<User>>();
        var profiles = new Mock<IRepository<UserProfile>>();
        var roles = new Mock<IRepository<Role>>();
        var unitOfWork = new Mock<IUnitOfWork>();
        var passwordHasher = new Mock<IPasswordHasher>();

        users.Setup(repository => repository.ExistsAsync(
                It.IsAny<Expression<Func<User, bool>>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);
        users.Setup(repository => repository.AddAsync(It.IsAny<User>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        profiles.Setup(repository => repository.AddAsync(It.IsAny<UserProfile>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        roles.Setup(repository => repository.FirstOrDefaultAsync(
                It.IsAny<Expression<Func<Role, bool>>>(), It.IsAny<CancellationToken>()))
            .Returns((Expression<Func<Role, bool>> predicate, CancellationToken _) =>
                Task.FromResult<Role?>(predicate.Compile()(role) ? role : null));
        unitOfWork.Setup(work => work.SaveChangesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(2);
        passwordHasher.Setup(hasher => hasher.HashPassword(It.IsAny<User>(), It.IsAny<string>()))
            .Returns("test-password-hash");

        var handler = new RegisterHandler(users.Object, profiles.Object, roles.Object,
            unitOfWork.Object, passwordHasher.Object, new RegisterValidator(),
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
        users.Verify(repository => repository.AddAsync(
                It.Is<User>(user => user.RoleId == role.Id), It.IsAny<CancellationToken>()),
            expectedStatus == 201 ? Times.Once() : Times.Never());
    }
}
