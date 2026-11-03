using System.Text.Json.Serialization;
using MediatR;

namespace SmartPool.Application.Features.ManageSwimClasses.Commands.DeactivateSwimClass;

public sealed class DeactivateSwimClassCommand : IRequest<DeactivateSwimClassResponse>
{
    [JsonIgnore]
    public Guid ClassId { get; set; }

    public string Reason { get; set; } = string.Empty;

    public DateTimeOffset ExpectedUpdatedAt { get; set; }
}

public sealed record DeactivateSwimClassResponse(
    Guid Id, string Status, string InactiveReason, DateTime InactivatedAt, DateTime UpdatedAt);
