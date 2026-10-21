using SmartPool.Application.Features.ManageEquipments;
using SmartPool.Application.Features.ManageEquipments.DTOs;
using Microsoft.EntityFrameworkCore;
using SmartPool.Application.Common.Models;
using SmartPool.Application.Features.ManageEquipments.Queries.GetEquipments;
using SmartPool.Application.Interfaces.Repositories;
using SmartPool.Domain.Enums;
using SmartPool.Domain.Entities;
using SmartPool.Application.Interfaces.Services;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using SmartPool.Infrastructure.Persistence.DbContext;

namespace SmartPool.Infrastructure.Persistence.Repositories.Equipments;

public sealed class EquipmentOperations(SmartPoolDbContext db) : IEquipmentOperations
{
    public Task<bool> CameraExistsAsync(string identity, CancellationToken cancellationToken)
    {
        var marker = JsonSerializer.Serialize(new { _camera = new { identity } });
        return db.Equipments.AsNoTracking().AnyAsync(x => x.IsDeleted != true && x.Metadata != null && EF.Functions.JsonContains(x.Metadata, marker), cancellationToken);
    }

    public async Task RegisterCameraAsync(Equipment equipment, string identity, Action publish, CancellationToken cancellationToken)
    {
        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        var lockKey = BitConverter.ToInt64(SHA256.HashData(Encoding.UTF8.GetBytes(identity)), 0);
        // Serializes duplicate checks across API instances without a schema change.
        await db.Database.ExecuteSqlInterpolatedAsync($"SELECT pg_advisory_xact_lock({lockKey})", cancellationToken);
        var marker = JsonSerializer.Serialize(new { _camera = new { identity } });
        if (await db.Equipments.AnyAsync(x => x.IsDeleted != true && x.Metadata != null && EF.Functions.JsonContains(x.Metadata, marker), cancellationToken))
            throw new CameraConnectionException("Duplicate", "Camera này đã được đăng ký trong hệ thống.");
        await db.Equipments.AddAsync(equipment, cancellationToken);
        await db.SaveChangesAsync(cancellationToken);
        publish(); // In-memory publication only: no network work inside this transaction.
        await transaction.CommitAsync(cancellationToken);
    }

    public async Task<PagedResponse<EquipmentListItem>> GetListAsync(GetEquipmentsQuery request, DateOnly today, CancellationToken cancellationToken)
    {
        var query = db.Equipments.AsNoTracking().Where(x => x.IsDeleted != true);
        if (!string.IsNullOrWhiteSpace(request.SearchTerm))
        {
            // Treat wildcard characters as literal search text.
            var term = request.SearchTerm.Trim().Replace("\\", "\\\\").Replace("%", "\\%").Replace("_", "\\_");
            var pattern = $"%{term}%";
            query = query.Where(x => EF.Functions.ILike(x.Name, pattern, "\\") ||
                (x.Location != null && EF.Functions.ILike(x.Location, pattern, "\\")));
        }
        if (!string.IsNullOrWhiteSpace(request.Category)) query = query.Where(x => x.Category == request.Category);
        if (!string.IsNullOrWhiteSpace(request.Location)) query = query.Where(x => x.Location == request.Location);
        if (!string.IsNullOrWhiteSpace(request.Status)) query = query.Where(x => x.Status == request.Status);

        var count = await query.CountAsync(cancellationToken);
        var items = await query.OrderBy(x => x.Name).ThenBy(x => x.Id)
            .Skip((request.PageIndex - 1) * request.PageSize).Take(request.PageSize)
            .Select(x => new EquipmentListItem
            {
                Id = x.Id, Name = x.Name, Category = x.Category, Location = x.Location,
                Metadata = x.Metadata, Status = x.Status,
                // Pending is the only upcoming schedule state defined by the current schema.
                // A correlated aggregate is translated to SQL; no per-device database calls.
                NextMaintenanceDate = x.MaintenanceSchedules
                    .Where(s => s.Status == "Pending" && s.PlannedDate >= today && !s.MaintenanceLogs.Any())
                    .Select(s => (DateOnly?)s.PlannedDate).Min()
            }).ToListAsync(cancellationToken);

        return new PagedResponse<EquipmentListItem>
        {
            Items = items, TotalCount = count, PageIndex = request.PageIndex, PageSize = request.PageSize
        };
    }

    public async Task<EquipmentFilterOptions> GetFilterOptionsAsync(CancellationToken cancellationToken)
    {
        var query = db.Equipments.AsNoTracking().Where(x => x.IsDeleted != true);
        var storedStatuses = await query.Where(x => x.Status != null && x.Status != "")
            .Select(x => x.Status!).Distinct().OrderBy(x => x).ToListAsync(cancellationToken);
        return new EquipmentFilterOptions
        {
            Categories = await query.Select(x => x.Category).Where(x => x != "").Distinct().OrderBy(x => x).ToListAsync(cancellationToken),
            Locations = await query.Where(x => x.Location != null && x.Location != "").Select(x => x.Location!).Distinct().OrderBy(x => x).ToListAsync(cancellationToken),
            Statuses = EquipmentStatuses.All.Concat(storedStatuses).Distinct(StringComparer.Ordinal).ToList()
        };
    }
}
