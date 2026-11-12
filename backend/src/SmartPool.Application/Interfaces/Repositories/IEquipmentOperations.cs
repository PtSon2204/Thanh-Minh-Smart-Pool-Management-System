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
    Task<Equipment?> GetByIdAsync(Guid id, CancellationToken cancellationToken);
    Task<List<Equipment>> GetAllActiveAsync(CancellationToken cancellationToken);
    Task AddAsync(Equipment equipment, CancellationToken cancellationToken);
    Task UpdateAsync(Equipment equipment, CancellationToken cancellationToken);
}
