using FluentValidation;
using System.Text.RegularExpressions;

namespace SmartPool.Application.Features.ManageTickets.Ticket.Commands.UpdateTicketCode;

public class UpdateTicketCodeValidator : AbstractValidator<UpdateTicketCodeCommand>
{
    public UpdateTicketCodeValidator()
    {
        RuleFor(x => x.Code)
    .NotEmpty().WithMessage("Mã vé không được để trống.")
    .Length(3, 6).WithMessage("Mã vé phải có độ dài từ 3 đến 6 ký tự.")
    .Matches(@"^\d{3,6}$").WithMessage("Mã vé chỉ được chứa từ 3 đến 6 chữ số (0–9).");
    }
}
