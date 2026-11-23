using FluentValidation;
using MediatR;
using SmartPool.Application.Interfaces.Repositories;
using SmartPool.Application.Interfaces.Services;

namespace SmartPool.Application.Features.ManageStaffs.Commands.CancelShiftAssignment
{
    public sealed class CancelShiftAssignmentHandler : IRequestHandler<CancelShiftAssignmentCommand, CancelShiftAssignmentResponse>
    {
        private readonly IStaffOperations operations;
        private readonly IValidator<CancelShiftAssignmentCommand> validator;
        private readonly INotificationService? notificationService;

        public CancelShiftAssignmentHandler(
            IStaffOperations operations,
            IValidator<CancelShiftAssignmentCommand> validator,
            INotificationService? notificationService = null)
        {
            this.operations = operations;
            this.validator = validator;
            this.notificationService = notificationService;
        }

        public async Task<CancelShiftAssignmentResponse> Handle(CancelShiftAssignmentCommand request, CancellationToken cancellationToken)
        {
            await validator.ValidateAndThrowAsync(request, cancellationToken);
            var result = await operations.CancelAssignmentAsync(request, cancellationToken);
            await StaffScheduleBroadcast.NotifyWaterQualityDutyChangedAsync(notificationService, "SHIFT_CANCELLED", cancellationToken);
            return result;
        }
    }
}
