namespace SmartPool.API.Authorization;

public static class AuthorizationPolicies
{
    public const string AdminOnly = nameof(AdminOnly);
    public const string AdminOrStaff = nameof(AdminOrStaff);
}
