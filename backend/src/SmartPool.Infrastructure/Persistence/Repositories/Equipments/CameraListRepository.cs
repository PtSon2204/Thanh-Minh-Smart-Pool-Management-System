using Microsoft.EntityFrameworkCore;
using System.Text.RegularExpressions;
using SmartPool.Application.Common.Models;
using SmartPool.Application.Features.ManageEquipments;
using SmartPool.Application.Features.ManageEquipments.Queries.GetCameraList;
using SmartPool.Domain.Entities;
using SmartPool.Domain.Enums;
using SmartPool.Infrastructure.Persistence.DbContext;

namespace SmartPool.Infrastructure.Persistence.Repositories.Equipments;

public sealed class CameraListRepository(SmartPoolDbContext db) : ICameraListRepository
{
    // Category is the existing stable equipment discriminator. Thiết bị đã khóa (Inactive) không đưa vào danh sách camera giám sát.
    private IQueryable<Equipment> Cameras => db.Equipments.AsNoTracking()
        .Where(x => x.IsDeleted != true && x.Status != EquipmentStatuses.Inactive && x.Category.Trim().ToLower() == "camera");

    public async Task<PagedResponse<CameraListItem>> ListAsync(GetCameraListQuery request, CancellationToken ct)
    {
        var query = Cameras;
        if (request.CameraId is Guid id) query = query.Where(x => x.Id == id);
        if (!string.IsNullOrWhiteSpace(request.Search))
        {
            var term = GetCameraListValidator.NormalizeSearch(request.Search);
            // Treat whitespace as optional between literal characters, in both
            // the entered term and stored name/location. Never interpret user regex.
            var pattern = string.Join(@"\s*", term.Select(c => Regex.Escape(c.ToString())));
            query = query.Where(x => Regex.IsMatch(x.Name, pattern, RegexOptions.IgnoreCase) ||
                x.Location != null && Regex.IsMatch(x.Location, pattern, RegexOptions.IgnoreCase));
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
