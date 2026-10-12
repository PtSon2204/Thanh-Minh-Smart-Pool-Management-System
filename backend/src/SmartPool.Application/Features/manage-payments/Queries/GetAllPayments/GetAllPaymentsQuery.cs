using MediatR;
using SmartPool.Application.Common.Models;
using SmartPool.Application.Features.ManagePayments.DTOs;

namespace SmartPool.Application.Features.ManagePayments.Queries.GetAllPayments
{
    public sealed class GetAllPaymentsQuery : IRequest<PagedResponse<PaymentSummaryDto>>
    {
        public int PageIndex { get; set; } = 1;
        public int PageSize { get; set; } = 20;
        public string? Status { get; set; }
        public string? PaymentMethod { get; set; }
        public DateTime? FromDate { get; set; }
        public DateTime? ToDate { get; set; }
        public string? Search { get; set; }
    }
}
