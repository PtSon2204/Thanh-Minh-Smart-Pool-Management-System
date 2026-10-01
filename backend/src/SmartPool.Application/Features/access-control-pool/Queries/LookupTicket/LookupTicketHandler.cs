using FluentValidation;
using MediatR;
using SmartPool.Application.Features.AccessControlPool.Contracts;
using SmartPool.Application.Interfaces.Repositories;

namespace SmartPool.Application.Features.AccessControlPool.Queries.LookupTicket
{
    public class LookupTicketHandler : IRequestHandler<LookupTicketQuery, PoolAccessValidationResult<LookupTicketResponse>>
    {
        private readonly IPoolAccessOperations _operations;
        private readonly IValidator<LookupTicketQuery> _validator;

        public LookupTicketHandler(IPoolAccessOperations operations, IValidator<LookupTicketQuery> validator)
        {
            _operations = operations;
            _validator = validator;
        }

        public async Task<PoolAccessValidationResult<LookupTicketResponse>> Handle(LookupTicketQuery request, CancellationToken cancellationToken)
        {
            var code = request.Code?.Trim();
            var validation = await _validator.ValidateAsync(new LookupTicketQuery { Code = code }, cancellationToken);
            if (!validation.IsValid)
            {
                var failure = validation.Errors[0];
                return PoolAccessValidationResult<LookupTicketResponse>.Failure(failure.PropertyName, failure.ErrorMessage);
            }

            var result = await _operations.LookupAsync(code!, DateTime.UtcNow, cancellationToken);
            return PoolAccessValidationResult<LookupTicketResponse>.Success(result);
        }
    }
}
