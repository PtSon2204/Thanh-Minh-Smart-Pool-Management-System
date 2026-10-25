using AutoMapper;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using SmartPool.Application.Features.ManageStaffs.AttendanceQr;
using SmartPool.Application.Features.ManageStaffs.Mapping;
using SmartPool.Application.Features.ManageStaffs.Queries.GetShifts;
using SmartPool.Domain.Entities;
using SmartPool.Infrastructure.Persistence.DbContext;
using SmartPool.Infrastructure.Persistence.Repositories.Staff;

namespace SmartPool.IntegrationTests;

public sealed class FixedShiftListingTests
{
    [Fact]
    public async Task GivenFixedAndLegacyShifts_WhenListing_ThenOnlyFixedShiftIdsAreReturned()
    {
        var options = await IntegrationTestDatabase.CreateOptionsAsync();
        await using var context = new SmartPoolDbContext(options);
        var missingFixedIds = await context.Shifts.AsNoTracking()
            .Where(shift => FixedShifts.Ids.Contains(shift.Id))
            .Select(shift => shift.Id)
            .ToListAsync();
        var insertedFixedIds = FixedShifts.All.Where(item => !missingFixedIds.Contains(item.Id)).Select(item => item.Id).ToArray();
        var legacyId = Guid.NewGuid();

        try
        {
            context.Shifts.AddRange(FixedShifts.All.Where(item => insertedFixedIds.Contains(item.Id)).Select(item => new Shift
            {
                Id = item.Id,
                Name = item.Name,
                StartTime = item.StartTime,
                EndTime = item.EndTime,
                IsActive = true
            }));
            context.Shifts.Add(new Shift
            {
                Id = legacyId, Name = "QA legacy shift", StartTime = new TimeOnly(20, 0), EndTime = new TimeOnly(23, 0), IsActive = false
            });
            await context.SaveChangesAsync();

            var mapper = new MapperConfiguration(configuration => configuration.AddProfile<StaffMappingProfile>(),
                NullLoggerFactory.Instance).CreateMapper();
            var operations = new StaffOperations(context, mapper, TimeProvider.System,
                Options.Create(new StaffAttendanceQrSettings()));

            var result = await operations.GetShiftsAsync(new GetShiftsQuery(), CancellationToken.None);

            Assert.Equal(FixedShifts.Ids.Order(), result.Items.Select(item => item.Id).Order());
            Assert.DoesNotContain(result.Items, item => item.Id == legacyId);
        }
        finally
        {
            await context.Shifts.Where(shift => shift.Id == legacyId || insertedFixedIds.Contains(shift.Id)).ExecuteDeleteAsync();
        }
    }
}
