using SmartPool.Application.Features.ManageEquipments.DTOs;
using SmartPool.Application.Common.Models;
using SmartPool.Application.Features.ManageEquipments.Queries.GetEquipments;
using SmartPool.Domain.Entities;

namespace SmartPool.Application.Interfaces.Repositories;

public interface IEquipmentOperations
{
    Task<PagedResponse<EquipmentListItem>> GetListAsync(GetEquipmentsQuery query, DateOnly today, CancellationToken cancellationToken);
    Task<EquipmentFilterOptions> GetFilterOptionsAsync(CancellationToken cancellationToken);
    Task RegisterCameraAsync(Equipment equipment, string identity, Action publish, CancellationToken cancellationToken);
    Task<bool> CameraExistsAsync(string identity, CancellationToken cancellationToken);
}
