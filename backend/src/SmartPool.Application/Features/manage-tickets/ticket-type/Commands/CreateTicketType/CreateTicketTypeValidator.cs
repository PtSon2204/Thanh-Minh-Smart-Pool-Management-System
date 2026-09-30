using FluentValidation;
using SmartPool.Domain.Enums;

namespace SmartPool.Application.Features.ManageTickets.TicketType.Commands.CreateTicketType
{
    public class CreateTicketTypeValidator : AbstractValidator<CreateTicketTypeCommand>
    {
        public CreateTicketTypeValidator()
        {
            RuleFor(x => x.Name)
                .NotEmpty().WithMessage("Tên loại vé không được để trống.")
                .MaximumLength(255).WithMessage("Tên loại vé không được vượt quá 255 ký tự.");

            RuleFor(x => x.TicketCategory)
                .IsInEnum().WithMessage("Phân loại vé không hợp lệ. Chấp nhận: VE_THANG, VE_LUOT, VE_THUONG.");

            RuleFor(x => x.Price)
                .GreaterThan(0).WithMessage("Giá vé phải lớn hơn 0.");

            RuleFor(x => x.DurationDays)
                .GreaterThan(0).When(x => x.DurationDays.HasValue)
                .WithMessage("Số ngày hiệu lực phải lớn hơn 0 nếu được nhập.");
        }
    }
}
