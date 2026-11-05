namespace SmartPool.Domain.Enums;

public static class EquipmentPriorityLevels
{
    public const string Critical = "Critical";
    public const string High = "High";
    public const string Normal = "Normal";

    public static IReadOnlyList<string> All { get; } = Array.AsReadOnly(new[] { Critical, High, Normal });
}
