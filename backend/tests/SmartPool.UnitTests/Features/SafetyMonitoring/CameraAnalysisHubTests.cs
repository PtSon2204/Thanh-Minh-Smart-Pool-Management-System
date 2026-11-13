using System.Security.Claims;
using Microsoft.AspNetCore.SignalR;
using Moq;
using SmartPool.Infrastructure.Services;

namespace SmartPool.UnitTests.Features.SafetyMonitoring;

public sealed class CameraAnalysisHubTests
{
    private static (NotificationHubBase Hub, Mock<IGroupManager> Groups) Create(string? role)
    {
        var user = new ClaimsPrincipal(new ClaimsIdentity(role is null ? [] : [new Claim(ClaimTypes.Role, role)], "test"));
        var context = new Mock<HubCallerContext>();
        context.SetupGet(x => x.User).Returns(user);
        context.SetupGet(x => x.ConnectionId).Returns("viewer-1");
        var groups = new Mock<IGroupManager>();
        groups.Setup(x => x.AddToGroupAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
        return (new NotificationHubBase { Context = context.Object, Groups = groups.Object }, groups);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("LIFEGUARD")]
    [InlineData("SALES")]
    public async Task Subscribe_RejectsRolesWithoutCameraPermission(string? role)
    {
        var (hub, groups) = Create(role);
        await Assert.ThrowsAsync<HubException>(() => hub.SubscribeCameraAnalysis(Guid.NewGuid()));
        groups.Verify(x => x.AddToGroupAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Subscribe_AdminJoinsOnlyRequestedCameraGroup_RejectsEmptyId()
    {
        var (hub, groups) = Create("ADMIN");
        var id = Guid.NewGuid();
        await hub.SubscribeCameraAnalysis(id);
        groups.Verify(x => x.AddToGroupAsync("viewer-1", $"camera-analysis_{id:N}", It.IsAny<CancellationToken>()), Times.Once);
        await Assert.ThrowsAsync<HubException>(() => hub.SubscribeCameraAnalysis(Guid.Empty));
    }
}
