using FluentValidation;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SmartPool.Application.Features.Authentication.Commands.Register;
using System.Text.Json;

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
        try
        {
            var response = await _sender.Send(command, cancellationToken);
            return StatusCode(StatusCodes.Status201Created, response);
        }
        catch (ValidationException exception)
        {
            var errors = exception.Errors
                .GroupBy(error => JsonNamingPolicy.CamelCase.ConvertName(error.PropertyName))
                .ToDictionary(
                    group => group.Key,
                    group => group.Select(error => error.ErrorMessage).Distinct().ToArray());

            return BadRequest(new ValidationProblemDetails(errors));
        }
        catch (RegisterHandler.RegisterConflictException exception)
        {
            var field = JsonNamingPolicy.CamelCase.ConvertName(exception.Field);
            var message = field switch
            {
                "username" => "Tên đăng nhập đã được sử dụng.",
                "email" => "Email đã được sử dụng.",
                "phone" => "Số điện thoại đã được sử dụng.",
                _ => "Thông tin đăng ký đã được sử dụng."
            };

            var problem = new ProblemDetails
            {
                Status = StatusCodes.Status409Conflict,
                Title = "Thông tin đăng ký bị trùng."
            };
            problem.Extensions["errors"] = new Dictionary<string, string[]>
            {
                [field] = [message]
            };

            return new ObjectResult(problem)
            {
                StatusCode = StatusCodes.Status409Conflict,
                ContentTypes = { "application/problem+json" }
            };
        }
        catch (RegisterHandler.CustomerRoleNotConfiguredException)
        {
            return Problem(
                statusCode: StatusCodes.Status500InternalServerError,
                title: "Cấu hình đăng ký chưa sẵn sàng.",
                detail: "Role CUSTOMER chưa tồn tại trong hệ thống.");
        }
    }
}
