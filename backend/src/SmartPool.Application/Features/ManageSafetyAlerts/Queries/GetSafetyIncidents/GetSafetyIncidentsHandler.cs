using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using SmartPool.Application.Interfaces.Repositories;
using SmartPool.Domain.Entities;

namespace SmartPool.Application.Features.ManageSafetyAlerts.Queries.GetSafetyIncidents;

public sealed class GetSafetyIncidentsHandler : IRequestHandler<GetSafetyIncidentsQuery, GetSafetyIncidentsResult>
{
    private readonly IRepository<Incident> _incidentRepo;
    private readonly IRepository<User> _userRepo;
    private readonly IRepository<UserProfile> _profileRepo;

    public GetSafetyIncidentsHandler(
        IRepository<Incident> incidentRepo,
        IRepository<User> userRepo,
        IRepository<UserProfile> profileRepo)
    {
        _incidentRepo = incidentRepo;
        _userRepo = userRepo;
        _profileRepo = profileRepo;
    }

    public async Task<GetSafetyIncidentsResult> Handle(
        GetSafetyIncidentsQuery request,
        CancellationToken cancellationToken)
    {
        if (request.PageNumber is < 1 or > 100000 || request.PageSize is < 1 or > 100 ||
            request.SourceType is not (null or "camera" or "lifeguard") || request.FromDate > request.ToDate)
            throw new ArgumentException("Bộ lọc lịch sử sự cố không hợp lệ.");
        var allIncidents = (await _incidentRepo.GetAllAsync(cancellationToken)).ToList();
        var allUsers = (await _userRepo.GetAllAsync(cancellationToken)).ToDictionary(u => u.Id, u => u);
        var allProfiles = (await _profileRepo.GetAllAsync(cancellationToken)).ToDictionary(p => p.UserId, p => p);

        // Chuyển sang DTO
        var dtos = allIncidents.Select(i =>
        {
            string reportedName = "Nhân viên";
            if (i.ReportedBy.HasValue && allUsers.TryGetValue(i.ReportedBy.Value, out var u))
            {
                if (allProfiles.TryGetValue(u.Id, out var prof) && !string.IsNullOrWhiteSpace(prof.FullName))
                    reportedName = prof.FullName;
                else if (!string.IsNullOrWhiteSpace(u.Username))
                    reportedName = u.Username;
            }

            string zone = !string.IsNullOrWhiteSpace(i.Location) ? i.Location : ResolveZone(i.Title, i.Description);
            bool isFromCamera = i.WarningId.HasValue || i.CameraId.HasValue;
            string sourceType = isFromCamera ? "Camera AI giám sát" : "Cứu hộ viên phát hiện thực tế";

            return new SafetyIncidentDto
            {
                Id = i.Id,
                IncidentCode = $"INC-{i.Id.ToString()[..8].ToUpperInvariant()}",
                Title = i.Title,
                Zone = zone,
                SeverityLevel = i.SeverityLevel ?? "EMERGENCY",
                Status = NormalizeStatus(i.Status),
                Description = i.Description,
                IncidentTime = i.IncidentTime ?? i.CreatedAt ?? DateTime.UtcNow,
                ReportedByName = isFromCamera ? "Hệ thống AI Camera" : reportedName,
                ReportedByUserId = i.ReportedBy,
                ImageIncidents = i.ImageIncidents,
                SourceType = sourceType,
                UpdatedAt = i.UpdatedAt,
            };
        }).OrderByDescending(x => x.IncidentTime).ToList();

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

    private static string ResolveZone(string? title, string? desc)
    {
        var text = $"{title} {desc}".ToLowerInvariant();
        if (text.Contains("trẻ em") || text.Contains("be tre em"))
            return "Bể trẻ em";
        if (text.Contains("bể lớn") || text.Contains("be lon"))
            return "Bể lớn";
        if (text.Contains("bể chính") || text.Contains("be chinh") || text.Contains("bể bơi chính"))
            return "Bể lớn";
        return "Chưa xác định";
    }

    private static string NormalizeStatus(string? status)
    {
        if (string.IsNullOrWhiteSpace(status)) return "Pending";
        var s = status.Trim().ToLowerInvariant();
        if (s is "pending" or "chờ xử lý" or "đang chờ") return "Pending";
        if (s is "resolved" or "đã giải quyết" or "đã xử lý" or "completed" or "đã ghi nhận")
            return "Resolved";
        if (s is "in_progress" or "in progress" or "đang xử lý" or "processing")
            return "In Progress";
        return status;
    }
}
