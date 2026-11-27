using FluentValidation;
using MediatR;
using SmartPool.Application.Features.ManageStaffs.Commands.AssignShift;
using SmartPool.Application.Interfaces.Repositories;
using SmartPool.Application.Interfaces.Services;

namespace SmartPool.Application.Features.ManageStaffs.Commands.AssignShiftBatch
{
    public sealed class AssignShiftBatchHandler : IRequestHandler<AssignShiftBatchCommand, IReadOnlyList<AssignShiftResponse>>
    {
        private readonly IStaffOperations operations;
        private readonly IValidator<AssignShiftBatchCommand> validator;
        private readonly INotificationService? notificationService;

        public AssignShiftBatchHandler(
            IStaffOperations operations,
            IValidator<AssignShiftBatchCommand> validator,
            INotificationService? notificationService = null)
        {
            this.operations = operations;
            this.validator = validator;
            this.notificationService = notificationService;
        }

        public async Task<IReadOnlyList<AssignShiftResponse>> Handle(AssignShiftBatchCommand request, CancellationToken cancellationToken)
        {
            await validator.ValidateAndThrowAsync(request, cancellationToken);
            var result = await operations.AssignShiftBatchAsync(request, cancellationToken);
            await StaffScheduleBroadcast.NotifyWaterQualityDutyChangedAsync(notificationService, "SHIFT_BATCH_ASSIGNED", cancellationToken);
            return result;
        }
    }
}
