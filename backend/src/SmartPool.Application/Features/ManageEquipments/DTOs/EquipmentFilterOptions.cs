using FluentValidation;
using MediatR;
using SmartPool.Application.Common.Models;
using SmartPool.Application.Interfaces.Services;

namespace SmartPool.Application.Features.ManageEquipments.DTOs;

public sealed class EquipmentFilterOptions
{
    public List<string> Categories { get; set; } = [];
    public List<string> Locations { get; set; } = [];
    public List<string> Statuses { get; set; } = [];
}
