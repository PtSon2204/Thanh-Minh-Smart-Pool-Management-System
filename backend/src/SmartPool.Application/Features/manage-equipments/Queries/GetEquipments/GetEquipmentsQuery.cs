using FluentValidation;
using MediatR;
using SmartPool.Application.Common.Models;
using SmartPool.Application.Interfaces.Services;

namespace SmartPool.Application.Features.ManageEquipments.Queries.GetEquipments;

public sealed class GetEquipmentsQuery : IRequest<PagedResponse<EquipmentListItem>>
{
    public string? SearchTerm { get; set; }
    public string? Category { get; set; }
    public string? Location { get; set; }
    public string? Status { get; set; }
    public int PageIndex { get; set; } = 1;
    public int PageSize { get; set; } = 10;
}

public sealed class GetEquipmentsValidator : AbstractValidator<GetEquipmentsQuery>
{
    public GetEquipmentsValidator()
    {
        RuleFor(x => x.PageIndex).InclusiveBetween(1, int.MaxValue / 100);
        RuleFor(x => x.PageSize).InclusiveBetween(1, 100);
        RuleFor(x => x.SearchTerm).MaximumLength(255);
        RuleFor(x => x.Category).MaximumLength(50);
        RuleFor(x => x.Location).MaximumLength(255);
        RuleFor(x => x.Status).MaximumLength(50);
    }
}

public sealed class EquipmentListItem
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public string? Location { get; set; }
    public string? Metadata { get; set; }
    public string? Status { get; set; }
    public DateOnly? NextMaintenanceDate { get; set; }
    public CameraConnectionStatus? CameraConnection { get; set; }
    public string? CameraSource { get; set; }
}

public sealed class EquipmentFilterOptions
{
    public List<string> Categories { get; set; } = [];
    public List<string> Locations { get; set; } = [];
    public List<string> Statuses { get; set; } = [];
}
