using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace SmartPool.Application.Features.Profiles.Commands.UpdateMyProfile;

public sealed class UpdateMyProfileCommand : IRequest<IActionResult>
{
    [System.Text.Json.Serialization.JsonIgnore] public Guid UserId { get; set; }
    public string FullName { get; init; } = string.Empty;
    public string? Address { get; init; }
    public DateOnly? DateOfBirth { get; init; }
}

