using MediatR;
using SmartPool.Application.Features.ManagePayments.DTOs;

namespace SmartPool.Application.Features.ManagePayments.Queries.GetPaymentById
{
    public sealed class GetPaymentByIdQuery : IRequest<PaymentDetailDto>
    {
        public Guid PaymentId { get; set; }
    }
}
