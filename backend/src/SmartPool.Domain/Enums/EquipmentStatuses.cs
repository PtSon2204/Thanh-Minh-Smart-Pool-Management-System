namespace SmartPool.Domain.Enums;

public static class EquipmentStatuses
{
    public const string Active = "Active";
    public const string Maintenance = "Maintenance";
    public const string Inactive = "Inactive";

    public static IReadOnlyList<string> All { get; } = Array.AsReadOnly(new[] { Active, Maintenance, Inactive });
}
