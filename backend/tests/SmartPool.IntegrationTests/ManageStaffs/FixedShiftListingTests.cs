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

[CollectionDefinition(nameof(ShiftListingIsolation), DisableParallelization = true)]
public sealed class ShiftListingIsolation { }

[Collection(nameof(ShiftListingIsolation))]
public sealed class FixedShiftListingTests
{
    [Fact]
    public async Task GivenFixedAndCustomShifts_WhenListing_ThenReturnsBoth()
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

            const int pageSize = 100;
            var firstPage = await operations.GetShiftsAsync(
                new GetShiftsQuery { PageIndex = 1, PageSize = pageSize }, CancellationToken.None);
            var resultItems = firstPage.Items.ToList();
            var pageCount = (firstPage.TotalCount + pageSize - 1) / pageSize;
            for (var pageIndex = 2; pageIndex <= pageCount; pageIndex++)
            {
                var page = await operations.GetShiftsAsync(
                    new GetShiftsQuery { PageIndex = pageIndex, PageSize = pageSize }, CancellationToken.None);
                resultItems.AddRange(page.Items);
            }

            Assert.All(FixedShifts.Ids.Append(legacyId), id => Assert.Contains(resultItems, item => item.Id == id));
            Assert.Contains(resultItems, item => item.Id == legacyId && !item.IsActive);
        }
        finally
        {
            await context.Shifts.Where(shift => shift.Id == legacyId || insertedFixedIds.Contains(shift.Id)).ExecuteDeleteAsync();
        }
    }
}
