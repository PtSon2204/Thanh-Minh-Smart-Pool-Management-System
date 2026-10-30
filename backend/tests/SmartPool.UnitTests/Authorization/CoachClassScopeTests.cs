using Moq;
using SmartPool.Application.Common.Models;
using SmartPool.Application.Features.ManageSwimClasses.Queries.GetSwimClasses;
using SmartPool.Application.Interfaces.Repositories;
using SmartPool.Application.Interfaces.Services;

namespace SmartPool.UnitTests.Authorization;

public sealed class CoachClassScopeTests
{
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task CustomerMayRequestCoachClassScopeOnlyWithCoachClaim(bool hasCoachRole)
    {
        var userId = Guid.NewGuid();
        var query = new GetSwimClassesQuery { Scope = "coach" };
        var operations = new Mock<ISwimClassOperations>();
        operations.Setup(item => item.GetListAsync(query, userId, "COACH",
            It.IsAny<CancellationToken>())).ReturnsAsync(new PagedResponse<SwimClassListItem>());
        var currentUser = new Mock<ICurrentUser>();
        currentUser.SetupGet(item => item.UserId).Returns(userId);
        currentUser.SetupGet(item => item.Role).Returns("CUSTOMER");
        currentUser.Setup(item => item.IsInRole("COACH")).Returns(hasCoachRole);
        var handler = new GetSwimClassesHandler(operations.Object, currentUser.Object,
            new GetSwimClassesValidator());

        if (hasCoachRole)
            await handler.Handle(query, CancellationToken.None);
        else
            await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
                handler.Handle(query, CancellationToken.None));

        operations.Verify(item => item.GetListAsync(query, userId, "COACH",
            It.IsAny<CancellationToken>()), hasCoachRole ? Times.Once() : Times.Never());
    }
}
