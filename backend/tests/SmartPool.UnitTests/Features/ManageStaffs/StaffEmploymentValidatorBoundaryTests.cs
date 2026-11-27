using SmartPool.Application.Features.ManageStaffs.Commands.UpdateStaff;

namespace SmartPool.UnitTests.Features.ManageStaffs;

public sealed class StaffEmploymentValidatorBoundaryTests
{
    [Fact]
    public void GivenUnsupportedEmploymentStatus_WhenUpdating_ThenItIsRejected()
    {
        var result = new UpdateStaffValidator().Validate(new UpdateStaffCommand { UserId = Guid.NewGuid(), Status = "Fired" });
        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, error => error.PropertyName == nameof(UpdateStaffCommand.Status));
        Assert.All(result.Errors, error => Assert.False(string.IsNullOrWhiteSpace(error.ErrorMessage)));
    }

    [Fact]
    public void GivenEmptyUserId_WhenUpdatingEmployment_ThenItIsRejected()
    {
        var result = new UpdateStaffValidator().Validate(new UpdateStaffCommand { UserId = Guid.Empty, Status = "Working" });
        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, error => error.PropertyName == nameof(UpdateStaffCommand.UserId));
        Assert.All(result.Errors, error => Assert.False(string.IsNullOrWhiteSpace(error.ErrorMessage)));
    }
}
