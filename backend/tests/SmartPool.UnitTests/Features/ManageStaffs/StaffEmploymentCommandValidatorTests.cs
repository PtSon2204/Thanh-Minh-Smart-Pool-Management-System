using SmartPool.Application.Features.ManageStaffs.Commands.UpdateStaff;

namespace SmartPool.UnitTests.Features.ManageStaffs;

public sealed class StaffEmploymentCommandValidatorTests
{
    [Fact]
    public void GivenEmploymentOnlyUpdateCommand_WhenValidating_ThenItIsAccepted()
    {
        var result = new UpdateStaffValidator().Validate(new UpdateStaffCommand { UserId = Guid.NewGuid(), Status = "Inactive" });
        Assert.True(result.IsValid);
        Assert.Empty(result.Errors);
    }

    [Fact]
    public void GivenWorkingEmployment_WhenUpdating_ThenItIsAccepted()
    {
        var result = new UpdateStaffValidator().Validate(new UpdateStaffCommand { UserId = Guid.NewGuid(), Status = "Working" });
        Assert.True(result.IsValid);
        Assert.Empty(result.Errors);
    }
}
