using SmartPool.Application.Features.ManageStaffs.Commands.CreateStaff;
using SmartPool.Application.Features.ManageStaffs.Commands.UpdateStaff;

namespace SmartPool.UnitTests.Features.ManageStaffs;

public sealed class StaffEmploymentValidatorBoundaryTests
{
    [Fact]
    public void GivenUnsupportedEmploymentStatus_WhenCreating_ThenItIsRejected()
    {
        var result = new CreateStaffValidator().Validate(new CreateStaffCommand
        {
            UserId = Guid.NewGuid(), JoinDate = new DateOnly(2026, 10, 5), Status = "Fired"
        });

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, error => error.PropertyName == nameof(CreateStaffCommand.Status));
    }

    [Fact]
    public void GivenUnsupportedEmploymentStatus_WhenUpdating_ThenItIsRejected()
    {
        var result = new UpdateStaffValidator().Validate(new UpdateStaffCommand
        {
            UserId = Guid.NewGuid(), Status = "Fired"
        });

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, error => error.PropertyName == nameof(UpdateStaffCommand.Status));
    }

    [Fact]
    public void GivenEmptyUserId_WhenCreatingEmployment_ThenItIsRejected()
    {
        var result = new CreateStaffValidator().Validate(new CreateStaffCommand
        {
            UserId = Guid.Empty, JoinDate = new DateOnly(2026, 10, 5), Status = "Working"
        });

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, error => error.PropertyName == nameof(CreateStaffCommand.UserId));
    }

    [Fact]
    public void GivenEmptyUserId_WhenUpdatingEmployment_ThenItIsRejected()
    {
        var result = new UpdateStaffValidator().Validate(new UpdateStaffCommand
        {
            UserId = Guid.Empty, Status = "Working"
        });

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, error => error.PropertyName == nameof(UpdateStaffCommand.UserId));
    }
}
