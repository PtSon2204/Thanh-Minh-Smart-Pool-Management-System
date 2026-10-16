using System.Security.Claims;
using FluentValidation;
using MediatR;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using SmartPool.Application.Interfaces.Repositories;
using SmartPool.Application.Interfaces.Services;
using SmartPool.Domain.Entities;

namespace SmartPool.Application.Features.Authentication.Commands.ChangePassword;

public sealed class ChangePasswordHandler
    : IRequestHandler<ChangePasswordCommand, IActionResult>
{
    private readonly IRepository<User> _users;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IPasswordHasher _passwordHasher;
    private readonly IValidator<ChangePasswordCommand> _validator;
    private readonly IHttpContextAccessor _httpContextAccessor;

    public ChangePasswordHandler(
        IRepository<User> users,
        IUnitOfWork unitOfWork,
        IPasswordHasher passwordHasher,
        IValidator<ChangePasswordCommand> validator,
        IHttpContextAccessor httpContextAccessor)
    {
        _users = users;
        _unitOfWork = unitOfWork;
        _passwordHasher = passwordHasher;
        _validator = validator;
        _httpContextAccessor = httpContextAccessor;
    }

    public async Task<IActionResult> Handle(
        ChangePasswordCommand request,
        CancellationToken cancellationToken)
    {
        // 1. Validate dữ liệu đầu vào.
        var validationResult = await _validator.ValidateAsync(
            request, cancellationToken);

        if (!validationResult.IsValid)
        {
            var errors = validationResult.Errors
                .GroupBy(error => error.PropertyName)
                .ToDictionary(
                    group => group.Key,
                    group => group.Select(error => error.ErrorMessage)
                        .ToArray());

            return new BadRequestObjectResult(
                new ValidationProblemDetails(errors));
        }

        // 2. Lấy ID từ JWT của người dùng đã đăng nhập.
        var principal = _httpContextAccessor.HttpContext?.User;

        var userIdClaim = principal?.FindFirstValue(
            ClaimTypes.NameIdentifier);

        if (!Guid.TryParse(userIdClaim, out var userId))
        {
            return new UnauthorizedResult();
        }

        // 3. Tìm tài khoản của chính người dùng đó.
        var user = await _users.FirstOrDefaultAsync(
            x => x.Id == userId && x.IsDeleted != true,
            cancellationToken);

        if (user is null || string.IsNullOrWhiteSpace(user.PasswordHash))
        {
            return new UnauthorizedResult();
        }

        // 4. Kiểm tra mật khẩu hiện tại.
        var isCurrentPasswordValid = _passwordHasher.VerifyPassword(
            user,
            user.PasswordHash,
            request.CurrentPassword);

        if (!isCurrentPasswordValid)
        {
            return new BadRequestObjectResult(new
            {
                message = "Mật khẩu hiện tại không chính xác."
            });
        }

        // 5. Hash mật khẩu mới, không lưu mật khẩu dạng văn bản.
        user.PasswordHash = _passwordHasher.HashPassword(
            user,
            request.NewPassword);

        user.UpdatedAt = DateTime.UtcNow;

        // 6. Đánh dấu entity cập nhật và lưu vào database.
        _users.Update(user);

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        // 7. Đổi mật khẩu thành công.
        return new NoContentResult();
    }
}