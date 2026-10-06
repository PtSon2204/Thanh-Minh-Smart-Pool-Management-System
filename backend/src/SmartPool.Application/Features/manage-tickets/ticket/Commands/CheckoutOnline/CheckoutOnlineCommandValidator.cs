using FluentValidation;

namespace SmartPool.Application.Features.ManageTickets.Ticket.Commands.CheckoutOnline
{
    public class CheckoutOnlineCommandValidator : AbstractValidator<CheckoutOnlineCommand>
    {
        public CheckoutOnlineCommandValidator()
        {
            RuleFor(x => x.Items)
                .NotEmpty().WithMessage("Giỏ hàng không được để trống.");

            RuleForEach(x => x.Items).ChildRules(item =>
            {
                item.RuleFor(i => i.TicketTypeId).NotEmpty().WithMessage("TicketTypeId không hợp lệ.");
                item.RuleFor(i => i.Quantity).GreaterThan(0).WithMessage("Số lượng phải lớn hơn 0.");
            });
        }
    }
}
