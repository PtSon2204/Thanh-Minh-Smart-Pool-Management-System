using FluentValidation;
using FluentValidation.Results;
using MediatR;
using SmartPool.Application.Common.Models;
using SmartPool.Application.Features.ManageServices.Commands.CheckoutRental;
using SmartPool.Application.Interfaces.Repositories;

namespace SmartPool.Application.Features.ManageServices.Payments
{
    public sealed class CreateRentalQrHandler : IRequestHandler<CreateRentalQrCommand, RentalPaymentStatusResponse>
    {
        private readonly IServiceOperations _operations;
        private readonly IValidator<CreateRentalQrCommand> _validator;

        public CreateRentalQrHandler(IServiceOperations operations, IValidator<CreateRentalQrCommand> validator)
        {
            _operations = operations;
            _validator = validator;
        }

        public async Task<RentalPaymentStatusResponse> Handle(CreateRentalQrCommand request, CancellationToken cancellationToken)
        {
            var validation = await _validator.ValidateAsync(request, cancellationToken);
            if (!validation.IsValid)
            {
                throw new ValidationException(validation.Errors);
            }

            return await _operations.CreateQrAsync(request, cancellationToken);
        }
    }

    public sealed class GetRentalPaymentHandler : IRequestHandler<GetRentalPaymentQuery, RentalPaymentStatusResponse>
    {
        private readonly IServiceOperations _operations;

        public GetRentalPaymentHandler(IServiceOperations operations)
        {
            _operations = operations;
        }

        public Task<RentalPaymentStatusResponse> Handle(GetRentalPaymentQuery request, CancellationToken cancellationToken)
        {
            return _operations.GetPaymentAsync(request.Id, cancellationToken);
        }
    }

    public sealed class ListRentalPaymentsHandler : IRequestHandler<ListRentalPaymentsQuery, PagedResponse<RentalPaymentStatusResponse>>
    {
        private readonly IServiceOperations _operations;
        private readonly IValidator<ListRentalPaymentsQuery> _validator;

        public ListRentalPaymentsHandler(IServiceOperations operations, IValidator<ListRentalPaymentsQuery> validator)
        {
            _operations = operations;
            _validator = validator;
        }

        public async Task<PagedResponse<RentalPaymentStatusResponse>> Handle(ListRentalPaymentsQuery request, CancellationToken cancellationToken)
        {
            var validation = await _validator.ValidateAsync(request, cancellationToken);
            if (!validation.IsValid)
            {
                throw new ValidationException(validation.Errors);
            }

            return await _operations.ListPaymentsAsync(request.PageIndex, request.PageSize, cancellationToken);
        }
    }

    public sealed class IssueRentalPaymentHandler : IRequestHandler<IssueRentalPaymentCommand, CheckoutRentalResponse>
    {
        private readonly IServiceOperations _operations;

        public IssueRentalPaymentHandler(IServiceOperations operations)
        {
            _operations = operations;
        }

        public Task<CheckoutRentalResponse> Handle(IssueRentalPaymentCommand request, CancellationToken cancellationToken)
        {
            EnsureOperator(request.OperatorId);
            return _operations.IssuePaymentAsync(request.Id, request.OperatorId, cancellationToken);
        }

        private static void EnsureOperator(Guid operatorId)
        {
            if (operatorId == Guid.Empty)
            {
                throw new ValidationException([new ValidationFailure("OperatorId", "Mã nhân viên không hợp lệ.")]);
            }
        }
    }

    public sealed class CancelRentalPaymentHandler : IRequestHandler<CancelRentalPaymentCommand, RentalPaymentStatusResponse>
    {
        private readonly IServiceOperations _operations;

        public CancelRentalPaymentHandler(IServiceOperations operations)
        {
            _operations = operations;
        }

        public Task<RentalPaymentStatusResponse> Handle(CancelRentalPaymentCommand request, CancellationToken cancellationToken)
        {
            EnsureOperator(request.OperatorId);
            return _operations.CancelPaymentAsync(request.Id, request.OperatorId, cancellationToken);
        }

        private static void EnsureOperator(Guid operatorId)
        {
            if (operatorId == Guid.Empty)
            {
                throw new ValidationException([new ValidationFailure("OperatorId", "Mã nhân viên không hợp lệ.")]);
            }
        }
    }
}
