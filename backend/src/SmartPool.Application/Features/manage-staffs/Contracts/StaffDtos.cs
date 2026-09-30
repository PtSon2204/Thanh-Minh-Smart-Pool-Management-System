namespace SmartPool.Application.Features.ManageStaffs.Contracts;

public enum StaffError
{
    Validation,
    NotFound,
    Conflict,
    Forbidden
}

public sealed record OperationResult<T>(T? Value, StaffError? Error, string? Message)
{
    public static OperationResult<T> Success(T value) => new(value, null, null);
    public static OperationResult<T> Failure(StaffError error, string message) => new(default, error, message);
}

public sealed record PageRequest(int Page = 1, int PageSize = 20);
public sealed record PageResult<T>(IReadOnlyList<T> Items, int Total, int Page, int PageSize);

public sealed record StaffDto(Guid UserId, string? Username, string? Email, string? Phone, string FullName,
    string? AvatarUrl, string? Address, DateOnly? DateOfBirth, Guid RoleId, string RoleName,
    DateOnly JoinDate, decimal BaseSalary, string Status);
public sealed record StaffOptionsDto(IReadOnlyList<EligibleUserDto> Users, IReadOnlyList<RoleDto> Roles);
public sealed record EligibleUserDto(Guid UserId, string? Username, string? Email, string? Phone, string? FullName);
public sealed record RoleDto(Guid Id, string Name);
public sealed record CreateStaffRequest(Guid UserId, Guid RoleId, DateOnly JoinDate, decimal BaseSalary,
    string Status, string FullName, string? AvatarUrl, string? Address, DateOnly? DateOfBirth);
public sealed record UpdateStaffRequest(Guid RoleId, decimal BaseSalary, string Status, string FullName,
    string? AvatarUrl, string? Address, DateOnly? DateOfBirth);
public sealed record StaffListRequest(int Page = 1, int PageSize = 20, string? Search = null, string? Status = null);
