using FluentValidation;
using MediatR;
using SmartPool.Application.Common.Models;
using SmartPool.Application.Features.ManageServices.Commands.CheckoutRental;
using System.Text.Json.Serialization;

namespace SmartPool.Application.Features.ManageServices.Payments
{
    public sealed record BankTransferEvent(long EventId, string AccountNumber, decimal Amount, string Reference, string? BankReferenceCode);

    public sealed record BankTransferResult(bool Success, string Message);

    public sealed class SePayOptions
    {
        public const string SectionName = "SePay";
        public string ApiKey { get; set; } = string.Empty;
        public string AccountNumber { get; set; } = string.Empty;
        public string BankId { get; set; } = string.Empty;
        public string AccountName { get; set; } = string.Empty;
        public bool IsConfigured => !string.IsNullOrWhiteSpace(ApiKey) && !string.IsNullOrWhiteSpace(AccountNumber) && !string.IsNullOrWhiteSpace(BankId);
    }

    public sealed class RentalSePayWebhookPayload
    {
        public long id { get; set; }
        public string? transferType { get; set; }
        public string? code { get; set; }
        public string? accountNumber { get; set; }
        public decimal transferAmount { get; set; }
        public string? content { get; set; }
        public string? referenceCode { get; set; }
    }

    public sealed class CreateRentalQrCommand : IRequest<RentalPaymentStatusResponse>
    {
        public Guid RequestId { get; set; }
        public List<CheckoutRentalLine> Items { get; set; } = [];
        public string? CustomerName { get; set; }
        public string? CustomerPhone { get; set; }
        [JsonIgnore]
        public Guid OperatorId { get; set; }
    }

    public sealed class GetRentalPaymentQuery : IRequest<RentalPaymentStatusResponse>
    {
        public Guid Id { get; set; }
    }

    public sealed class ListRentalPaymentsQuery : IRequest<PagedResponse<RentalPaymentStatusResponse>>
    {
        public int PageIndex { get; set; } = 1;
        public int PageSize { get; set; } = 20;
    }

    public sealed class IssueRentalPaymentCommand : IRequest<CheckoutRentalResponse>
    {
        public Guid Id { get; set; }
        [JsonIgnore]
        public Guid OperatorId { get; set; }
    }

    public sealed class CancelRentalPaymentCommand : IRequest<RentalPaymentStatusResponse>
    {
        public Guid Id { get; set; }
        [JsonIgnore]
        public Guid OperatorId { get; set; }
    }

    public sealed class RentalPaymentLine
    {
        public Guid ProductId { get; set; }
        public string Name { get; set; } = string.Empty;
        public int Quantity { get; set; }
        public decimal UnitPrice { get; set; }
    }

    public sealed class RentalPaymentStatusResponse
    {
        public Guid Id { get; set; }
        public Guid OrderId { get; set; }
        public string? CustomerName { get; set; }
        public string? CustomerPhone { get; set; }
        public string TransactionRef { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty;
        public decimal FeeAmount { get; set; }
        public decimal DepositAmount { get; set; }
        public decimal TotalToCollect { get; set; }
        public decimal PaidAmount { get; set; }
        public decimal RemainingAmount { get; set; }
        public decimal ExcessAmount { get; set; }
        public bool RequiresReconciliation { get; set; }
        public DateTime CreatedAtUtc { get; set; }
        public DateTime ExpiresAtUtc { get; set; }
        public DateTime ServerTimeUtc { get; set; }
        public string BankId { get; set; } = string.Empty;
        public string AccountNumber { get; set; } = string.Empty;
        public string AccountName { get; set; } = string.Empty;
        public List<RentalPaymentLine> Items { get; set; } = [];
        public CheckoutRentalResponse? IssuedResult { get; set; }
    }

    public sealed class CreateRentalQrValidator : AbstractValidator<CreateRentalQrCommand>
    {
        public CreateRentalQrValidator()
        {
            RuleFor(command => command.OperatorId).NotEmpty().WithMessage("Mã nhân viên không hợp lệ.");
            RuleFor(command => command.RequestId).NotEmpty();
            RuleFor(command => command.CustomerName).NotEmpty().MaximumLength(255).WithMessage("Nhập tên khách hàng hợp lệ.");
            RuleFor(command => command.CustomerPhone).NotEmpty().MaximumLength(20).WithMessage("Nhập số điện thoại khách hàng.");
            RuleFor(command => command.Items).NotNull().NotEmpty();
            RuleFor(command => command.Items).Must(items => items is null || (items.Count <= 20 && items.All(item => item is not null) && items.Sum(item => (long)item.Quantity) <= 100 && items.Select(item => item.ProductId).Distinct().Count() == items.Count))
                .WithMessage("Chọn các sản phẩm khác nhau, tối đa 100 đơn vị mỗi đơn.");
            When(command => command.Items is not null && command.Items.All(item => item is not null), () =>
                RuleForEach(command => command.Items).ChildRules(line =>
                {
                    line.RuleFor(item => item.ProductId).NotEmpty();
                    line.RuleFor(item => item.Quantity).InclusiveBetween(1, 100);
                    line.RuleFor(item => item.ExpectedUnitPrice).GreaterThanOrEqualTo(0);
                    line.RuleFor(item => item.ExpectedUnitPrice).Must(price => price == decimal.Truncate(price))
                        .WithMessage("Đơn giá thuê phải là số tiền đồng.");
                }));
        }
    }

    public sealed class ListRentalPaymentsValidator : AbstractValidator<ListRentalPaymentsQuery>
    {
        public ListRentalPaymentsValidator()
        {
            RuleFor(query => query.PageIndex).GreaterThan(0).WithMessage("Chỉ số trang phải lớn hơn 0.");
            RuleFor(query => query.PageSize).InclusiveBetween(1, 100).WithMessage("Kích thước trang phải từ 1 đến 100.");
        }
    }
}
