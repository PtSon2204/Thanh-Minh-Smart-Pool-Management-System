using Microsoft.Extensions.Configuration;

namespace SmartPool.IntegrationTests;

internal static class IntegrationTestSettings
{
    private static readonly IConfiguration Defaults = new ConfigurationBuilder()
        .SetBasePath(AppContext.BaseDirectory)
        .AddJsonFile("appsettings.IntegrationTests.json", optional: false)
        .Build();

    public static string DefaultConnection => Defaults.GetConnectionString("DefaultConnection")
        ?? throw new InvalidOperationException("The test-only DefaultConnection setting is missing.");

    public static bool Bootstrap => bool.TryParse(Environment.GetEnvironmentVariable("SMARTPOOL_TEST_BOOTSTRAP"), out var enabled)
        ? enabled : Defaults.GetValue<bool>("Bootstrap");

    public static string RedisConnection => Environment.GetEnvironmentVariable("SMARTPOOL_TEST_REDIS_CONNECTION")
        ?? Defaults.GetConnectionString("Redis")
        ?? throw new InvalidOperationException("The test-only Redis setting is missing.");

}
