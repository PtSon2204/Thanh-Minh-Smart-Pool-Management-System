using SmartPool.Application.Features.ManageStaffs.Commands.CreateStaff;
using SmartPool.Application.Features.ManageStaffs.Commands.UpdateStaff;

namespace SmartPool.UnitTests.Features.ManageStaffs;

public sealed class StaffEmploymentCommandValidatorTests
{
    [Fact]
    public void GivenEmploymentOnlyCreateCommand_WhenValidating_ThenItIsAccepted()
    {
        var result = new CreateStaffValidator().Validate(new CreateStaffCommand
        {
            UserId = Guid.NewGuid(),
            JoinDate = new DateOnly(2026, 10, 5),
            BaseSalary = 10000000,
            Status = "Working"
        });

        Assert.True(result.IsValid);
    }

    [Fact]
    public void GivenEmploymentOnlyUpdateCommand_WhenValidating_ThenItIsAccepted()
    {
        var result = new UpdateStaffValidator().Validate(new UpdateStaffCommand
        {
            UserId = Guid.NewGuid(),
            BaseSalary = 10000000,
            Status = "Inactive"
        });

        Assert.True(result.IsValid);
    }

    [Fact]
    public void GivenMissingJoinDate_WhenCreatingEmployment_ThenItIsRejected()
    {
        var result = new CreateStaffValidator().Validate(new CreateStaffCommand
        {
            UserId = Guid.NewGuid(),
            BaseSalary = 10000000,
            Status = "Working"
        });

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, error => error.PropertyName == nameof(CreateStaffCommand.JoinDate));
    }
}
