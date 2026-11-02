using System.Text.Json.Serialization;
using MediatR;

namespace SmartPool.Application.Features.ManageSwimClasses.Commands.UpdateSwimClass;

public sealed class UpdateSwimClassCommand : IRequest<UpdateSwimClassResponse>
{
    [JsonIgnore]
    public Guid ClassId { get; set; }

    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public int Capacity { get; set; }
    public decimal Fee { get; set; }

    [JsonConverter(typeof(OffsetRequiredDateTimeOffsetConverter))]
    public DateTimeOffset ExpectedUpdatedAt { get; set; }

    [JsonConverter(typeof(OffsetRequiredDateTimeOffsetConverter))]
    public DateTimeOffset RegistrationStartAt { get; set; }

    [JsonConverter(typeof(OffsetRequiredDateTimeOffsetConverter))]
    public DateTimeOffset RegistrationEndAt { get; set; }
}
