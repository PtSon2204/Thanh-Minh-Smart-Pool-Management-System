namespace SmartPool.IntegrationTests;

public sealed class IntegrationTestSafetyTests
{
    [Theory]
    [InlineData("Host=localhost;Database=smartpool_dev")]
    [InlineData("Host=localhost;Database=postgres")]
    [InlineData("Host=production.example.test;Database=smartpool_test")]
    [InlineData("Host=localhost;Database=smartpool_test_not-an-isolated-id")]
    public void GivenUnsafeDatabaseTarget_WhenValidating_ThenItIsRejectedBeforeConnecting(string connectionString)
    {
        Assert.Throws<InvalidOperationException>(() => IntegrationTestDatabase.ValidateTarget(connectionString));
    }

    [Theory]
    [InlineData("Host=localhost;Database=smartpool_test")]
    [InlineData("Host=localhost;Database=smartpool_test_0123456789abcdef0123456789abcdef")]
    public void GivenLocalTestDatabase_WhenValidating_ThenItIsAccepted(string connectionString)
    {
        IntegrationTestDatabase.ValidateTarget(connectionString);
    }
}
