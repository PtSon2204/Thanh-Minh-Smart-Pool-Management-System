using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using SmartPool.Application.Features.ManagePayments.DTOs;
using SmartPool.Application.Interfaces.Repositories;

namespace SmartPool.Application.Features.ManagePayments.Queries.GetPaymentById
{
    public sealed class GetPaymentByIdHandler : IRequestHandler<GetPaymentByIdQuery, PaymentDetailDto>
    {
        private readonly IPaymentOperations _paymentOperations;

        public GetPaymentByIdHandler(IPaymentOperations paymentOperations)
        {
            _paymentOperations = paymentOperations;
        }

        public async Task<PaymentDetailDto> Handle(GetPaymentByIdQuery request, CancellationToken cancellationToken)
        {
            var payment = await _paymentOperations.GetPaymentByIdAsync(request.PaymentId, cancellationToken);
            if (payment == null)
            {
                throw new KeyNotFoundException($"Không tìm thấy giao dịch thanh toán với mã: {request.PaymentId}");
            }
            return payment;
        }
    }
}
