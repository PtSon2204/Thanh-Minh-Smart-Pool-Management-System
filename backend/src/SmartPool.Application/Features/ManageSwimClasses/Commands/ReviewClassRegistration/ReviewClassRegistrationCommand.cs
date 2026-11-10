using System.Text.Json.Serialization;
using MediatR;

namespace SmartPool.Application.Features.ManageSwimClasses.Commands.ReviewClassRegistration;

public sealed class ReviewClassRegistrationCommand : IRequest<ReviewClassRegistrationResponse>
{
    [JsonIgnore]
    public Guid ClassId { get; set; }

    [JsonIgnore]
    public Guid RegistrationId { get; set; }

    public string Decision { get; set; } = string.Empty;

    public string? ReviewNote { get; set; }

    [JsonConverter(typeof(OffsetRequiredDateTimeOffsetConverter))]
    public DateTimeOffset ExpectedUpdatedAt { get; set; }
}
