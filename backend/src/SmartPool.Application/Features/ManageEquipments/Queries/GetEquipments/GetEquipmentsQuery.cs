using SmartPool.Application.Features.ManageEquipments.DTOs;
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
