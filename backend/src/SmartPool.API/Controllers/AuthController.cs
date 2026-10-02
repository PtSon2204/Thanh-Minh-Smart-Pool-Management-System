using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SmartPool.Application.Features.Authentication.Commands.Register;

namespace SmartPool.API.Controllers;

[ApiController]
[Route("api/auth")]
public sealed class AuthController : ControllerBase
{
    private readonly ISender _sender;

    public AuthController(ISender sender)
    {
        _sender = sender;
    }

    /// <summary>Đăng ký tài khoản khách hàng. Không tự động đăng nhập hoặc phát hành token.</summary>
    /// <response code="201">Tài khoản và hồ sơ đã được tạo.</response>
    /// <response code="400">Dữ liệu đăng ký không hợp lệ; phản hồi chứa lỗi theo trường.</response>
    /// <response code="409">Username, email hoặc số điện thoại đã được sử dụng.</response>
    /// <response code="500">Thiếu role CUSTOMER trong dữ liệu cấu hình của hệ thống.</response>
    [AllowAnonymous]
    [HttpPost("register")]
    [ProducesResponseType(typeof(RegisterResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> Register(
        [FromBody] RegisterCommand command,
        CancellationToken cancellationToken)
    {
        return await _sender.Send(command, cancellationToken);
    }
}
