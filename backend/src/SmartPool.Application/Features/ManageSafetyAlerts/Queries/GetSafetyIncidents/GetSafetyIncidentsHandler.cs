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
        var allIncidents = (await _incidentRepo.GetAllAsync(cancellationToken)).ToList();
        var allUsers = (await _userRepo.GetAllAsync(cancellationToken)).ToDictionary(u => u.Id, u => u);
        var allProfiles = (await _profileRepo.GetAllAsync(cancellationToken)).ToDictionary(p => p.UserId, p => p);

        // Fallback demo data nếu DB chưa có bản ghi
        if (!allIncidents.Any())
        {
            return BuildDemoResult(request);
        }

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

            string zone = ResolveZone(i.Title, i.Description);
            bool isFromCamera = !string.IsNullOrWhiteSpace(i.ImageIncidents);
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

        // Thống kê tổng
        int total = dtos.Count;
        int pending = dtos.Count(x => x.Status == "Pending");
        int inProgress = dtos.Count(x => x.Status == "In Progress");
        int resolved = dtos.Count(x => x.Status == "Resolved");
        int emergency = dtos.Count(x => string.Equals(x.SeverityLevel, "EMERGENCY", StringComparison.OrdinalIgnoreCase));
        int cameraCount = dtos.Count(x => !string.IsNullOrWhiteSpace(x.ImageIncidents) || x.SourceType.Contains("Camera"));
        int lifeguardCount = total - cameraCount;

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
        return "Bể lớn";
    }

    private static string NormalizeStatus(string? status)
    {
        if (string.IsNullOrWhiteSpace(status)) return "Resolved";
        var s = status.Trim().ToLowerInvariant();
        if (s is "resolved" or "đã giải quyết" or "đã xử lý" or "completed" or "đã ghi nhận")
            return "Resolved";
        if (s is "in_progress" or "in progress" or "đang xử lý" or "processing")
            return "In Progress";
        return "Resolved";
    }

    private static GetSafetyIncidentsResult BuildDemoResult(GetSafetyIncidentsQuery request)
    {
        var now = DateTime.UtcNow;
        var demo = new List<SafetyIncidentDto>
        {
            new()
            {
                Id = Guid.NewGuid(),
                IncidentCode = "INC-2026-001",
                Title = "Cảnh báo cứu hộ: Bể lớn",
                Zone = "Bể lớn",
                SeverityLevel = "EMERGENCY",
                Status = "Resolved",
                Description = "Camera AI phát hiện vị trí khách có dấu hiệu đuối nước tại độ sâu 1.8m làn số 3",
                IncidentTime = now.AddMinutes(-10),
                ReportedByName = "Hệ thống AI Camera",
                ImageIncidents = "https://images.unsplash.com/photo-1576013551627-0cc20b96c2a7?q=80&w=800&auto=format&fit=crop",
                SourceType = "Camera AI giám sát",
                UpdatedAt = now.AddMinutes(-10)
            },
            new()
            {
                Id = Guid.NewGuid(),
                IncidentCode = "INC-2026-002",
                Title = "Cảnh báo cứu hộ: Bể trẻ em",
                Zone = "Bể trẻ em",
                SeverityLevel = "EMERGENCY",
                Status = "Resolved",
                Description = "Cứu hộ viên phát hiện trẻ nhỏ trượt ngã sát mép nước khu vực cầu trượt, đã tiếp cận sơ cứu tại chỗ",
                IncidentTime = now.AddHours(-1),
                ReportedByName = "Cứu hộ viên Nguyễn Văn An",
                ImageIncidents = null,
                SourceType = "Cứu hộ viên phát hiện thực tế",
                UpdatedAt = now.AddHours(-1)
            },
            new()
            {
                Id = Guid.NewGuid(),
                IncidentCode = "INC-2026-003",
                Title = "Cảnh báo an toàn: Bể lớn",
                Zone = "Bể lớn",
                SeverityLevel = "WARNING",
                Status = "Resolved",
                Description = "Camera AI giám sát phát hiện người bơi vi phạm ranh giới phao ngăn làn số 2",
                IncidentTime = now.AddHours(-3),
                ReportedByName = "Hệ thống AI Camera",
                ImageIncidents = "https://images.unsplash.com/photo-1519315901367-f34ff9154487?q=80&w=800&auto=format&fit=crop",
                SourceType = "Camera AI giám sát",
                UpdatedAt = now.AddHours(-3)
            },
            new()
            {
                Id = Guid.NewGuid(),
                IncidentCode = "INC-2026-004",
                Title = "Cảnh báo an toàn: Bể trẻ em",
                Zone = "Bể trẻ em",
                SeverityLevel = "WARNING",
                Status = "Resolved",
                Description = "Khách đông đùa nghịch quá giới hạn tại chân cầu trượt, cứu hộ viên đã thổi còi nhắc nhở",
                IncidentTime = now.AddHours(-6),
                ReportedByName = "Giám sát viên Lê Thị Thảo",
                ImageIncidents = null,
                SourceType = "Cứu hộ viên phát hiện thực tế",
                UpdatedAt = now.AddHours(-6)
            },
            new()
            {
                Id = Guid.NewGuid(),
                IncidentCode = "INC-2026-005",
                Title = "Cảnh báo cứu hộ: Bể lớn",
                Zone = "Bể lớn",
                SeverityLevel = "EMERGENCY",
                Status = "Resolved",
                Description = "Cứu hộ viên phát hiện khách bơi có triệu chứng chuột rút giữa lòng bể, đã hỗ trợ phao vào bờ",
                IncidentTime = now.AddDays(-1),
                ReportedByName = "Cứu hộ viên Trần Đình Trọng",
                ImageIncidents = null,
                SourceType = "Cứu hộ viên phát hiện thực tế",
                UpdatedAt = now.AddDays(-1)
            }
        };

        return new GetSafetyIncidentsResult
        {
            Items = demo,
            TotalCount = demo.Count,
            PageNumber = request.PageNumber,
            PageSize = request.PageSize,
            TotalIncidents = demo.Count,
            PendingCount = 0,
            InProgressCount = 0,
            ResolvedCount = demo.Count,
            EmergencyCount = demo.Count(x => x.SeverityLevel == "EMERGENCY"),
            CameraCount = demo.Count(x => !string.IsNullOrWhiteSpace(x.ImageIncidents)),
            LifeguardCount = demo.Count(x => string.IsNullOrWhiteSpace(x.ImageIncidents)),
        };
    }
}
