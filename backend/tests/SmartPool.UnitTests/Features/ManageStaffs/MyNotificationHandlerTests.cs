using Moq;
using SmartPool.Application.Features.Notifications;
using SmartPool.Application.Features.Notifications.Queries.GetNotifications;
using SmartPool.Application.Interfaces.Repositories;

namespace SmartPool.UnitTests.Features.ManageStaffs;

public sealed class MyNotificationHandlerTests
{
    [Fact]
    public async Task GivenOwnerQuery_WhenHandled_ThenOnlyThatOwnerIsPassedToOperations()
    {
        var userId = Guid.NewGuid();
        var result = new GetNotificationsResult { TotalCount = 1, Page = 2, PageSize = 10 };
        var operations = new Mock<IStaffOperations>();
        operations.Setup(item => item.GetMyNotificationsAsync(userId, 2, 10, It.IsAny<CancellationToken>()))
            .ReturnsAsync(result);
        var request = new GetMyNotificationsQuery(2, 10) { UserId = userId };

        var actual = await new GetMyNotificationsHandler(operations.Object).Handle(request, default);

        Assert.Same(result, actual);
        operations.Verify(item => item.GetMyNotificationsAsync(userId, 2, 10, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task GivenOwnerAndNotification_WhenMarkedRead_ThenOwnerScopeIsPassedToOperations()
    {
        var userId = Guid.NewGuid();
        var notificationId = Guid.NewGuid();
        var operations = new Mock<IStaffOperations>();
        operations.Setup(item => item.MarkMyNotificationReadAsync(userId, notificationId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        var request = new MarkMyNotificationReadCommand(notificationId) { UserId = userId };

        var actual = await new MarkMyNotificationReadHandler(operations.Object).Handle(request, default);

        Assert.True(actual);
        operations.Verify(item => item.MarkMyNotificationReadAsync(userId, notificationId, It.IsAny<CancellationToken>()), Times.Once);
    }
}
