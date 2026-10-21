using FluentValidation;
using MediatR;
using SmartPool.Application.Common.Models;
using SmartPool.Application.Interfaces.Services;

namespace SmartPool.Application.Features.ManageEquipments.Queries.GetEquipments;

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
