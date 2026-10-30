namespace SmartPool.Infrastructure.Persistence.Repositories.SwimClasses;

public sealed partial class SwimClassOperations
{
    // PostgreSQL timestamptz stores microseconds. Advance by at least one
    // microsecond so a rapid second edit always changes the concurrency token.
    private static DateTime NextVersionTimestamp(DateTime utcNow, params DateTime[] previous)
    {
        var candidate = new DateTime(utcNow.Ticks - utcNow.Ticks % 10, DateTimeKind.Utc);
        foreach (var prior in previous)
            if (candidate <= prior) candidate = prior.AddTicks(10);
        return candidate;
    }
}
