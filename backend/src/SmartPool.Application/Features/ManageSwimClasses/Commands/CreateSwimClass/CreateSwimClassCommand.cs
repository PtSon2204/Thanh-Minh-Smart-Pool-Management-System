using MediatR;

namespace SmartPool.Application.Features.ManageSwimClasses.Commands.CreateSwimClass;

public sealed class CreateSwimClassCommand : IRequest<CreateSwimClassResponse>
{
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public int Capacity { get; set; }
    public decimal Fee { get; set; }
    public Guid CoachUserId { get; set; }
    public DateTimeOffset RegistrationStartAt { get; set; }
    public DateTimeOffset RegistrationEndAt { get; set; }
}
