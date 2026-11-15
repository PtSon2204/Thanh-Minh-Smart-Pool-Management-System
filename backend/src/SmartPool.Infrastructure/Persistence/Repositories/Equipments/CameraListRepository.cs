using Microsoft.EntityFrameworkCore;
using SmartPool.Application.Common.Models;
using SmartPool.Application.Features.ManageEquipments;
using SmartPool.Application.Features.ManageEquipments.Queries.GetCameraList;
using SmartPool.Domain.Entities;
using SmartPool.Infrastructure.Persistence.DbContext;

namespace SmartPool.Infrastructure.Persistence.Repositories.Equipments;

public sealed class CameraListRepository(SmartPoolDbContext db) : ICameraListRepository
{
    // Category is the existing stable equipment discriminator. There is no area entity/UUID in this schema.
    private IQueryable<Equipment> Cameras => db.Equipments.AsNoTracking()
        .Where(x => x.IsDeleted != true && x.Category.Trim().ToLower() == "camera");

    public async Task<PagedResponse<CameraListItem>> ListAsync(GetCameraListQuery request, CancellationToken ct)
    {
        var query = Cameras;
        if (request.CameraId is Guid id) query = query.Where(x => x.Id == id);
        if (!string.IsNullOrWhiteSpace(request.Search))
        {
            var term = request.Search.Trim().Replace("\\", "\\\\").Replace("%", "\\%").Replace("_", "\\_");
            var pattern = $"%{term}%";
            query = query.Where(x => EF.Functions.ILike(x.Name, pattern, "\\") ||
                x.Location != null && EF.Functions.ILike(x.Location, pattern, "\\"));
        }
        if (!string.IsNullOrWhiteSpace(request.Status)) query = query.Where(x => x.Status == request.Status);
        if (!string.IsNullOrWhiteSpace(request.Area)) query = query.Where(x => x.Location == request.Area);
        var count = await query.CountAsync(ct);
        var rows = await query.OrderBy(x => x.Name).ThenBy(x => x.Id)
            .Skip((request.Page - 1) * request.PageSize).Take(request.PageSize)
            .Select(x => new { x.Id, x.Name, x.Location, x.Status, x.Metadata }).ToListAsync(ct);
        return new PagedResponse<CameraListItem>
        {
            Items = rows.Select(x => new CameraListItem { Id = x.Id, Name = x.Name, Location = x.Location,
                Status = x.Status, Source = CameraMetadata.IsBrowserCamera(x.Metadata) ? "Browser" : "RTSP" }).ToList(),
            TotalCount = count, PageIndex = request.Page, PageSize = request.PageSize
        };
    }

    public Task<string[]> AreasAsync(CancellationToken ct) => Cameras.Where(x => x.Location != null && x.Location != "")
        .Select(x => x.Location!).Distinct().OrderBy(x => x).ToArrayAsync(ct);
}
