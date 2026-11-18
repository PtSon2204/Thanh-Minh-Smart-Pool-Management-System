using Moq;
using SmartPool.Application.Features.ManageReports.Queries.GetIncidentStatistics;
using SmartPool.Application.Interfaces.Repositories;
using SmartPool.Domain.Entities;

namespace SmartPool.UnitTests.Features.ManageReports;

public sealed class GetIncidentStatisticsHandlerTests
{
    [Fact]
    public async Task Handle_ExcludesPendingVariants_AndMapsEmergencyToKhanCap()
    {
        var staffId = Guid.NewGuid();
        var now = DateTime.UtcNow;
        var reportOps = new Mock<IReportOperations>();
        reportOps.Setup(x => x.GetIncidentsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync([
                new Incident
                {
                    Id = Guid.NewGuid(), Title = "Cảnh báo cứu hộ: Bể lớn", SeverityLevel = "EMERGENCY",
                    Status = "Resolved", ReportedBy = staffId, IncidentTime = now.AddMinutes(-10),
                    CreatedAt = now.AddMinutes(-10), UpdatedAt = now.AddMinutes(-5),
                },
                new Incident
                {
                    Id = Guid.NewGuid(), Title = "Cứu hộ kỹ thuật", SeverityLevel = "WARNING",
                    Status = "In Progress", ReportedBy = staffId, IncidentTime = now.AddMinutes(-20),
                    CreatedAt = now.AddMinutes(-20), UpdatedAt = now.AddMinutes(-15),
                },
                new Incident { Id = Guid.NewGuid(), Title = "Pending English", SeverityLevel = "EMERGENCY", Status = "Pending", CreatedAt = now },
                new Incident { Id = Guid.NewGuid(), Title = "Pending Vietnamese", SeverityLevel = "EMERGENCY", Status = "chờ xử lý", CreatedAt = now },
                new Incident { Id = Guid.NewGuid(), Title = "Pending Null", SeverityLevel = "EMERGENCY", Status = null, CreatedAt = now },
            ]);
        reportOps.Setup(x => x.GetUsersAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync([new User { Id = staffId, Username = "lifeguard1" }]);
        reportOps.Setup(x => x.GetUserProfilesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync([new UserProfile { UserId = staffId, FullName = "Nguyễn Cứu Hộ" }]);

        var result = await new GetIncidentStatisticsHandler(reportOps.Object).Handle(new(), default);

        Assert.Equal(2, result.TotalIncidents);
        Assert.Equal(1, result.ResolvedIncidents);
        Assert.Equal(1, result.InProgressIncidents);
        Assert.Equal(2, result.RecentIncidents.Count);
        Assert.Contains(result.RecentIncidents, r => r.SeverityLevel == "Khẩn cấp" && r.Status == "Đã giải quyết");
        Assert.Contains(result.RecentIncidents, r => r.SeverityLevel == "Cảnh báo" && r.Status == "Đang xử lý");
        Assert.DoesNotContain(result.RecentIncidents, r => r.Title.StartsWith("Pending"));
    }
}
