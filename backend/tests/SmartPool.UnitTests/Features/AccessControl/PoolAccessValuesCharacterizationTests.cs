using SmartPool.Application.Features.AccessControlPool.Contracts;

namespace SmartPool.UnitTests.Features.AccessControl;

public sealed class PoolAccessValuesCharacterizationTests
{
    [Fact]
    public void GivenExistingAccessValues_WhenReadingConstants_ThenPublicSpellingsRemainUnchanged()
    {
        Assert.Equal("Active", PoolAccessValues.Active);
        Assert.Equal("Used", PoolAccessValues.Used);
        Assert.Equal("Allowed", PoolAccessValues.Allowed);
        Assert.Equal("Denied", PoolAccessValues.Denied);
    }
}
