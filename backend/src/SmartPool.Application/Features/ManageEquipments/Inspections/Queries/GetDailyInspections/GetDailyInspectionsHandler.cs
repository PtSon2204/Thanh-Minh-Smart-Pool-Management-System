using System.Text.Json;
using MediatR;
using SmartPool.Application.Common.Models;
using SmartPool.Application.Features.ManageEquipments.Inspections.DTOs;
using SmartPool.Application.Interfaces.Repositories;
using SmartPool.Domain.Entities;

namespace SmartPool.Application.Features.ManageEquipments.Inspections.Queries.GetDailyInspections;

public sealed class GetDailyInspectionsHandler(
    IRepository<DailyInspection> inspectionRepository,
    IRepository<User> userRepository)
    : IRequestHandler<GetDailyInspectionsQuery, PagedResponse<DailyInspectionDto>>
{
    public async Task<PagedResponse<DailyInspectionDto>> Handle(
        GetDailyInspectionsQuery request,
        CancellationToken cancellationToken)
    {
        var all = await inspectionRepository.FindAsync(x => true, cancellationToken);
        var query = all.AsQueryable();

        if (request.FromDate.HasValue)
            query = query.Where(x => x.InspectionDate >= request.FromDate.Value);

        if (request.ToDate.HasValue)
            query = query.Where(x => x.InspectionDate <= request.ToDate.Value);

        if (!string.IsNullOrWhiteSpace(request.OverallStatus))
            query = query.Where(x => x.OverallStatus == request.OverallStatus);

        var total = query.Count();
        var items = query
            .OrderByDescending(x => x.InspectionDate)
            .ThenByDescending(x => x.InspectionTime)
            .Skip((request.PageIndex - 1) * request.PageSize)
            .Take(request.PageSize)
            .ToList();

        // Fetch inspector names
        var inspectorIds = items.Select(x => x.InspectorId).Distinct().ToList();
        var users = (await userRepository.FindAsync(u => inspectorIds.Contains(u.Id), cancellationToken))
            .ToDictionary(
                u => u.Id,
                u => u.UserProfile?.FullName ?? u.Username ?? "Nhân viên");

        var dtos = items.Select(x =>
        {
            var parsedItems = ParseItems(x.ItemsJson);
            var shiftDef = x.ShiftId.HasValue ? FixedShifts.FindById(x.ShiftId.Value) : null;

            return new DailyInspectionDto
            {
                Id = x.Id,
                InspectorId = x.InspectorId,
                InspectorName = users.GetValueOrDefault(x.InspectorId, "Nhân viên"),
                ShiftId = x.ShiftId ?? Guid.Empty,
                ShiftName = shiftDef?.Name ?? "Chưa phân ca",
                InspectionDate = x.InspectionDate,
                InspectionTime = x.InspectionTime,
                OverallStatus = x.OverallStatus,
                TotalItems = parsedItems.Count,
                IssuesCount = parsedItems.Count(i => !i.IsPassed),
                Notes = x.Notes,
                Items = parsedItems
            };
        }).ToList();

        return new PagedResponse<DailyInspectionDto>
        {
            Items = dtos,
            TotalCount = total,
            PageIndex = request.PageIndex,
            PageSize = request.PageSize
        };
    }

    private static List<DailyInspectionItemDto> ParseItems(string itemsJson)
    {
        if (string.IsNullOrWhiteSpace(itemsJson)) return [];
        try
        {
            return JsonSerializer.Deserialize<List<DailyInspectionItemDto>>(itemsJson) ?? [];
        }
        catch
        {
            return [];
        }
    }
}
