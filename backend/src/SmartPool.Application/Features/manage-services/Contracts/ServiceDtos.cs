namespace SmartPool.Application.Features.ManageServices.Contracts;

public sealed record ServiceDto(Guid Id, string Name, string Type, decimal Price, int? StockQuantity, bool IsActive, bool IsDeleted, DateTime? CreatedAt, DateTime? UpdatedAt);
public sealed record CreateServiceRequest(string Name, string Type, decimal Price, int? StockQuantity, bool IsActive);
public sealed record UpdateServiceRequest(string Name, string Type, decimal Price, bool IsActive);
public sealed record AdjustStockRequest(int Delta, string Note);
public sealed record InventoryLogDto(Guid Id, Guid ProductId, string ChangeType, int Quantity, string? Note, Guid? CreatedBy, DateTime? CreatedAt);
public sealed record StockAdjustmentDto(InventoryLogDto Log, int StockQuantity);
public sealed record ServiceListQuery(int Page = 1, int PageSize = 20, string? Search = null, string? Type = null, bool? Active = null);
public sealed record InventoryHistoryQuery(int Page = 1, int PageSize = 20, DateOnly? Date = null);
public sealed record PageDto<T>(IReadOnlyList<T> Items, int Total, int Page, int PageSize);

public enum ServiceOperationError
{
    Validation,
    NotFound,
    Conflict
}

public sealed record ServiceOperationResult<T>(T? Value, ServiceOperationError? Error, IReadOnlyDictionary<string, string[]> Errors)
{
    public bool IsSuccess => Error is null;
    public static ServiceOperationResult<T> Success(T value) => new(value, null, new Dictionary<string, string[]>());
    public static ServiceOperationResult<T> Failure(ServiceOperationError error, string field, string message) => new(default, error, new Dictionary<string, string[]> { [field] = [message] });
}
