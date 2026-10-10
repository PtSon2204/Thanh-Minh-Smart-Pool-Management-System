using MediatR;
using Microsoft.AspNetCore.Mvc;
using System.Text.Json.Serialization;

namespace SmartPool.Application.Features.ManageUsers.Commands.UpdateUser;

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed class UpdateUserCommand : IRequest<IActionResult>
{
    [JsonIgnore]
    public Guid Id { get; set; }

    public string Email { get; init; } = string.Empty;
    public string Phone { get; init; } = string.Empty;
    public string FullName { get; init; } = string.Empty;
    public DateOnly? DateOfBirth { get; init; }
    public string? Address { get; init; }
}
