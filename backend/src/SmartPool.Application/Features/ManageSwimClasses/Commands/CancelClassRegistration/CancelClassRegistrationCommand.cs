using System.Text.Json.Serialization;
using MediatR;

namespace SmartPool.Application.Features.ManageSwimClasses.Commands.CancelClassRegistration;

public sealed class CancelClassRegistrationCommand : IRequest<CancelClassRegistrationResponse>
{
    [JsonIgnore]
    public Guid ClassId { get; set; }

    [JsonIgnore]
    public Guid RegistrationId { get; set; }

    public string Reason { get; set; } = string.Empty;

    [JsonConverter(typeof(OffsetRequiredDateTimeOffsetConverter))]
    public DateTimeOffset ExpectedUpdatedAt { get; set; }
}

public sealed record CancelClassRegistrationResponse(
    Guid RegistrationId, Guid ClassId, string Status,
    string CancelReason, Guid CancelledByUserId,
    DateTime CancelledAt, DateTime UpdatedAt);
