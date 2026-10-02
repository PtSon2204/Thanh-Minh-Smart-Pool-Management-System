using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace SmartPool.Application.Features.Authentication.Commands.Register;


public sealed class RegisterCommand : IRequest<IActionResult>
{
    public string Username { get; init; } = string.Empty;

    public string Email { get; init; } = string.Empty;

    public string Phone { get; init; } = string.Empty;

    public string Password { get; init; } = string.Empty;

    public string ConfirmPassword { get; init; } = string.Empty;

    public string FullName { get; init; } = string.Empty;

    public DateOnly? DateOfBirth { get; init; }

    public string? Address { get; init; }
}
