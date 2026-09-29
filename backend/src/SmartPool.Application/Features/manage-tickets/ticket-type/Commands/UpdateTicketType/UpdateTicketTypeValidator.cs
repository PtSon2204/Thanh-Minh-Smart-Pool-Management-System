using FluentValidation;

namespace SmartPool.Application.Features.ManageTickets.TicketType.Commands.UpdateTicketType
{
    public class UpdateTicketTypeValidator : AbstractValidator<UpdateTicketTypeCommand>
    {
        public UpdateTicketTypeValidator()
        {
            RuleFor(x => x.Id)
                .NotEmpty().WithMessage("Id loại vé không được để trống.");

            RuleFor(x => x.Name)
                .NotEmpty().WithMessage("Tên loại vé không được để trống.")
                .MaximumLength(255).WithMessage("Tên loại vé không được vượt quá 255 ký tự.");

            RuleFor(x => x.TicketCategory)
                .IsInEnum().WithMessage("Phân loại vé không hợp lệ.");

            RuleFor(x => x.Price)
                .GreaterThan(0).WithMessage("Giá vé phải lớn hơn 0.");

            RuleFor(x => x.DurationDays)
                .GreaterThan(0).When(x => x.DurationDays.HasValue)
                .WithMessage("Số ngày hiệu lực phải lớn hơn 0 nếu được nhập.");
        }
    }
}
