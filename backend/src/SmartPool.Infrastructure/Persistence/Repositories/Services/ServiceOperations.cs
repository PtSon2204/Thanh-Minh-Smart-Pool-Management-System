using AutoMapper;
using Microsoft.Extensions.Options;
using SmartPool.Application.Features.ManageServices.Payments;
using SmartPool.Application.Interfaces.Repositories;
using SmartPool.Infrastructure.Persistence.DbContext;

namespace SmartPool.Infrastructure.Persistence.Repositories.Services
{

public sealed partial class ServiceOperations : IServiceOperations
{
    private readonly SmartPoolDbContext _context;
    private readonly IMapper _mapper;
    private readonly TimeProvider _clock;
    private readonly SePayOptions _paymentOptions;

    public ServiceOperations(SmartPoolDbContext context, IMapper mapper, TimeProvider clock, IOptions<SePayOptions> paymentOptions)
    {
        _context = context;
        _mapper = mapper;
        _clock = clock;
        _paymentOptions = paymentOptions.Value;
    }
}
}
