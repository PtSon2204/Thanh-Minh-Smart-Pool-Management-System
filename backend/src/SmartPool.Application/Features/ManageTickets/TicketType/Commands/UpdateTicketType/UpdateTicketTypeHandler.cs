using AutoMapper;
using MediatR;
using SmartPool.Application.Common.Helpers;
using SmartPool.Application.Interfaces.Repositories;
using SmartPool.Domain.Enums;
using TicketTypeEntity = SmartPool.Domain.Entities.TicketType;

namespace SmartPool.Application.Features.ManageTickets.TicketType.Commands.UpdateTicketType
{
    public class UpdateTicketTypeHandler
        : IRequestHandler<UpdateTicketTypeCommand, UpdateTicketTypeResponse>
    {
        private readonly ITicketTypeOperations _operations;
        private readonly IMapper _mapper;

        public UpdateTicketTypeHandler(
            ITicketTypeOperations operations,
            IMapper mapper)
        {
            _operations = operations;
            _mapper = mapper;
        }

        public async Task<UpdateTicketTypeResponse> Handle(
            UpdateTicketTypeCommand request,
            CancellationToken cancellationToken)
        {
            var entity = await _operations.GetByIdAsync(request.Id, cancellationToken)
                ?? throw new KeyNotFoundException($"Không tìm thấy loại vé với Id: {request.Id}");

            int? totalEntries = request.TicketCategory == TicketCategoryEnum.VE_LUOT
                ? 1
                : null;

            // Cập nhật các trường
            entity.Name           = request.Name;
            entity.TicketCategory = request.TicketCategory.ToString();
            entity.Price          = request.Price;
            entity.TotalEntries   = totalEntries;
            entity.DurationDays   = TicketValidityPolicy.GetDurationDays(request.TicketCategory, totalEntries);
            entity.UpdatedAt      = DateTime.UtcNow;

            await _operations.UpdateAsync(entity, cancellationToken);

            return _mapper.Map<UpdateTicketTypeResponse>(entity);
        }
    }
}
