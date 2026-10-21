using FluentValidation;

namespace SmartPool.Application.Features.AccessControlPool.Queries.LookupTicket
{
    public class LookupTicketValidator : AbstractValidator<LookupTicketQuery>
    {
        public LookupTicketValidator()
        {
            RuleFor(query => query.Code)
                .NotEmpty().WithMessage("Mã vé không được để trống.")
                .Must(code => !string.IsNullOrWhiteSpace(code)).WithMessage("Mã vé không hợp lệ.")
                .MaximumLength(255).WithMessage("Mã vé không được vượt quá 255 ký tự.");
        }
    }
}
