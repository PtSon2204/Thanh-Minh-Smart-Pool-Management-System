using AutoMapper;
using FluentValidation;
using MediatR;
using SmartPool.Application.Common.Models;
using SmartPool.Application.Interfaces.Repositories;
using SmartPool.Domain.Entities;

namespace SmartPool.Application.Features.ManageServices.Queries.GetServices
{
    public sealed class GetServicesHandler : IRequestHandler<GetServicesQuery, PagedResponse<GetServicesResponse>>
    {
        private readonly IRepository<Product> _repository;
        private readonly IMapper _mapper;
        private readonly IValidator<GetServicesQuery> _validator;

        public GetServicesHandler(IRepository<Product> repository, IMapper mapper, IValidator<GetServicesQuery> validator)
        {
            _repository = repository;
            _mapper = mapper;
            _validator = validator;
        }

        public async Task<PagedResponse<GetServicesResponse>> Handle(GetServicesQuery request, CancellationToken cancellationToken)
        {
            var validation = await _validator.ValidateAsync(request, cancellationToken);
            if (!validation.IsValid)
            {
                throw new ValidationException(validation.Errors);
            }

            var products = await _repository.FindAsync(product => product.IsDeleted != true, cancellationToken);
            var query = products.AsQueryable();

            if (!string.IsNullOrWhiteSpace(request.SearchTerm))
            {
                var searchTerm = request.SearchTerm.Trim().ToLower();
                query = query.Where(product => product.Name.ToLower().Contains(searchTerm));
            }

            if (!string.IsNullOrWhiteSpace(request.Type))
            {
                query = query.Where(product => product.Type == request.Type);
            }

            if (request.IsActive.HasValue)
            {
                query = request.IsActive.Value
                    ? query.Where(product => product.IsActive == true || product.IsActive == null)
                    : query.Where(product => product.IsActive == false);
            }

            var totalCount = query.Count();
            var items = query.OrderByDescending(product => product.CreatedAt)
                .Skip((request.PageIndex - 1) * request.PageSize)
                .Take(request.PageSize)
                .ToList();

            return new PagedResponse<GetServicesResponse>
            {
                Items = _mapper.Map<List<GetServicesResponse>>(items),
                TotalCount = totalCount,
                PageIndex = request.PageIndex,
                PageSize = request.PageSize
            };
        }
    }
}
