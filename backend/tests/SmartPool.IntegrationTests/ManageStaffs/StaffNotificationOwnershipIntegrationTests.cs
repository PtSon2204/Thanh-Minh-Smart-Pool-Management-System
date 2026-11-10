using Microsoft.EntityFrameworkCore;
using SmartPool.Domain.Entities;

namespace SmartPool.IntegrationTests;

public sealed class StaffNotificationOwnershipIntegrationTests
{
    [Fact]
    public async Task OwnNotificationReadsAndMarksCannotAccessAnotherUsersNotifications()
    {
        await using var fixture = await StaffWorkPatternTestFixture.CreateAsync();
        var ownNotificationId = Guid.NewGuid();
        var otherNotificationId = Guid.NewGuid();
        var otherEmployeeId = await fixture.AddReplacementEmployeeAsync();
        var now = fixture.Clock.GetUtcNow().UtcDateTime;
        fixture.Context.Notifications.AddRange(
            new Notification
            {
                Id = ownNotificationId,
                UserId = fixture.EmployeeId,
                Title = "Own notification",
                Content = "Synthetic content",
                Type = "SYSTEM",
                IsRead = false,
                CreatedAt = now
            },
            new Notification
            {
                Id = otherNotificationId,
                UserId = otherEmployeeId,
                Title = "Other notification",
                Content = "Synthetic content",
                Type = "SYSTEM",
                IsRead = false,
                CreatedAt = now
            });
        try
        {
            await fixture.Context.SaveChangesAsync();

            var ownNotifications = await fixture.Operations.GetMyNotificationsAsync(fixture.EmployeeId, 1, 20, default);
            Assert.Equal(1, ownNotifications.TotalCount);
            Assert.Equal(ownNotificationId, Assert.Single(ownNotifications.Items).Id);

            var changed = await fixture.Operations.MarkMyNotificationReadAsync(fixture.EmployeeId, otherNotificationId, default);
            var otherNotification = await fixture.Context.Notifications.AsNoTracking()
                .SingleAsync(item => item.Id == otherNotificationId);

            Assert.False(changed);
            Assert.False(otherNotification.IsRead);
        }
        finally
        {
            await fixture.Context.Notifications.Where(item => item.Id == ownNotificationId || item.Id == otherNotificationId)
                .ExecuteDeleteAsync();
            await fixture.Context.Employees.Where(item => item.UserId == otherEmployeeId).ExecuteDeleteAsync();
            await fixture.Context.UserProfiles.Where(item => item.UserId == otherEmployeeId).ExecuteDeleteAsync();
            await fixture.Context.Users.Where(item => item.Id == otherEmployeeId).ExecuteDeleteAsync();
        }
    }
}
