using FluentValidation;
using SmartPool.Application.Common.Helpers;

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
                .MaximumLength(30).WithMessage("Tên loại vé không được vượt quá 30 ký tự.")
                .Matches(@"\A[\p{L}\p{M}\p{Nd} ()]+\z").WithMessage("Tên loại vé chỉ được chứa chữ, số, khoảng trắng và dấu ngoặc ().");

            RuleFor(x => x.TicketCategory)
                .IsInEnum().WithMessage("Phân loại vé không hợp lệ.");

            RuleFor(x => x.Price)
                .GreaterThan(0).WithMessage("Giá vé phải lớn hơn 0.");

            RuleFor(x => x.DurationDays)
                .Must((command, days) => !days.HasValue || days == TicketValidityPolicy.GetDurationDays(command.TicketCategory))
                .When(x => Enum.IsDefined(x.TicketCategory))
                .WithMessage("Vé tháng có hiệu lực 30 ngày; vé lượt có hiệu lực 24 giờ. Không được tùy chỉnh thời hạn.");
        }
    }
}
