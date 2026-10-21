using SmartPool.Application.Features.ManageEquipments.DTOs;
using FluentValidation;
using MediatR;
using SmartPool.Application.Common.Models;
using SmartPool.Application.Common.Time;
using SmartPool.Application.Interfaces.Repositories;
using SmartPool.Application.Interfaces.Services;

namespace SmartPool.Application.Features.ManageEquipments.Queries.GetEquipmentFilters;

public sealed class GetEquipmentFiltersHandler(IEquipmentOperations repository)
    : IRequestHandler<GetEquipmentFiltersQuery, EquipmentFilterOptions>
{
    public Task<EquipmentFilterOptions> Handle(GetEquipmentFiltersQuery request, CancellationToken cancellationToken) =>
        repository.GetFilterOptionsAsync(cancellationToken);
}
