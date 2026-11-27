namespace SmartPool.API.Authorization;

public static class AuthorizationPolicies
{
    public const string AdminOnly = nameof(AdminOnly);
    public const string LifeguardOnly = nameof(LifeguardOnly);
    public const string SafetyOperators = nameof(SafetyOperators);
}
