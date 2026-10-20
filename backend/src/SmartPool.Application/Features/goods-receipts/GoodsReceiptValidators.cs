using FluentValidation;

namespace SmartPool.Application.Features.GoodsReceipts;

public sealed class CreateGoodsReceiptValidator : AbstractValidator<CreateGoodsReceiptCommand>
{
    public CreateGoodsReceiptValidator()
    {
        RuleFor(x => x.Id).NotEmpty();
        RuleFor(x => x.OperatorId).NotEmpty();
        RuleFor(x => x.ReceiptCode).NotEmpty().MaximumLength(50)
            .Must(code => !string.IsNullOrWhiteSpace(code)).WithMessage("Nhập mã phiếu nhập.");
        RuleFor(x => x.ReceivedAt).NotEmpty().Must(value => value.Kind == DateTimeKind.Utc)
            .WithMessage("Thời điểm nhập hàng phải có múi giờ UTC (Z).");
        RuleFor(x => x.SupplierName).MaximumLength(255);
        RuleFor(x => x.Note).MaximumLength(2000);
        RuleFor(x => x.Details).NotNull().Must(lines => lines is { Count: > 0 and <= 100 })
            .WithMessage("Phiếu phải có từ 1 đến 100 dòng hàng.");
        RuleFor(x => x.Details).Must(lines => lines is null || lines.All(line => line is not null) &&
            lines.Select(line => line.ProductId).Distinct().Count() == lines.Count)
            .WithMessage("Mỗi sản phẩm chỉ được xuất hiện một lần trong phiếu.");
        RuleForEach(x => x.Details).NotNull().SetValidator(new GoodsReceiptLineValidator());
    }
}

public sealed class GoodsReceiptLineValidator : AbstractValidator<GoodsReceiptLine>
{
    public GoodsReceiptLineValidator()
    {
        RuleFor(x => x.ProductId).NotEmpty();
        RuleFor(x => x.Quantity).GreaterThan(0).WithMessage("Số lượng nhập phải lớn hơn 0.");
        RuleFor(x => x.UnitCost).InclusiveBetween(0m, 9999999999999999.99m)
            .PrecisionScale(18, 2, true).WithMessage("Giá nhập phải không âm và có tối đa 2 chữ số thập phân.");
    }
}

public sealed class GetGoodsReceiptsValidator : AbstractValidator<GetGoodsReceiptsQuery>
{
    public GetGoodsReceiptsValidator()
    {
        RuleFor(x => x.PageIndex).InclusiveBetween(1, 1000000);
        RuleFor(x => x.PageSize).InclusiveBetween(1, 100);
        RuleFor(x => x.SearchTerm).MaximumLength(255);
        RuleFor(x => x.Status).Must(status => status is null or "Draft" or "Posted" or "Cancelled");
        RuleFor(x => x.ProductId).NotEqual(Guid.Empty).When(x => x.ProductId.HasValue);
    }
}
