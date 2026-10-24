using System.Text.Json.Serialization;
using MediatR;
using SmartPool.Application.Features.ManageSwimClasses;

namespace SmartPool.Application.Features.ManageSwimClasses.Commands.CreateClassSessions;

public sealed class CreateClassSessionsCommand : IRequest<CreateClassSessionsResponse>
{
    [JsonIgnore]
    public Guid ClassId { get; set; }

    public List<CreateClassSessionItem> Sessions { get; set; } = [];
}

public sealed class CreateClassSessionItem
{
    [JsonConverter(typeof(OffsetRequiredDateTimeOffsetConverter))]
    public DateTimeOffset StartsAt { get; set; }

    [JsonConverter(typeof(OffsetRequiredDateTimeOffsetConverter))]
    public DateTimeOffset EndsAt { get; set; }
    public Guid? CoachUserId { get; set; }
}
