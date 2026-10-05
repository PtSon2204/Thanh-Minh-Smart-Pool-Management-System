using MediatR;
using System.Text.Json.Serialization;

namespace SmartPool.Application.Features.ManageServices.Commands.SetServiceStatus;

public sealed class SetServiceStatusCommand : IRequest<SetServiceStatusResponse>
{
    [JsonIgnore]
    public Guid Id { get; set; }

    [JsonRequired]
    public bool IsActive { get; set; }
}
