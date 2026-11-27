using System.Net.Mail;
using System.Text.Json.Serialization;
using FluentValidation;
using FluentValidation.Results;
using MediatR;
using SmartPool.Application.Interfaces.Repositories;

namespace SmartPool.Application.Features.ManageStaffs.Commands.AddEmployee;

public sealed class ResetStaffPasswordCommand : IRequest<AddEmployeeResponse>
{
    [JsonIgnore] public Guid UserId { get; set; }
    [JsonIgnore] public Guid ActorId { get; set; }
    public string PersonalEmail { get; set; } = "";
}

public sealed class ResetStaffPasswordHandler(IStaffOperations operations)
    : IRequestHandler<ResetStaffPasswordCommand, AddEmployeeResponse>
{
    public Task<AddEmployeeResponse> Handle(ResetStaffPasswordCommand request, CancellationToken cancellationToken)
    {
        var email = request.PersonalEmail?.Trim().ToLowerInvariant() ?? "";
        if (email.Length > 150 || !MailAddress.TryCreate(email, out var parsed) || parsed.Address != email ||
            email.EndsWith("@smartpool.com", StringComparison.OrdinalIgnoreCase))
            throw new ValidationException([new ValidationFailure("PersonalEmail", "Email cá nhân không hợp lệ.")]);
        return operations.ResetStaffPasswordAsync(request.UserId, request.ActorId, email, cancellationToken);
    }
}
