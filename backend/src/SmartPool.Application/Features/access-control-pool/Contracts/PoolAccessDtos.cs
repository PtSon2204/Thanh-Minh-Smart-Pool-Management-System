namespace SmartPool.Application.Features.AccessControlPool.Contracts;

public static class PoolAccessValues
{
    public const string SingleUse = "SingleUse";
    public const string Monthly = "Monthly";
    public const string Active = "Active";
    public const string Used = "Used";
    public const string Manual = "Manual";
    public const string Qr = "Qr";
    public const string Allowed = "Allowed";
    public const string Denied = "Denied";
}

public sealed record TicketSummaryDto(
    Guid Id,
    string TicketTypeName,
    string Category,
    DateTime? ExpiryDate,
    string? Status);

public sealed record TicketLookupResult(
    bool Found,
    bool IsValid,
    string Reason,
    TicketSummaryDto? Ticket);

public sealed record EntryConfirmationResult(
    bool Allowed,
    string Reason,
    TicketSummaryDto? Ticket,
    DateTime ScanTime,
    string InputMode);

public sealed record EntryHistoryItemDto(
    Guid Id,
    Guid? TicketId,
    string Status,
    string? Reason,
    DateTime ScanTime,
    Guid? OperatorId,
    string? InputMode);

public sealed record PagedResult<T>(IReadOnlyList<T> Items, int Total, int Page, int PageSize);

public sealed record DailyEntrySummaryDto(DateOnly Date, int AcceptedEntries);

public sealed record ValidationResult<T>(T? Value, IReadOnlyDictionary<string, string[]>? Errors)
{
    public bool IsValid => Errors is null;

    public static ValidationResult<T> Success(T value) => new(value, null);

    public static ValidationResult<T> Failure(string property, string error) =>
        new(default, new Dictionary<string, string[]> { [property] = [error] });
}
