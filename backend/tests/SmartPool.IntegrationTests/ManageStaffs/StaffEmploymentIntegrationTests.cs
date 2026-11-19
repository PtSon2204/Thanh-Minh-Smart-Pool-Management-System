using SmartPool.Application.Features.ManageStaffs.Commands.UpdateStaff;
using SmartPool.Application.Features.ManageStaffs.Onboarding;
using SmartPool.Application.Features.ManageStaffs.Queries.GetStaffEdit;
using Microsoft.EntityFrameworkCore;
using System.Security.Cryptography;

namespace SmartPool.IntegrationTests.ManageStaffs;

public sealed class StaffEmploymentIntegrationTests
{
    [Fact]
    public async Task UpdateProfileThenToggleStatus()
    {
        await using var fixture = await StaffEmploymentFixture.CreateAsync();
        try
        {
            var userId = fixture.AttachedStaffUserId;
            var before = await fixture.ReadUserSnapshotAsync(userId);
            var edit = await fixture.CreateOperations().GetStaffEditAsync(
                new GetStaffEditQuery(userId, fixture.EligibleAdminUserId), CancellationToken.None);
            var phone = $"09{RandomNumberGenerator.GetInt32(0, 100_000_000):D8}";

            await fixture.CreateOperations(fixture.CreateContext()).UpdateStaffAsync(new UpdateStaffCommand
            {
                UserId = userId,
                ActorId = fixture.EligibleAdminUserId,
                Status = "Working",
                Details = new StaffEditDetails
                {
                    FullName = "Updated QA Staff",
                    Phone = phone,
                    Address = "Updated QA address"
                },
                ExpectedUpdatedAt = edit.ExpectedUpdatedAt,
                ExpectedUserUpdatedAt = edit.ExpectedUserUpdatedAt,
                ExpectedProfileUpdatedAt = edit.ExpectedProfileUpdatedAt,
                ExpectedProfileExists = edit.ExpectedProfileExists,
                ExpectedApprovedRevisionId = edit.ExpectedApprovedRevisionId,
                ExpectedApprovedRevisionVersion = edit.ExpectedApprovedRevisionVersion,
                ExpectedCorrectionVersion = edit.ExpectedCorrectionVersion
            }, CancellationToken.None);

            var edited = await fixture.ReadUserSnapshotAsync(userId);
            Assert.Equal(("Updated QA Staff", phone, "Updated QA address"),
                (edited.FullName, edited.Phone, edited.Address));
            Assert.Equal((before.DateOfBirth, before.Username, before.Email, before.RoleId),
                (edited.DateOfBirth, edited.Username, edited.Email, edited.RoleId));
            await using (var context = fixture.CreateContext())
                Assert.Equal(0, await context.StaffOnboardingRevisions.CountAsync(revision => revision.EmployeeId == userId));

            await fixture.CreateOperations(fixture.CreateContext()).UpdateStaffAsync(new UpdateStaffCommand
            {
                UserId = userId,
                Status = "Inactive"
            }, CancellationToken.None);

            var after = await fixture.ReadUserSnapshotAsync(userId);
            var employee = await fixture.ReadEmployeeAsync(userId);

            Assert.Equal(edited with { Status = "LOCKED" }, after);
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
