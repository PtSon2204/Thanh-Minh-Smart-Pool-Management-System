using FluentValidation;
using MediatR;
using SmartPool.Application.Features.AccessControlPool.Contracts;
using SmartPool.Application.Interfaces.Repositories;

namespace SmartPool.Application.Features.AccessControlPool.Commands.ConfirmEntry
{
    public class ConfirmEntryHandler : IRequestHandler<ConfirmEntryCommand, PoolAccessValidationResult<ConfirmEntryResponse>>
    {
        private readonly IPoolAccessOperations _operations;
        private readonly IValidator<ConfirmEntryCommand> _validator;

        public ConfirmEntryHandler(IPoolAccessOperations operations, IValidator<ConfirmEntryCommand> validator)
        {
            _operations = operations;
            _validator = validator;
        }

        public async Task<PoolAccessValidationResult<ConfirmEntryResponse>> Handle(ConfirmEntryCommand request, CancellationToken cancellationToken)
        {
            var code = request.Code?.Trim();
            var validationRequest = new ConfirmEntryCommand
            {
                Code = code,
                InputMode = request.InputMode,
                OperatorId = request.OperatorId,
                EntriesRequested = request.EntriesRequested
            };
            var validation = await _validator.ValidateAsync(validationRequest, cancellationToken);
            if (!validation.IsValid)
            {
                var failure = validation.Errors[0];
                return PoolAccessValidationResult<ConfirmEntryResponse>.Failure(failure.PropertyName, failure.ErrorMessage);
            }

            int entriesToDeduct = (request.EntriesRequested.HasValue && request.EntriesRequested.Value > 1) ? request.EntriesRequested.Value : 1;
            var result = entriesToDeduct > 1
                ? await _operations.ConfirmAsync(code!, request.InputMode!, request.OperatorId, entriesToDeduct, cancellationToken)
                : await _operations.ConfirmAsync(code!, request.InputMode!, request.OperatorId, cancellationToken);

            return PoolAccessValidationResult<ConfirmEntryResponse>.Success(result);
        }
    }
}
