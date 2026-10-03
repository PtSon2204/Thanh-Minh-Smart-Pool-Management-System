using AutoMapper;
using SmartPool.Application.Features.ManageVouchers.Commands.CreateVoucher;
using SmartPool.Application.Features.ManageVouchers.Commands.UpdateVoucher;
using SmartPool.Application.Features.ManageVouchers.Queries.GetAllVouchers;
using SmartPool.Domain.Entities;

namespace SmartPool.Application.Features.ManageVouchers.Mapping
{
    public sealed class VoucherMappingProfile : Profile
    {
        public VoucherMappingProfile()
        {
            CreateMap<Voucher, GetVouchersResponse>();
            CreateMap<Voucher, CreateVoucherResponse>();
            CreateMap<Voucher, UpdateVoucherResponse>();
        }
    }
}
