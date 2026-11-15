using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using SmartPool.Application.Interfaces.Repositories;

namespace SmartPool.Application.Features.ManageSafetyAlerts.Queries.GetSafetyIncidents;

public sealed class GetSafetyIncidentsHandler : IRequestHandler<GetSafetyIncidentsQuery, GetSafetyIncidentsResult>
{
    private readonly ISafetyAlertOperations _operations;

    public GetSafetyIncidentsHandler(ISafetyAlertOperations operations)
    {
        _operations = operations;
    }

    public async Task<GetSafetyIncidentsResult> Handle(
        GetSafetyIncidentsQuery request,
        CancellationToken cancellationToken)
    {
        if (request.PageNumber is < 1 or > 100000 || request.PageSize is < 1 or > 100 ||
            request.SourceType is not (null or "camera" or "lifeguard") || request.FromDate > request.ToDate)
            throw new ArgumentException("Bộ lọc lịch sử sự cố không hợp lệ.");
        var allIncidents = await _operations.GetAllIncidentsAsync(cancellationToken);
        var allUsers = (await _operations.GetAllUsersAsync(cancellationToken)).ToDictionary(u => u.Id, u => u);
        var allProfiles = (await _operations.GetAllProfilesAsync(cancellationToken)).ToDictionary(p => p.UserId, p => p);

        var dtos = allIncidents.Select(i => SafetyIncidentMapper.ToDto(
            i,
            i.ReportedBy.HasValue && allUsers.TryGetValue(i.ReportedBy.Value, out var user) ? user : null,
            i.ReportedBy.HasValue && allProfiles.TryGetValue(i.ReportedBy.Value, out var profile) ? profile : null))
            .OrderByDescending(x => x.IncidentTime).ToList();

        // Lọc theo điều kiện tìm kiếm
        var queryable = dtos.AsEnumerable();

        if (!string.IsNullOrWhiteSpace(request.Search))
        {
            var s = request.Search.Trim().ToLowerInvariant();
            queryable = queryable.Where(x =>
                x.Title.ToLowerInvariant().Contains(s) ||
                (x.Description != null && x.Description.ToLowerInvariant().Contains(s)) ||
                x.ReportedByName.ToLowerInvariant().Contains(s) ||
                x.IncidentCode.ToLowerInvariant().Contains(s));
        }

        if (!string.IsNullOrWhiteSpace(request.Zone) && request.Zone != "Tất cả")
        {
            queryable = queryable.Where(x => string.Equals(x.Zone, request.Zone, StringComparison.OrdinalIgnoreCase));
        }

        if (!string.IsNullOrWhiteSpace(request.SeverityLevel) && request.SeverityLevel != "Tất cả")
        {
            queryable = queryable.Where(x => string.Equals(x.SeverityLevel, request.SeverityLevel, StringComparison.OrdinalIgnoreCase));
        }

        if (!string.IsNullOrWhiteSpace(request.Status) && request.Status != "Tất cả")
        {
            queryable = queryable.Where(x => string.Equals(x.Status, request.Status, StringComparison.OrdinalIgnoreCase));
        }

        if (request.SourceType is not null)
            queryable = queryable.Where(x => request.SourceType == "camera"
                ? x.SourceType == "Camera AI giám sát" : x.SourceType != "Camera AI giám sát");

        if (request.FromDate.HasValue)
        {
            queryable = queryable.Where(x => x.IncidentTime >= request.FromDate.Value);
        }

        if (request.ToDate.HasValue)
        {
            queryable = queryable.Where(x => x.IncidentTime <= request.ToDate.Value);
        }

        var filteredList = queryable.ToList();
        int filteredCount = filteredList.Count;

        // Thống kê trên toàn bộ kết quả đã lọc, trước phân trang
        int total = filteredList.Count;
        int pending = filteredList.Count(x => x.Status == "Pending");
        int inProgress = filteredList.Count(x => x.Status == "In Progress");
        int resolved = filteredList.Count(x => x.Status == "Resolved");
        int emergency = filteredList.Count(x => string.Equals(x.SeverityLevel, "EMERGENCY", StringComparison.OrdinalIgnoreCase));
        int cameraCount = filteredList.Count(x => x.SourceType.Contains("Camera"));
        int lifeguardCount = total - cameraCount;

        var pagedItems = filteredList
            .Skip((request.PageNumber - 1) * request.PageSize)
            .Take(request.PageSize)
            .ToList();

        return new GetSafetyIncidentsResult
        {
            Items = pagedItems,
            TotalCount = filteredCount,
            PageNumber = request.PageNumber,
            PageSize = request.PageSize,
            TotalIncidents = total,
            PendingCount = pending,
            InProgressCount = inProgress,
            ResolvedCount = resolved,
            EmergencyCount = emergency,
            CameraCount = cameraCount,
            LifeguardCount = lifeguardCount,
        };
    }
}
