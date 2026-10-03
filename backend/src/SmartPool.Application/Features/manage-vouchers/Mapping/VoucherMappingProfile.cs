using AutoMapper;
using SmartPool.Application.Features.ManageVouchers.Commands.CreateVoucher;
using SmartPool.Application.Features.ManageVouchers.Queries.GetAllVouchers;
using SmartPool.Domain.Entities;

namespace SmartPool.Application.Features.ManageVouchers.Mapping
{
    public sealed class VoucherMappingProfile : Profile
    {
        public VoucherMappingProfile()
        {
            CreateMap<Voucher, GetVouchersResponse>();
            // Note: CreateVoucherResponse typically doesn't map full entity properties, but if it needs to, we could.
            // Based on CreateVoucherResponse, we have Id, Code, Message.
            CreateMap<Voucher, CreateVoucherResponse>();
        }
    }
}
