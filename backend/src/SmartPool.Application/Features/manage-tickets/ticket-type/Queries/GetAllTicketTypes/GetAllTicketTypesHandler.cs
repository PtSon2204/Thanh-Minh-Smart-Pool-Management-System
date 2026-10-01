using AutoMapper;
using MediatR;
using SmartPool.Application.Common.Models;
using SmartPool.Application.Interfaces.Repositories;
using SmartPool.Domain.Enums;

namespace SmartPool.Application.Features.ManageTickets.TicketType.Queries.GetAllTicketTypes
{
    public class GetAllTicketTypesHandler : IRequestHandler<GetAllTicketTypesQuery, PagedResponse<GetAllTicketTypesResponse>>
    {
        private readonly IRepository<SmartPool.Domain.Entities.TicketType> _repo;
        private readonly IMapper _mapper;

        public GetAllTicketTypesHandler(
            IRepository<SmartPool.Domain.Entities.TicketType> repo,
            IMapper mapper)
        {
            _repo = repo;
            _mapper = mapper;
        }

        public async Task<PagedResponse<GetAllTicketTypesResponse>> Handle(
            GetAllTicketTypesQuery request,
            CancellationToken cancellationToken)
        {
            var query = await _repo.FindAsync(t => t.IsDeleted != true, cancellationToken);
            var queryable = query.AsQueryable();

            queryable = queryable.Where(t => t.TicketCategory == TicketCategoryEnum.VE_THANG.ToString() || 
                                             t.TicketCategory == TicketCategoryEnum.VE_LUOT.ToString());

            if (!string.IsNullOrWhiteSpace(request.SearchTerm))
            {
                var search = request.SearchTerm.ToLower();
                queryable = queryable.Where(t => t.Name.ToLower().Contains(search));
            }

            if (request.Category.HasValue)
            {
                queryable = queryable.Where(t => t.TicketCategory == request.Category.Value.ToString());
            }

            if (request.IsActive.HasValue)
            {
                if (request.IsActive.Value)
                    queryable = queryable.Where(t => t.IsActive == true || t.IsActive == null); // null is considered active in map
                else
                    queryable = queryable.Where(t => t.IsActive == false);
            }

            var totalCount = queryable.Count();

            var items = queryable
                .OrderByDescending(t => t.CreatedAt)
                .Skip((request.PageIndex - 1) * request.PageSize)
                .Take(request.PageSize)
                .ToList();

            var mappedItems = _mapper.Map<List<GetAllTicketTypesResponse>>(items);

            return new PagedResponse<GetAllTicketTypesResponse>
            {
                Items = mappedItems,
                TotalCount = totalCount,
                PageIndex = request.PageIndex,
                PageSize = request.PageSize
            };
        }
    }
}

