using SmartPool.Application.Features.ManageStaffs;
using SmartPool.Application.Features.ManageStaffs.Commands.CreateStaff;
using SmartPool.Application.Features.ManageStaffs.Commands.UpdateStaff;

namespace SmartPool.IntegrationTests;

public sealed class StaffEmploymentIntegrationTests
{
    [Fact]
    public async Task GivenEligibleStaffUser_WhenCreatingAndUpdatingEmployment_ThenUserIdentityRoleAndProfileStayUnchanged()
    {
        await using var fixture = await StaffEmploymentFixture.CreateAsync();
        try
        {
            var before = await fixture.ReadUserSnapshotAsync(fixture.EligibleStaffUserId);

            await fixture.CreateOperations().CreateStaffAsync(fixture.CreateCommand(fixture.EligibleStaffUserId), CancellationToken.None);
            await fixture.CreateOperations().UpdateStaffAsync(new UpdateStaffCommand
            {
                UserId = fixture.EligibleStaffUserId,
                BaseSalary = 12500000,
                Status = "Inactive"
            }, CancellationToken.None);

            var after = await fixture.ReadUserSnapshotAsync(fixture.EligibleStaffUserId);
            var employee = await fixture.ReadEmployeeAsync(fixture.EligibleStaffUserId);

            Assert.Equal(before, after);
            Assert.Equal((new DateOnly(2026, 10, 5), 12500000m, "Inactive"),
                (employee.JoinDate, employee.BaseSalary, employee.Status));
        }
        finally
        {
            await fixture.CleanupAsync();
            Assert.Equal((2, 7, 7), fixture.CleanupCounts);
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
            await Assert.ThrowsAsync<StaffConflictException>(() => fixture.CreateOperations()
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
    public async Task GivenSoftDeletedStaffRole_WhenReadingOptionsAndCreatingEmployment_ThenAccountIsExcludedAndRejected()
    {
        await using var fixture = await StaffEmploymentFixture.CreateAsync();
        try
        {
            var options = await fixture.CreateOperations().GetOptionsAsync(CancellationToken.None);

            Assert.DoesNotContain(options.Users, user => user.UserId == fixture.SoftDeletedRoleUserId);
            await Assert.ThrowsAsync<StaffConflictException>(() => fixture.CreateOperations()
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
    public async Task GivenTwoConcurrentEmploymentCreates_WhenAttachingTheSameUser_ThenOneEmployeeAndOneControlledConflictResult()
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
                catch (StaffConflictException)
                {
                    return false;
                }
            }

            var results = await Task.WhenAll(SubmitAsync(), SubmitAsync());

            Assert.Single(results, result => result);
            Assert.Single(results, result => !result);
            Assert.Equal(1, await fixture.EmployeeCountAsync(fixture.ConcurrentStaffUserId));
        }
        finally
        {
            await fixture.CleanupAsync();
            Assert.Equal((2, 7, 7), fixture.CleanupCounts);
        }
    }
}
