using FluentValidation;
using Moq;
using SmartPool.Application.Interfaces.Services;
using SmartPool.Application.Features.ManageEquipments.Queries.GetCameraList;

namespace SmartPool.UnitTests.Features.SafetyMonitoring;

public sealed class CameraSearchValidationTests
{
    [Theory]
    [InlineData("t", false)]
    [InlineData(" t ! ", false)]
    [InlineData("%_", false)]
    [InlineData("7", false)]
    [InlineData(" \t ", true)]
    [InlineData("Camera demo", true)]
    [InlineData("phía bên phải", true)]
    [InlineData("mét 7", true)]
    public void Search_RequiresTwoLettersOrDigitsIgnoringWhitespace(string search, bool valid)
    {
        Assert.Equal(valid, new GetCameraListValidator().Validate(new GetCameraListQuery(Search: search)).IsValid);
    }

    [Fact]
    public void Search_NormalizesWhitespaceAndChecksNormalizedLength()
    {
        Assert.Equal("camera2", GetCameraListValidator.NormalizeSearch(" ca\tmera \n2 "));
        var validator = new GetCameraListValidator();
        Assert.True(validator.Validate(new GetCameraListQuery(Search: string.Concat(Enumerable.Repeat("a ", 200)))).IsValid);
        Assert.False(validator.Validate(new GetCameraListQuery(Search: new string('a', 201))).IsValid);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" \t\n ")]
    public void Search_OmittedOrBlankApiFilter_AllowsInitialListAndReset(string? search)
    {
        // The UI rejects an empty search submission. The optional API filter
        // remains empty for initial loading and the explicit Reset action.
        Assert.True(new GetCameraListValidator().Validate(new GetCameraListQuery(Search: search)).IsValid);
    }

    [Theory]
    [InlineData("t")]
    [InlineData(" t ! ")]
    [InlineData("%_")]
    public async Task Search_InvalidInput_DoesNotReadRepositoryOrCameraStatus(string search)
    {
        var repository = new Mock<ICameraListRepository>(MockBehavior.Strict);
        var cameras = new Mock<ICameraService>(MockBehavior.Strict);
        var handler = new GetCameraListHandler(repository.Object, cameras.Object, new GetCameraListValidator());
        await Assert.ThrowsAsync<ValidationException>(() => handler.Handle(new(Search: search), default));
        repository.VerifyNoOtherCalls();
        cameras.VerifyNoOtherCalls();
    }
}
