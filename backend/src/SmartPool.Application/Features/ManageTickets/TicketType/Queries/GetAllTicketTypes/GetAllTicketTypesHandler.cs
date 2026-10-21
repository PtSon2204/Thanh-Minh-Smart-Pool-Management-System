using AutoMapper;
using MediatR;
using SmartPool.Application.Common.Models;
using SmartPool.Application.Interfaces.Repositories;
using SmartPool.Domain.Enums;

namespace SmartPool.Application.Features.ManageTickets.TicketType.Queries.GetAllTicketTypes
{
    public class GetAllTicketTypesHandler : IRequestHandler<GetAllTicketTypesQuery, PagedResponse<GetAllTicketTypesResponse>>
    {
        private readonly ITicketTypeOperations _operations;
        private readonly IMapper _mapper;

        public GetAllTicketTypesHandler(
            ITicketTypeOperations operations,
            IMapper mapper)
        {
            _operations = operations;
            _mapper = mapper;
        }

        public async Task<PagedResponse<GetAllTicketTypesResponse>> Handle(
            GetAllTicketTypesQuery request,
            CancellationToken cancellationToken)
        {
            var categoryString = request.Category?.ToString();

            var (items, totalCount) = await _operations.GetPagedTicketTypesAsync(
                request.SearchTerm,
                categoryString,
                request.IsActive,
                request.PageIndex,
                request.PageSize,
                cancellationToken);

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

