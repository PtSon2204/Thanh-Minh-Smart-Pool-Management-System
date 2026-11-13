using System.Text.Json;
using MediatR;
using SmartPool.Application.Common.Models;
using SmartPool.Application.Features.ManageEquipments.Inspections.DTOs;
using SmartPool.Application.Interfaces.Repositories;
using SmartPool.Domain.Entities;

namespace SmartPool.Application.Features.ManageEquipments.Inspections.Queries.GetDailyInspections;

public sealed class GetDailyInspectionsHandler(
    IDailyInspectionOperations inspectionOperations,
    IUserOperations userOperations)
    : IRequestHandler<GetDailyInspectionsQuery, PagedResponse<DailyInspectionDto>>
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public async Task<PagedResponse<DailyInspectionDto>> Handle(
        GetDailyInspectionsQuery request,
        CancellationToken cancellationToken)
    {
        var (items, total) = await inspectionOperations.GetPagedInspectionsAsync(
            request.FromDate, request.ToDate, request.OverallStatus,
            request.PageIndex, request.PageSize, cancellationToken);

        // Fetch inspector names
        var inspectorIds = items.Select(x => x.InspectorId).Distinct().ToList();
        var users = (await userOperations.GetUsersByIdsAsync(inspectorIds, cancellationToken))
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
            var list = JsonSerializer.Deserialize<List<DailyInspectionItemDto>>(itemsJson, JsonOptions) ?? [];
            return list
                .OrderBy(x => x.IsPassed) // Thiết bị gặp sự cố (false) ưu tiên lên đầu
                .ThenBy(x => x.ResolutionStatus == "Repaired" || x.ResolutionStatus == "Replaced") // Chưa xử lý lên trước đã xử lý
                .ThenBy(x => x.EquipmentName)
                .ToList();
        }
        catch
        {
            return [];
        }
    }
}
