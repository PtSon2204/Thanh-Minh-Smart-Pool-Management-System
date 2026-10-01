using SmartPool.Domain.Enums;

namespace SmartPool.Application.Features.Authentication.Commands.Register;

/// <summary>
/// Kết quả trả về sau khi tạo tài khoản thành công.
/// Register không phát hành access token hoặc refresh token.
/// </summary>
public sealed class RegisterResponse
{
    public Guid Id { get; init; }

    public string Username { get; init; } = string.Empty;

    public string Email { get; init; } = string.Empty;

    public string Phone { get; init; } = string.Empty;

    public string FullName { get; init; } = string.Empty;

    public RoleEnum Role { get; init; } = RoleEnum.CUSTOMER;

    public string Status { get; init; } = "Active";

    public DateTime CreatedAt { get; init; }
}
