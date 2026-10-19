using FluentValidation;
using MediatR;
using SmartPool.Application.Common.Models;
using SmartPool.Application.Interfaces.Repositories;

namespace SmartPool.Application.Features.GoodsReceipts;

public sealed class GoodsReceiptHandlers(IGoodsReceiptOperations operations,
    IValidator<CreateGoodsReceiptCommand> createValidator, IValidator<GetGoodsReceiptsQuery> listValidator) :
    IRequestHandler<CreateGoodsReceiptCommand, GoodsReceiptResponse>,
    IRequestHandler<GetGoodsReceiptsQuery, PagedResponse<GoodsReceiptResponse>>,
    IRequestHandler<GetGoodsReceiptQuery, GoodsReceiptResponse>,
    IRequestHandler<PostGoodsReceiptCommand, GoodsReceiptResponse>,
    IRequestHandler<CancelGoodsReceiptCommand, GoodsReceiptResponse>
{
    public async Task<GoodsReceiptResponse> Handle(CreateGoodsReceiptCommand request, CancellationToken cancellationToken)
    {
        await createValidator.ValidateAndThrowAsync(request, cancellationToken);
        return await operations.CreateAsync(request, cancellationToken);
    }

    public async Task<PagedResponse<GoodsReceiptResponse>> Handle(GetGoodsReceiptsQuery request, CancellationToken cancellationToken)
    {
        await listValidator.ValidateAndThrowAsync(request, cancellationToken);
        return await operations.GetAllAsync(request, cancellationToken);
    }

    public Task<GoodsReceiptResponse> Handle(GetGoodsReceiptQuery request, CancellationToken cancellationToken) =>
        operations.GetByIdAsync(request.Id, cancellationToken);
    public Task<GoodsReceiptResponse> Handle(PostGoodsReceiptCommand request, CancellationToken cancellationToken) =>
        operations.PostAsync(request.Id, request.OperatorId, cancellationToken);
    public Task<GoodsReceiptResponse> Handle(CancelGoodsReceiptCommand request, CancellationToken cancellationToken) =>
        operations.CancelAsync(request.Id, cancellationToken);
}
