using System.Text.Json.Serialization;
using MediatR;

namespace SmartPool.Application.Features.ManageSwimClasses.Commands.CancelClassSession;

public sealed class CancelClassSessionCommand : IRequest<CancelClassSessionResponse>
{
    [JsonIgnore]
    public Guid ClassId { get; set; }

    [JsonIgnore]
    public Guid SessionId { get; set; }

    public string Reason { get; set; } = string.Empty;

    [JsonConverter(typeof(OffsetRequiredDateTimeOffsetConverter))]
    public DateTimeOffset ExpectedUpdatedAt { get; set; }
}

public sealed record CancelClassSessionResponse(
    Guid Id, Guid ClassId, string Status, string CancelReason, DateTime UpdatedAt);
