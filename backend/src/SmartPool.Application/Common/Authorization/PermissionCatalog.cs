namespace SmartPool.Application.Common.Authorization;

public static class PermissionCatalog
{
    private static readonly Dictionary<string, string[]> Modules = new(StringComparer.Ordinal)
    {
        ["profiles"] = ["read", "update"],
        ["staff"] = ["read", "create", "update"],
        ["shifts"] = ["read", "create", "update"],
        ["staff_schedule"] = ["read", "create", "delete"],
        ["swim_classes"] = ["read", "create", "update"],
        ["class_sessions"] = ["create"],
        ["attendance"] = ["read", "update"],
        ["ticket_lookup"] = ["read"],
        ["pool_entry"] = ["read", "create"],
        ["ticket_sales"] = ["read", "create"],
        ["customer_tickets"] = ["read", "create", "update"],
        ["ticket_types"] = ["read", "create", "update", "delete"],
        ["services"] = ["read", "create", "update"],
        ["inventory"] = ["read", "update"],
        ["rentals"] = ["read", "create", "update"],
        ["vouchers"] = ["read", "create", "update", "delete"],
        ["payments"] = ["read"],
        ["reports"] = ["read"],
        ["notifications"] = ["read", "create", "update"],
        ["safety_alerts"] = ["create", "read"]
    };

    public static IReadOnlyDictionary<string, string[]> All => Modules;

    public static bool IsValid(string? module, string? action) =>
        module is not null && action is not null &&
        Modules.TryGetValue(module, out var actions) && actions.Contains(action, StringComparer.Ordinal);

    public static string Policy(string module, string action) => $"permission:{module}:{action}";
}
