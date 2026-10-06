namespace SmartPool.Application.Features.AccessControlPool.Contracts
{
    public static class PoolAccessValues
    {
        public const string Active = "Active";
        public const string Used = "Used";
        public const string Manual = "Manual";
        public const string Qr = "Qr";
        public const string Allowed = "Allowed";
        public const string Denied = "Denied";

        public static bool IsActive(string? status) =>
            string.Equals(status, Active, StringComparison.OrdinalIgnoreCase);

        public static bool IsUsed(string? status) =>
            string.Equals(status, Used, StringComparison.OrdinalIgnoreCase);

        public static bool IsAllowedEntryStatus(string? status) =>
            string.Equals(status, Allowed, StringComparison.OrdinalIgnoreCase);
    }
}
