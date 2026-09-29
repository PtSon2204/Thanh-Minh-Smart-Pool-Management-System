using AutoMapper;
using SmartPool.Application.Features.ManageTickets.TicketType.Commands.CreateTicketType;
using SmartPool.Application.Features.ManageTickets.TicketType.Queries.GetAllTicketTypes;
using SmartPool.Domain.Enums;
using TicketTypeEntity = SmartPool.Domain.Entities.TicketType;

namespace SmartPool.Application.Features.ManageTickets.TicketType.Mapping
{
    /// <summary>
    /// AutoMapper Profile cho TicketType.
    /// Được scan tự động bởi AddAutoMapper() trong DependencyInjection.cs.
    /// </summary>
    public class TicketTypeMappingProfile : Profile
    {
        public TicketTypeMappingProfile()
        {
            // Entity → GetAllTicketTypesResponse
            CreateMap<TicketTypeEntity, GetAllTicketTypesResponse>()
                .ForMember(dest => dest.TicketCategory,
                    opt => opt.MapFrom(src => Enum.Parse<TicketCategoryEnum>(src.TicketCategory)))
                .ForMember(dest => dest.IsActive,
                    opt => opt.MapFrom(src => src.IsActive ?? true));

            // Entity → CreateTicketTypeResponse
            CreateMap<TicketTypeEntity, CreateTicketTypeResponse>()
                .ForMember(dest => dest.TicketCategory,
                    opt => opt.MapFrom(src => Enum.Parse<TicketCategoryEnum>(src.TicketCategory)))
                .ForMember(dest => dest.IsActive,
                    opt => opt.MapFrom(src => src.IsActive ?? true));
        }
    }
}

