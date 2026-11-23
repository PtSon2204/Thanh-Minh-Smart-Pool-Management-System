using MediatR;
using SmartPool.Application.Features.ManageWaterQuality.Dtos;
using SmartPool.Application.Interfaces.Repositories;
using SmartPool.Application.Interfaces.Services;

namespace SmartPool.Application.Features.ManageWaterQuality.Queries.GetTodayWaterQualityGate;

public sealed record GetTodayWaterQualityGateQuery : IRequest<TodayWaterQualityGateDto>;

public sealed class GetTodayWaterQualityGateHandler : IRequestHandler<GetTodayWaterQualityGateQuery, TodayWaterQualityGateDto>
{
    private readonly IWaterQualityOperations _operations;
    private readonly ICurrentUser _currentUser;

    public GetTodayWaterQualityGateHandler(IWaterQualityOperations operations, ICurrentUser currentUser)
    {
        _operations = operations;
        _currentUser = currentUser;
    }

    public async Task<TodayWaterQualityGateDto> Handle(GetTodayWaterQualityGateQuery request, CancellationToken cancellationToken)
    {
        var isLifeguard = _currentUser.IsInRole("LIFEGUARD")
            || string.Equals(_currentUser.Role, "LIFEGUARD", StringComparison.OrdinalIgnoreCase);

        if (!_currentUser.UserId.HasValue
            || _currentUser.UserId.Value == Guid.Empty
            || !isLifeguard)
        {
            return new TodayWaterQualityGateDto(false, null, null, null);
        }

        return await _operations.GetTodayWaterQualityGateAsync(_currentUser.UserId.Value, cancellationToken);
    }
}
