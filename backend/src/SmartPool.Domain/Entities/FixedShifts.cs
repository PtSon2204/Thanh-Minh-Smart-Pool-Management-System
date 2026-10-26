namespace SmartPool.Domain.Entities;

public sealed record FixedShiftDefinition(Guid Id, string Name, TimeOnly StartTime, TimeOnly EndTime);

public static class FixedShifts
{
    public static readonly FixedShiftDefinition Morning = new(
        Guid.Parse("0f5c2b1a-7d3e-4a6b-8c9d-1e2f3a4b5c6d"),
        "Ca sáng",
        new TimeOnly(6, 0),
        new TimeOnly(11, 30));

    public static readonly FixedShiftDefinition Afternoon = new(
        Guid.Parse("1a6d3c2b-8e4f-4b7c-9d0e-2f3a4b5c6d7e"),
        "Ca chiều",
        new TimeOnly(13, 30),
        new TimeOnly(19, 30));

    public static readonly IReadOnlyList<FixedShiftDefinition> All = [Morning, Afternoon];

    public static readonly IReadOnlyList<Guid> Ids = All.Select(shift => shift.Id).ToArray();
}
