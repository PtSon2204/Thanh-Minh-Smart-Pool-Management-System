using AutoMapper;
using SmartPool.Application.Features.ManageServices.Commands.CreateService;
using SmartPool.Application.Features.ManageServices.Commands.UpdateService;
using SmartPool.Application.Features.ManageServices.Queries.GetServices;
using SmartPool.Domain.Entities;

namespace SmartPool.Application.Features.ManageServices.Mapping
{
    public sealed class ServiceMappingProfile : Profile
    {
        public ServiceMappingProfile()
        {
            CreateMap<Product, GetServicesResponse>().ForMember(destination => destination.IsActive, options => options.MapFrom(source => source.IsActive ?? false));
            CreateMap<Product, CreateServiceResponse>().ForMember(destination => destination.IsActive, options => options.MapFrom(source => source.IsActive ?? false));
            CreateMap<Product, UpdateServiceResponse>().ForMember(destination => destination.IsActive, options => options.MapFrom(source => source.IsActive ?? false));
        }
    }
}
