using AutoMapper;
using SmartPool.Application.Interfaces.Repositories;
using SmartPool.Infrastructure.Persistence.DbContext;

namespace SmartPool.Infrastructure.Persistence.Repositories
{

public sealed partial class ServiceOperations : IServiceOperations
{
    private readonly SmartPoolDbContext _context;
    private readonly IMapper _mapper;

    public ServiceOperations(SmartPoolDbContext context, IMapper mapper)
    {
        _context = context;
        _mapper = mapper;
    }
}
}
