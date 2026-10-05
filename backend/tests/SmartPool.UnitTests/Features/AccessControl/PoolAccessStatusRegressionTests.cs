using SmartPool.Application.Features.AccessControlPool.Contracts;

namespace SmartPool.UnitTests.Features.AccessControl;

public sealed class PoolAccessStatusRegressionTests
{
    [Theory]
    [InlineData("ACTIVE")]
    [InlineData("active")]
    [InlineData("Active")]
    [InlineData("AcTiVe")]
    public void GivenAnyActiveSpelling_WhenCheckingTicketStatus_ThenTicketIsActive(string status)
    {
        Assert.True(PoolAccessValues.IsActive(status));
    }

    [Theory]
    [InlineData("USED")]
    [InlineData("used")]
    [InlineData("UsEd")]
    public void GivenAnyUsedSpelling_WhenCheckingTicketStatus_ThenTicketIsUsed(string status)
    {
        Assert.True(PoolAccessValues.IsUsed(status));
        Assert.False(PoolAccessValues.IsActive(status));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("Pending")]
    [InlineData("Active ")]
    public void GivenUnknownTicketStatus_WhenCheckingIt_ThenItIsNeitherActiveNorUsed(string? status)
    {
        Assert.False(PoolAccessValues.IsActive(status));
        Assert.False(PoolAccessValues.IsUsed(status));
    }

    [Theory]
    [InlineData("ALLOWED")]
    [InlineData("allowed")]
    [InlineData("AlLoWeD")]
    public void GivenHistoricalAcceptedStatus_WhenCheckingIt_ThenItIsAccepted(string status)
    {
        Assert.True(PoolAccessValues.IsAllowedEntryStatus(status));
    }
}
