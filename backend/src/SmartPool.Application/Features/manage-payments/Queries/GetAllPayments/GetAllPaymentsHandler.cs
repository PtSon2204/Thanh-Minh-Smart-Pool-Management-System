using System.Threading;
using System.Threading.Tasks;
using MediatR;
using SmartPool.Application.Common.Models;
using SmartPool.Application.Features.ManagePayments.DTOs;
using SmartPool.Application.Interfaces.Repositories;

namespace SmartPool.Application.Features.ManagePayments.Queries.GetAllPayments
{
    public sealed class GetAllPaymentsHandler : IRequestHandler<GetAllPaymentsQuery, PagedResponse<PaymentSummaryDto>>
    {
        private readonly IPaymentOperations _paymentOperations;

        public GetAllPaymentsHandler(IPaymentOperations paymentOperations)
        {
            _paymentOperations = paymentOperations;
        }

        public async Task<PagedResponse<PaymentSummaryDto>> Handle(GetAllPaymentsQuery request, CancellationToken cancellationToken)
        {
            return await _paymentOperations.GetAllPaymentsAsync(request, cancellationToken);
        }
    }
}
