namespace SmartPool.Application.Features.ManageServices.Contracts;

public sealed record RentalCheckoutRequest(Guid ProductId, int Quantity, string? CustomerName, string? CustomerPhone, decimal DepositPerUnit);
public sealed record RentalReturnRequest;
public sealed record RentalDto(Guid Id, Guid OrderId, Guid ProductId, string ProductName, DateTime? RentTime, DateTime? ReturnTime, decimal? DepositAmount, string Status, string? OrderStatus);
public sealed record RentalCheckoutDto(Guid OrderId, decimal TotalAmount, decimal FinalAmount, Guid OrderDetailId, IReadOnlyList<RentalDto> Rentals);
public sealed record RentalReturnDto(RentalDto Rental, int StockQuantity);
public sealed record RentalListQuery(int Page = 1, int PageSize = 20, string? Status = null, DateOnly? Date = null, Guid? ProductId = null);
