using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SmartPool.API.Authorization;
using SmartPool.Application.Common.Models;
using SmartPool.Application.Features.ManageUsers.Commands.CreateUser;
using SmartPool.Application.Features.ManageUsers.Commands.ChangeUserRole;
using SmartPool.Application.Features.ManageUsers.Commands.ChangeUserStatus;
using SmartPool.Application.Features.ManageUsers.Commands.UpdateUser;
using SmartPool.Application.Features.ManageUsers.Queries.GetUserById;
using SmartPool.Application.Features.ManageUsers.Queries.GetUsers;

namespace SmartPool.API.Controllers;

[ApiController]
[Route("api/users")]
[Authorize(Policy = AuthorizationPolicies.AdminOnly)]
public sealed class UsersController : ControllerBase
{
    private readonly ISender _sender;

    public UsersController(ISender sender)
    {
        _sender = sender;
    }

    /// <summary>Admin tạo tài khoản người dùng với vai trò CUSTOMER, STAFF hoặc ADMIN.</summary>
    /// <response code="201">Tạo tài khoản và hồ sơ thành công.</response>
    /// <response code="400">Dữ liệu đầu vào không hợp lệ.</response>
    /// <response code="401">Chưa đăng nhập.</response>
    /// <response code="403">Không có quyền Admin.</response>
    /// <response code="409">Username, email, số điện thoại bị trùng hoặc vai trò không khả dụng.</response>
    [HttpPost]
    [ProducesResponseType(typeof(CreateUserResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public Task<IActionResult> CreateUser([FromBody] CreateUserCommand command, CancellationToken cancellationToken)
    {
        return _sender.Send(command, cancellationToken);
    }

    /// <summary>Admin xem danh sách người dùng có phân trang, tìm kiếm và lọc.</summary>
    [HttpGet]
    [ProducesResponseType(typeof(PagedResponse<GetUsersResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public Task<IActionResult> GetUsers([FromQuery] GetUsersQuery query, CancellationToken cancellationToken)
    {
        return _sender.Send(query, cancellationToken);
    }

    /// <summary>Admin xem thông tin tài khoản, hồ sơ và vai trò của một người dùng.</summary>
    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(GetUserByIdResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public Task<IActionResult> GetUserById(Guid id, CancellationToken cancellationToken)
    {
        return _sender.Send(new GetUserByIdQuery { Id = id }, cancellationToken);
    }

    /// <summary>Admin cập nhật email, số điện thoại và hồ sơ của người dùng.</summary>
    [HttpPut("{id:guid}")]
    [ProducesResponseType(typeof(UpdateUserResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public Task<IActionResult> UpdateUser(Guid id, [FromBody] UpdateUserCommand command, CancellationToken cancellationToken)
    {
        command.Id = id;
        return _sender.Send(command, cancellationToken);
    }

    /// <summary>Admin thay đổi vai trò người dùng.</summary>
    [HttpPatch("{id:guid}/role")]
    [ProducesResponseType(typeof(ChangeUserRoleResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public Task<IActionResult> ChangeUserRole(Guid id, [FromBody] ChangeUserRoleCommand command,
        CancellationToken cancellationToken)
    {
        command.Id = id;
        return _sender.Send(command, cancellationToken);
    }

    /// <summary>Admin khóa hoặc mở tài khoản người dùng; lịch làm việc đã phân vẫn được giữ.</summary>
    [HttpPatch("{id:guid}/status")]
    [ProducesResponseType(typeof(ChangeUserStatusResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public Task<IActionResult> ChangeUserStatus(Guid id, [FromBody] ChangeUserStatusCommand command,
        CancellationToken cancellationToken)
    {
        command.Id = id;
        return _sender.Send(command, cancellationToken);
    }
}
