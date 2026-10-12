using System;
using System.Threading;
using System.Threading.Tasks;
using SmartPool.Application.Common.Models;
using SmartPool.Application.Features.ManagePayments.DTOs;
using SmartPool.Application.Features.ManagePayments.Queries.GetAllPayments;
using SmartPool.Application.Features.ManagePayments.Queries.GetPaymentById;

namespace SmartPool.Application.Interfaces.Repositories
{
    public interface IPaymentOperations
    {
        Task<PagedResponse<PaymentSummaryDto>> GetAllPaymentsAsync(GetAllPaymentsQuery request, CancellationToken cancellationToken);
        Task<PaymentDetailDto?> GetPaymentByIdAsync(Guid paymentId, CancellationToken cancellationToken);
    }
}
