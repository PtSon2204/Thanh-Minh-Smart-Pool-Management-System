using MediatR;
using Microsoft.AspNetCore.Mvc;
using System.Text.Json.Serialization;

namespace SmartPool.Application.Features.ManageUsers.Commands.ChangeUserRole;

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed class ChangeUserRoleCommand : IRequest<IActionResult>
{
    [JsonIgnore]
    public Guid Id { get; set; }

    public string Role { get; init; } = string.Empty;
}
