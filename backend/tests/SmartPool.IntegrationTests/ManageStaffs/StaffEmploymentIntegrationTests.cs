using SmartPool.Application.Features.ManageStaffs.Commands.UpdateStaff;
using SmartPool.Application.Features.ManageStaffs.Onboarding;

namespace SmartPool.IntegrationTests;

public sealed class StaffEmploymentIntegrationTests
{
    [Fact]
    public async Task GivenHistoricalEmployee_WhenUpdatingStatus_ThenUserIdentityRoleAndProfileStayUnchanged()
    {
        await using var fixture = await StaffEmploymentFixture.CreateAsync();
        try
        {
            var before = await fixture.ReadUserSnapshotAsync(fixture.AttachedStaffUserId);

            await fixture.CreateOperations().UpdateStaffAsync(new UpdateStaffCommand
            {
                UserId = fixture.AttachedStaffUserId,

                Status = "Inactive"
            }, CancellationToken.None);

            var after = await fixture.ReadUserSnapshotAsync(fixture.AttachedStaffUserId);
            var employee = await fixture.ReadEmployeeAsync(fixture.AttachedStaffUserId);

            Assert.Equal(before, after);
            Assert.Equal((new DateOnly(2025, 1, 1), "Inactive"),
                (employee.JoinDate, employee.Status));
        }
        finally
        {
            await fixture.CleanupAsync();
            Assert.Equal((1, 7, 7), fixture.CleanupCounts);
        }
    }

    [Fact]
    public async Task GivenMixedUserRolesAndEmployment_WhenReadingOptions_ThenOnlyUnattachedActiveStaffAndAdminUsersAppear()
    {
        await using var fixture = await StaffEmploymentFixture.CreateAsync();
        try
        {
            var options = await fixture.CreateOperations().GetOptionsAsync(CancellationToken.None);

            var fixtureOptions = options.Users.Where(user => fixture.UserIds.Contains(user.UserId));

            Assert.Equal(
                new[] { fixture.ConcurrentStaffUserId, fixture.EligibleAdminUserId, fixture.EligibleStaffUserId }.Order(),
                fixtureOptions.Select(user => user.UserId).Order());
        }
        finally
        {
            await fixture.CleanupAsync();
            Assert.Equal((1, 7, 7), fixture.CleanupCounts);
        }
    }

    [Fact]
    public async Task GivenCustomerUser_WhenCreatingEmployment_ThenItIsRejectedWithoutEmployeeAttachment()
    {
        await using var fixture = await StaffEmploymentFixture.CreateAsync();
        try
        {
            await Assert.ThrowsAsync<StaffOnboardingException>(() => fixture.CreateOperations()
                .CreateStaffAsync(fixture.CreateCommand(fixture.CustomerUserId), CancellationToken.None));

            Assert.False(await fixture.EmployeeExistsAsync(fixture.CustomerUserId));
        }
        finally
        {
            await fixture.CleanupAsync();
            Assert.Equal((1, 7, 7), fixture.CleanupCounts);
        }
    }

    [Fact]
    public async Task GivenSoftDeletedSalesRole_WhenReadingOptionsAndCreatingEmployment_ThenAccountIsExcludedAndRejected()
    {
        await using var fixture = await StaffEmploymentFixture.CreateAsync();
        try
        {
            var options = await fixture.CreateOperations().GetOptionsAsync(CancellationToken.None);

            Assert.DoesNotContain(options.Users, user => user.UserId == fixture.SoftDeletedRoleUserId);
            await Assert.ThrowsAsync<StaffOnboardingException>(() => fixture.CreateOperations()
                .CreateStaffAsync(fixture.CreateCommand(fixture.SoftDeletedRoleUserId), CancellationToken.None));
            Assert.False(await fixture.EmployeeExistsAsync(fixture.SoftDeletedRoleUserId));
        }
        finally
        {
            await fixture.CleanupAsync();
            Assert.Equal((1, 7, 7), fixture.CleanupCounts);
        }
    }


    [Fact]
    public async Task GivenTwoConcurrentLegacyCreates_ThenBothRejectWithoutEmployeeAttachment()
    {
        await using var fixture = await StaffEmploymentFixture.CreateAsync();
        try
        {
            using var gate = new Barrier(2);

            async Task<bool> SubmitAsync()
            {
                await using var context = fixture.CreateContext();
                await Task.Run(() => gate.SignalAndWait(TimeSpan.FromSeconds(10)));
                try
                {
                    await fixture.CreateOperations(context)
                        .CreateStaffAsync(fixture.CreateCommand(fixture.ConcurrentStaffUserId), CancellationToken.None);
                    return true;
                }
                catch (StaffOnboardingException exception) when (exception.Code == "ONBOARDING_REQUIRED")
                {
                    return false;
                }
            }

            var results = await Task.WhenAll(SubmitAsync(), SubmitAsync());

            Assert.All(results, result => Assert.False(result));
            Assert.Equal(0, await fixture.EmployeeCountAsync(fixture.ConcurrentStaffUserId));
        }
        finally
        {
            await fixture.CleanupAsync();
            Assert.Equal((1, 7, 7), fixture.CleanupCounts);
        }
    }
}
