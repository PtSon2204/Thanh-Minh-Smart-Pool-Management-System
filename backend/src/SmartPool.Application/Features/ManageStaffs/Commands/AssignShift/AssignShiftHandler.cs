using FluentValidation;
using MediatR;
using SmartPool.Application.Interfaces.Repositories;
using SmartPool.Application.Interfaces.Services;

namespace SmartPool.Application.Features.ManageStaffs.Commands.AssignShift
{
    public sealed class AssignShiftHandler : IRequestHandler<AssignShiftCommand, AssignShiftResponse>
    {
        private readonly IStaffOperations operations;
        private readonly IValidator<AssignShiftCommand> validator;
        private readonly INotificationService? notificationService;

        public AssignShiftHandler(
            IStaffOperations operations,
            IValidator<AssignShiftCommand> validator,
            INotificationService? notificationService = null)
        {
            this.operations = operations;
            this.validator = validator;
            this.notificationService = notificationService;
        }

        public async Task<AssignShiftResponse> Handle(AssignShiftCommand request, CancellationToken cancellationToken)
        {
            await validator.ValidateAndThrowAsync(request, cancellationToken);
            var result = await operations.AssignShiftAsync(request, cancellationToken);
            await StaffScheduleBroadcast.NotifyWaterQualityDutyChangedAsync(notificationService, "SHIFT_ASSIGNED", cancellationToken);
            return result;
        }
    }
}
