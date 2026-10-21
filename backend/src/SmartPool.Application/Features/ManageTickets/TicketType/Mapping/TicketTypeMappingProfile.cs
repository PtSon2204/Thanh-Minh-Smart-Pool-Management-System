using AutoMapper;
using SmartPool.Application.Features.ManageTickets.TicketType.Commands.CreateTicketType;
using SmartPool.Application.Features.ManageTickets.TicketType.Commands.UpdateTicketType;
using SmartPool.Application.Features.ManageTickets.TicketType.Queries.GetAllTicketTypes;
using SmartPool.Application.Features.ManageTickets.TicketType.Queries.GetTicketTypeById;
using SmartPool.Domain.Enums;
using TicketTypeEntity = SmartPool.Domain.Entities.TicketType;

namespace SmartPool.Application.Features.ManageTickets.TicketType.Mapping
{
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

            // Entity → GetTicketTypeByIdResponse
            CreateMap<TicketTypeEntity, GetTicketTypeByIdResponse>()
                .ForMember(dest => dest.TicketCategory,
                    opt => opt.MapFrom(src => Enum.Parse<TicketCategoryEnum>(src.TicketCategory)))
                .ForMember(dest => dest.IsActive,
                    opt => opt.MapFrom(src => src.IsActive ?? true));

            // Entity → UpdateTicketTypeResponse
            CreateMap<TicketTypeEntity, UpdateTicketTypeResponse>()
                .ForMember(dest => dest.TicketCategory,
                    opt => opt.MapFrom(src => Enum.Parse<TicketCategoryEnum>(src.TicketCategory)))
                .ForMember(dest => dest.IsActive,
                    opt => opt.MapFrom(src => src.IsActive ?? true));
        }
    }
}

