using System.Text.Json.Serialization;
using MediatR;
using SmartPool.Application.Common.Models;

namespace SmartPool.Application.Features.GoodsReceipts;

public sealed class CreateGoodsReceiptCommand : IRequest<GoodsReceiptResponse>
{
    // Generated once by the client and reused when retrying an uncertain request.
    public Guid Id { get; set; }
    public string ReceiptCode { get; set; } = string.Empty;
    public DateTime ReceivedAt { get; set; }
    public string? SupplierName { get; set; }
    public string? Note { get; set; }
    public List<GoodsReceiptLine> Details { get; set; } = [];
    [JsonIgnore]
    public Guid OperatorId { get; set; }
}

public sealed class GoodsReceiptLine
{
    public Guid ProductId { get; set; }
    public int Quantity { get; set; }
    public decimal UnitCost { get; set; }
}

public sealed class GetGoodsReceiptsQuery : IRequest<PagedResponse<GoodsReceiptResponse>>
{
    public string? SearchTerm { get; set; }
    public string? Status { get; set; }
    public Guid? ProductId { get; set; }
    public int PageIndex { get; set; } = 1;
    public int PageSize { get; set; } = 10;
}

public sealed record GetGoodsReceiptQuery(Guid Id) : IRequest<GoodsReceiptResponse>;
public sealed record PostGoodsReceiptCommand(Guid Id, Guid OperatorId) : IRequest<GoodsReceiptResponse>;
public sealed record CancelGoodsReceiptCommand(Guid Id) : IRequest<GoodsReceiptResponse>;

public sealed class GoodsReceiptResponse
{
    public Guid Id { get; set; }
    public string ReceiptCode { get; set; } = string.Empty;
    public DateTime ReceivedAt { get; set; }
    public string? SupplierName { get; set; }
    public string Status { get; set; } = string.Empty;
    public Guid CreatedBy { get; set; }
    public string? Note { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    public List<GoodsReceiptDetailResponse> Details { get; set; } = [];
    public decimal TotalAmount => Details.Sum(line => line.Quantity * line.UnitCost);
}

public sealed class GoodsReceiptDetailResponse
{
    public Guid Id { get; set; }
    public Guid ProductId { get; set; }
    public string ProductName { get; set; } = string.Empty;
    public int Quantity { get; set; }
    public decimal UnitCost { get; set; }
    public decimal LineAmount => Quantity * UnitCost;
}
