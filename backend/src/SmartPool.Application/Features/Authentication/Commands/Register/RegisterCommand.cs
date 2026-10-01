using MediatR;

namespace SmartPool.Application.Features.Authentication.Commands.Register;

/// <summary>
/// Dữ liệu người dùng cung cấp khi đăng ký tài khoản.
/// Role và trạng thái tài khoản do hệ thống tự gán, không nhận từ client.
/// </summary>
public sealed class RegisterCommand : IRequest<RegisterResponse>
{
    public string Username { get; init; } = string.Empty;

    public string Email { get; init; } = string.Empty;

    /// <summary>
    /// Số điện thoại là bắt buộc và sẽ được sử dụng làm thông tin đăng nhập.
    /// </summary>
    public string Phone { get; init; } = string.Empty;

    public string Password { get; init; } = string.Empty;

    public string ConfirmPassword { get; init; } = string.Empty;

    public string FullName { get; init; } = string.Empty;

    public DateOnly? DateOfBirth { get; init; }

    public string? Address { get; init; }
}
