using System.Text.Json;
using SmartPool.Domain.Entities;

namespace SmartPool.Application.Features.ManageEquipments.Maintenance;

// A versioned application payload in the existing Description column; this is not a database entity.
public sealed record MaintenanceConfiguration
{
    public string Format { get; init; } = "smartpool.maintenance.v1";
    public Guid SeriesId { get; init; }
    public int Sequence { get; init; }
    public int AnchorSequence { get; init; }
    public DateOnly AnchorDate { get; init; }
    public string TaskName { get; init; } = string.Empty;
    public string RepeatUnit { get; init; } = "None";
    public int RepeatEvery { get; init; } = 1;
    public int RemindBeforeDays { get; init; }
    public string? PreviousEquipmentStatus { get; init; }

    public string Serialize() => JsonSerializer.Serialize(this);

    public static MaintenanceConfiguration Read(MaintenanceSchedule schedule)
    {
        try
        {
            var value = JsonSerializer.Deserialize<MaintenanceConfiguration>(schedule.Description ?? "");
            if (value is { Format: "smartpool.maintenance.v1", RepeatEvery: >= 1 and <= 365,
                RemindBeforeDays: >= 0 and <= 365 } && value.SeriesId != Guid.Empty && value.AnchorDate != default &&
                value.Sequence >= value.AnchorSequence && value.AnchorSequence >= 0 &&
                value.RepeatUnit is "None" or "Day" or "Week" or "Month" && !string.IsNullOrWhiteSpace(value.TaskName))
                return value;
        }
        catch (JsonException) { }
        // Existing schedules with plain descriptions remain visible and behave as one-off jobs.
        return new MaintenanceConfiguration { SeriesId = schedule.Id, AnchorDate = schedule.PlannedDate,
            TaskName = string.IsNullOrWhiteSpace(schedule.Description) ? "Bảo trì thiết bị" : schedule.Description };
    }

    public (int Sequence, DateOnly Date)? NextAfter(DateOnly today)
    {
        if (RepeatUnit == "None") return null;
        try
        {
            var step = RepeatUnit == "Week" ? checked(RepeatEvery * 7) : RepeatEvery;
            var elapsed = RepeatUnit == "Month"
                ? (today.Year - AnchorDate.Year) * 12 + today.Month - AnchorDate.Month
                : today.DayNumber - AnchorDate.DayNumber;
            var offset = Math.Max(Sequence - AnchorSequence + 1, Math.Max(0, elapsed / step));
            DateOnly date;
            do
            {
                date = RepeatUnit == "Month" ? AnchorDate.AddMonths(checked(offset * step)) : AnchorDate.AddDays(checked(offset * step));
                if (date > today) return (checked(AnchorSequence + offset), date);
                offset++;
            } while (true);
        }
        catch (ArgumentOutOfRangeException) { return null; }
        catch (OverflowException) { return null; }
    }
}
