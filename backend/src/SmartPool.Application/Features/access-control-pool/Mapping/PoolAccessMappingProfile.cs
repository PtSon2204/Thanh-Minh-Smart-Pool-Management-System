using AutoMapper;
using SmartPool.Application.Features.AccessControlPool.Contracts;
using SmartPool.Application.Features.AccessControlPool.Queries.GetEntryHistory;
using SmartPool.Domain.Entities;

namespace SmartPool.Application.Features.AccessControlPool.Mapping
{
    public class PoolAccessMappingProfile : Profile
    {
        public PoolAccessMappingProfile()
        {
            CreateMap<EntryLog, GetEntryHistoryResponse>()
                .ForMember(dest => dest.Reason, opt => opt.MapFrom(src => src.Message));
        }
    }
}
