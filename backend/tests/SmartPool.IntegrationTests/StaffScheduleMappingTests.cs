using AutoMapper;
using Microsoft.Extensions.Logging.Abstractions;
using SmartPool.Application.Features.ManageStaffs.Mapping;
using SmartPool.Application.Features.ManageStaffs.Queries.GetSchedule;
using SmartPool.Domain.Entities;

namespace SmartPool.IntegrationTests;

public sealed class StaffScheduleMappingTests
{
    [Fact]
    public void GivenScheduleEmployee_WhenMapping_ThenFullNameUsesUsernameAsFallback()
    {
        var mapper = new MapperConfiguration(configuration => configuration.AddProfile<StaffMappingProfile>(), NullLoggerFactory.Instance).CreateMapper();
        var profileName = mapper.Map<GetScheduleResponse>(CreateAssignment("Nguyễn Minh", "minh-user"));
        var usernameFallback = mapper.Map<GetScheduleResponse>(CreateAssignment(null, "minh-user"));

        Assert.Equal("Nguyễn Minh", profileName.EmployeeName);
        Assert.Equal("minh-user", usernameFallback.EmployeeName);
    }

    private static EmployeeShift CreateAssignment(string? fullName, string username) => new()
    {
        Employee = new Employee
        {
            User = new User
            {
                Username = username,
                UserProfile = fullName is null ? null : new UserProfile { FullName = fullName }
            }
        },
        Shift = new Shift { Name = "Ca sáng", StartTime = new TimeOnly(8, 0), EndTime = new TimeOnly(12, 0) }
    };
}
