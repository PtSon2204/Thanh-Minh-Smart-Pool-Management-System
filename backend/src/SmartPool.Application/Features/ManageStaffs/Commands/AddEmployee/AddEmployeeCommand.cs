using MediatR;
using System.Text.Json.Serialization;
using SmartPool.Application.Features.ManageStaffs.WorkPatterns;

namespace SmartPool.Application.Features.ManageStaffs.Commands.AddEmployee;

public sealed class AddEmployeeCommand : IRequest<AddEmployeeResponse>
{
    [JsonIgnore]
    public Guid ActorId { get; set; }
    public string FullName { get; set; } = string.Empty;
    public string Gender { get; set; } = string.Empty;
    public DateOnly DateOfBirth { get; set; }
    public string Phone { get; set; } = string.Empty;
    public string PersonalEmail { get; set; } = string.Empty;
    public string CccdNumber { get; set; } = string.Empty;
    public string PermanentAddress { get; set; } = string.Empty;
    public string? TemporaryAddress { get; set; }
    public DateOnly JoinDate { get; set; }
    public string TargetRole { get; set; } = string.Empty;
    public ContractTerms? Contract { get; set; }
}

public sealed record AddEmployeeResponse(Guid UserId, string CompanyEmail, string EmailDelivery)
{
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Message { get; init; }
}
