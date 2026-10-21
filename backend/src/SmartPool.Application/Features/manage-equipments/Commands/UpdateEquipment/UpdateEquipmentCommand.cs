using FluentValidation;
using MediatR;
using System.Text.Json;
using SmartPool.Application.Features.ManageEquipments.Commands.CreateEquipment;

namespace SmartPool.Application.Features.ManageEquipments.Commands.UpdateEquipment;

public sealed class UpdateEquipmentCommand : IRequest<bool>
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public string? Location { get; set; }
    public JsonElement? Metadata { get; set; }
}

public sealed class UpdateEquipmentValidator : AbstractValidator<UpdateEquipmentCommand>
{
    public UpdateEquipmentValidator()
    {
        RuleFor(x => x.Id).NotEmpty();
        // Reuse text and specification rules without requiring a new camera connection.
        RuleFor(x => x).Custom((request, context) =>
        {
            var validation = new CreateEquipmentValidator().Validate(new CreateEquipmentCommand
            {
                Name = request.Name, Category = request.Category, Location = request.Location, Metadata = request.Metadata
            });
            foreach (var error in validation.Errors.Where(x => x.PropertyName is "Name" or "Category" or "Location" or "Metadata"))
                context.AddFailure(error);
        });
    }
}
