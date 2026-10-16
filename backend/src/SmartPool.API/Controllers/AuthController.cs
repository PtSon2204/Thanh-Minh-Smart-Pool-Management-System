using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SmartPool.Application.Features.Authentication.Commands.ChangePassword;
using SmartPool.Application.Features.Authentication.Commands.Login;
using SmartPool.Application.Features.Authentication.Commands.PasswordReset;
using SmartPool.Application.Features.Authentication.Commands.Register;
using SmartPool.Application.Features.Authentication.Commands.Register.ResendOTP;
using SmartPool.Application.Features.Authentication.Commands.Register.VerifyOTP;

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

    /// <summary>Đăng nhập bằng username, email hoặc số điện thoại và nhận access JWT.</summary>
    /// <response code="200">Đăng nhập thành công; trả thông tin tài khoản và access token.</response>
    /// <response code="400">Thiếu hoặc sai định dạng dữ liệu đăng nhập.</response>
    /// <response code="401">Thông tin đăng nhập không hợp lệ hoặc tài khoản không khả dụng.</response>
    [AllowAnonymous]
    [HttpPost("login")]
    [ProducesResponseType(typeof(LoginResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    public Task<IActionResult> Login(
        [FromBody] LoginCommand command,
        CancellationToken cancellationToken)
    {
        return _sender.Send(command, cancellationToken);
    }

    /// <summary>Khởi tạo đăng ký khách hàng và gửi OTP đến email. Chưa tạo tài khoản.</summary>
    /// <response code="202">Đã lưu phiên tạm và gửi OTP; trả registrationId cùng thời hạn.</response>
    /// <response code="400">Dữ liệu đăng ký không hợp lệ; phản hồi chứa lỗi theo trường.</response>
    /// <response code="409">Username, email, số điện thoại đã được sử dụng hoặc email đang chờ xác minh.</response>
    /// <response code="429">Quá giới hạn khởi tạo đăng ký.</response>
    /// <response code="500">Thiếu role CUSTOMER trong dữ liệu cấu hình của hệ thống.</response>
    /// <response code="503">Redis hoặc dịch vụ email chưa sẵn sàng.</response>
    [AllowAnonymous]
    [HttpPost("register")]
    [ProducesResponseType(typeof(RegisterPendingResponse), StatusCodes.Status202Accepted)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status429TooManyRequests)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status500InternalServerError)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status503ServiceUnavailable)]
    public async Task<IActionResult> Register(
        [FromBody] RegisterCommand command,
        CancellationToken cancellationToken)
    {
        return await _sender.Send(command, cancellationToken);
    }

    /// <summary>Xác minh OTP email và tạo tài khoản khách hàng sau khi mã hợp lệ.</summary>
    /// <response code="201">Tài khoản và hồ sơ đã được tạo; không phát hành token.</response>
    /// <response code="400">Dữ liệu hoặc OTP không hợp lệ.</response>
    /// <response code="409">Thông tin đăng ký bị trùng hoặc phiên đang/đã được xác minh.</response>
    /// <response code="410">OTP hoặc phiên đăng ký đã hết hạn.</response>
    /// <response code="429">Đã nhập sai OTP quá số lần cho phép.</response>
    /// <response code="500">Thiếu role CUSTOMER.</response>
    /// <response code="503">Redis hoặc database chưa sẵn sàng.</response>
    [AllowAnonymous]
    [HttpPost("register/verify")]
    [ProducesResponseType(typeof(RegisterResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status410Gone)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status429TooManyRequests)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status500InternalServerError)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status503ServiceUnavailable)]
    public Task<IActionResult> VerifyRegistration(
        [FromBody] VerifyRegistrationCommand command,
        CancellationToken cancellationToken)
    {
        return _sender.Send(command, cancellationToken);
    }

    /// <summary>Gửi lại OTP email cho phiên đăng ký còn hiệu lực.</summary>
    /// <response code="202">Đã gửi mã mới và cập nhật thời hạn OTP.</response>
    /// <response code="400">Mã phiên đăng ký không hợp lệ.</response>
    /// <response code="409">Phiên đã được xác minh hoặc đang xử lý gửi lại.</response>
    /// <response code="410">Phiên đăng ký đã hết hạn.</response>
    /// <response code="429">Chưa hết thời gian chờ, hết lượt gửi lại hoặc hết lượt thử OTP.</response>
    /// <response code="503">Redis hoặc dịch vụ email chưa sẵn sàng.</response>
    [AllowAnonymous]
    [HttpPost("register/resend")]
    [ProducesResponseType(typeof(ResendRegistrationResponse), StatusCodes.Status202Accepted)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status410Gone)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status429TooManyRequests)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status503ServiceUnavailable)]
    public Task<IActionResult> ResendRegistration(
        [FromBody] ResendRegistrationCommand command,
        CancellationToken cancellationToken)
    {
        return _sender.Send(command, cancellationToken);
    }

    /// <summary>Yêu cầu OTP khôi phục mật khẩu; phản hồi không tiết lộ email có tài khoản hay không.</summary>
    /// <response code="202">Đã tiếp nhận yêu cầu, trả recoveryId và các mốc thời gian.</response>
    /// <response code="400">Email không đúng định dạng.</response>
    /// <response code="429">Vượt giới hạn yêu cầu theo email hoặc IP.</response>
    /// <response code="503">Redis, database hoặc địa chỉ kết nối không sẵn sàng.</response>
    [AllowAnonymous]
    [HttpPost("password-reset/request")]
    [ProducesResponseType(typeof(RequestPasswordResetResponse), StatusCodes.Status202Accepted)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status429TooManyRequests)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status503ServiceUnavailable)]
    public Task<IActionResult> RequestPasswordReset(
        [FromBody] RequestPasswordResetCommand command,
        CancellationToken cancellationToken)
    {
        return _sender.Send(command, cancellationToken);
    }

    /// <summary>Gửi lại OTP cho phiên khôi phục còn hiệu lực.</summary>
    /// <response code="202">Đã tiếp nhận yêu cầu gửi lại; trả các mốc thời gian.</response>
    /// <response code="400">Mã phiên khôi phục không hợp lệ.</response>
    /// <response code="409">Phiên đang xử lý hoặc đã được xác minh.</response>
    /// <response code="410">Phiên khôi phục đã hết hạn.</response>
    /// <response code="429">Chưa hết cooldown, hết lượt gửi lại hoặc hết lượt thử OTP.</response>
    /// <response code="503">Redis chưa sẵn sàng.</response>
    [AllowAnonymous]
    [HttpPost("password-reset/resend")]
    [ProducesResponseType(typeof(ResendPasswordResetResponse), StatusCodes.Status202Accepted)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status410Gone)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status429TooManyRequests)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status503ServiceUnavailable)]
    public Task<IActionResult> ResendPasswordReset(
        [FromBody] ResendPasswordResetCommand command,
        CancellationToken cancellationToken)
    {
        return _sender.Send(command, cancellationToken);
    }

    /// <summary>Xác minh OTP khôi phục và cấp reset token; chưa đổi mật khẩu.</summary>
    /// <response code="200">OTP hợp lệ; trả reset token dùng một lần và thời hạn.</response>
    /// <response code="400">Dữ liệu hoặc OTP không hợp lệ.</response>
    /// <response code="409">Phiên đang hoặc đã được xác minh.</response>
    /// <response code="410">OTP hoặc phiên khôi phục đã hết hạn.</response>
    /// <response code="429">Đã nhập OTP sai quá số lần cho phép.</response>
    /// <response code="503">Redis chưa sẵn sàng.</response>
    [AllowAnonymous]
    [HttpPost("password-reset/verify")]
    [ProducesResponseType(typeof(VerifyPasswordResetResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status410Gone)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status429TooManyRequests)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status503ServiceUnavailable)]
    public Task<IActionResult> VerifyPasswordReset(
        [FromBody] VerifyPasswordResetCommand command,
        CancellationToken cancellationToken)
    {
        return _sender.Send(command, cancellationToken);
    }

    /// <summary>Đổi mật khẩu bằng reset token đã xác minh, sau đó vô hiệu các access JWT cũ.</summary>
    /// <response code="204">Mật khẩu đã đổi; reset token đã được tiêu thụ.</response>
    /// <response code="400">Dữ liệu hoặc reset token không hợp lệ.</response>
    /// <response code="409">Tài khoản đã thay đổi hoặc một yêu cầu khác đang xử lý.</response>
    /// <response code="410">Phiên hoặc reset token đã hết hạn.</response>
    /// <response code="503">Redis hoặc database chưa sẵn sàng.</response>
    [AllowAnonymous]
    [HttpPost("password-reset/complete")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status410Gone)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status503ServiceUnavailable)]
    public Task<IActionResult> CompletePasswordReset(
        [FromBody] CompletePasswordResetCommand command,
        CancellationToken cancellationToken)
    {
        return _sender.Send(command, cancellationToken);
    }

    [Authorize]
    [HttpPut("change-password")]
    public async Task<IActionResult> ChangePassword(
        [FromBody] ChangePasswordCommand command,
        CancellationToken cancellationToken)
    {
        return await _sender.Send(command, cancellationToken);
    }
}
