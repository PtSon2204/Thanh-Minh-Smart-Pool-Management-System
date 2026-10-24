using FluentValidation;
using MediatR;
using SmartPool.Application.Interfaces.Repositories;

namespace SmartPool.Application.Features.ManageServices.Commands.QuoteRental;

public sealed class QuoteRentalCommand : IRequest<QuoteRentalResponse>
{
    public List<QuoteRentalLine> Items { get; set; } = [];
}

public sealed class QuoteRentalLine
{
    public Guid ProductId { get; set; }
    public int Quantity { get; set; }
}

public sealed class QuoteRentalResponse
{
    public List<QuotedRentalLine> Items { get; set; } = [];
    public decimal DepositPerUnit { get; set; } = 20000m;
    public decimal RentalFeeTotal { get; set; }
    public decimal DepositTotal { get; set; }
    public decimal TotalToCollect { get; set; }
    public int TotalQuantity { get; set; }
}

public sealed class QuotedRentalLine
{
    public Guid ProductId { get; set; }
    public string ProductName { get; set; } = string.Empty;
    public int Quantity { get; set; }
    public decimal UnitPrice { get; set; }
    public decimal RentalFee { get; set; }
    public decimal DepositAmount { get; set; }
}

public sealed class QuoteRentalValidator : AbstractValidator<QuoteRentalCommand>
{
    public QuoteRentalValidator()
    {
        RuleFor(command => command.Items).NotNull().NotEmpty();
        RuleFor(command => command.Items).Must(items => items is null || (items.Count <= 20 && items.All(item => item is not null) && items.Sum(item => (long)item.Quantity) <= 100 && items.Select(item => item.ProductId).Distinct().Count() == items.Count))
            .WithMessage("Chọn các sản phẩm khác nhau, tối đa 100 đơn vị mỗi đơn.");
        When(command => command.Items is not null && command.Items.All(item => item is not null), () =>
            RuleForEach(command => command.Items).ChildRules(line =>
            {
                line.RuleFor(item => item.ProductId).NotEmpty();
                line.RuleFor(item => item.Quantity).InclusiveBetween(1, 100);
            }));
    }
}

public sealed class QuoteRentalHandler(IServiceOperations operations) : IRequestHandler<QuoteRentalCommand, QuoteRentalResponse>
{
    public Task<QuoteRentalResponse> Handle(QuoteRentalCommand request, CancellationToken cancellationToken) =>
        operations.QuoteRentalAsync(request, cancellationToken);
}
