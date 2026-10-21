using System.Text.Json.Serialization;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace SmartPool.Application.Features.ManageUsers.Commands.ChangeUserStatus;

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed class ChangeUserStatusCommand : IRequest<IActionResult>
{
    [JsonIgnore]
    public Guid Id { get; set; }

    public string Status { get; init; } = string.Empty;
}
