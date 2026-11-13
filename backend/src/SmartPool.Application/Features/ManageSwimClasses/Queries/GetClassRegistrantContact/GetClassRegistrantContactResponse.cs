namespace SmartPool.Application.Features.ManageSwimClasses.Queries.GetClassRegistrantContact;

public sealed record GetClassRegistrantContactResponse(
    Guid UserId,
    string FullName,
    string? Email,
    string? Phone);
