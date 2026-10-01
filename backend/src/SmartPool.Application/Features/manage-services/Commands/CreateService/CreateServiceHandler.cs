using AutoMapper;
using FluentValidation;
using MediatR;
using SmartPool.Application.Interfaces.Repositories;
using SmartPool.Domain.Entities;

namespace SmartPool.Application.Features.ManageServices.Commands.CreateService
{
    public sealed class CreateServiceHandler : IRequestHandler<CreateServiceCommand, CreateServiceResponse>
    {
        private readonly IRepository<Product> _repository;
        private readonly IUnitOfWork _unitOfWork;
        private readonly IMapper _mapper;
        private readonly IValidator<CreateServiceCommand> _validator;

        public CreateServiceHandler(IRepository<Product> repository, IUnitOfWork unitOfWork, IMapper mapper, IValidator<CreateServiceCommand> validator)
        {
            _repository = repository;
            _unitOfWork = unitOfWork;
            _mapper = mapper;
            _validator = validator;
        }

        public async Task<CreateServiceResponse> Handle(CreateServiceCommand request, CancellationToken cancellationToken)
        {
            var validation = await _validator.ValidateAsync(request, cancellationToken);
            if (!validation.IsValid)
            {
                throw new ValidationException(validation.Errors);
            }

            var product = new Product
            {
                Id = Guid.NewGuid(),
                Name = request.Name.Trim(),
                Type = request.Type,
                Price = request.Price,
                StockQuantity = request.StockQuantity,
                IsActive = request.IsActive,
                IsDeleted = false,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };

            await _repository.AddAsync(product, cancellationToken);
            await _unitOfWork.SaveChangesAsync(cancellationToken);
            return _mapper.Map<CreateServiceResponse>(product);
        }
    }
}
