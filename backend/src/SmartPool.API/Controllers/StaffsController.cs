using FluentValidation;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SmartPool.API.Authorization;
using SmartPool.API.Contracts;
using SmartPool.API.Extensions;
using SmartPool.Application.Common.Models;
using SmartPool.Application.Features.ManageStaffs;
using SmartPool.Application.Features.ManageStaffs.Commands.AssignShift;
using SmartPool.Application.Features.ManageStaffs.Commands.CancelShiftAssignment;
using SmartPool.Application.Features.ManageStaffs.Commands.CreateStaff;
using SmartPool.Application.Features.ManageStaffs.Commands.RecordAttendance;
using SmartPool.Application.Features.ManageStaffs.Commands.SaveSalary;
using SmartPool.Application.Features.ManageStaffs.Commands.UpdateStaff;
using SmartPool.Application.Features.ManageStaffs.Queries.GetSalaries;
using SmartPool.Application.Features.ManageStaffs.Queries.GetSchedule;
using SmartPool.Application.Features.ManageStaffs.Queries.GetStaffOptions;
using SmartPool.Application.Features.ManageStaffs.Queries.GetStaffs;
using SmartPool.Domain.Enums;

namespace SmartPool.API.Controllers
{
    [ApiController]
    [Route("api/staffs")]
    [Authorize(Policy = AuthorizationPolicies.AdminOrStaff)]
    public sealed class StaffsController : ControllerBase
    {
        private readonly ISender _sender;

        public StaffsController(ISender sender)
        {
            _sender = sender;
        }

        /// <summary>Lấy danh sách nhân viên có phân trang và lọc.</summary>
        [HttpGet]
        [Authorize(Policy = AuthorizationPolicies.AdminOnly)]
        [ProducesResponseType(typeof(PagedResponse<GetStaffsResponse>), StatusCodes.Status200OK)]
        public async Task<IActionResult> GetStaffs([FromQuery] GetStaffsQuery query, CancellationToken cancellationToken)
        {
            return await SendAsync(query, cancellationToken);
        }

        /// <summary>Tạo hồ sơ nhân viên từ tài khoản đủ điều kiện.</summary>
        [HttpPost]
        [Authorize(Policy = AuthorizationPolicies.AdminOnly)]
        [ProducesResponseType(typeof(CreateStaffResponse), StatusCodes.Status201Created)]
        public async Task<IActionResult> CreateStaff([FromBody] CreateStaffCommand command, CancellationToken cancellationToken)
        {
            return await SendAsync(command, cancellationToken, StatusCodes.Status201Created);
        }

        /// <summary>Lấy tài khoản và vai trò có thể dùng để tạo nhân viên.</summary>
        [HttpGet("options")]
        [Authorize(Policy = AuthorizationPolicies.AdminOnly)]
        [ProducesResponseType(typeof(GetStaffOptionsResponse), StatusCodes.Status200OK)]
        public async Task<IActionResult> GetOptions(CancellationToken cancellationToken)
        {
            return await SendAsync(new GetStaffOptionsQuery(), cancellationToken);
        }

        /// <summary>Cập nhật hồ sơ nhân viên.</summary>
        [HttpPut("{userId:guid}")]
        [Authorize(Policy = AuthorizationPolicies.AdminOnly)]
        [ProducesResponseType(typeof(UpdateStaffResponse), StatusCodes.Status200OK)]
        public async Task<IActionResult> UpdateStaff(Guid userId, [FromBody] UpdateStaffCommand command, CancellationToken cancellationToken)
        {
            command.UserId = userId;
            return await SendAsync(command, cancellationToken);
        }

        /// <summary>Lấy lịch làm việc có phân trang và lọc.</summary>
        [HttpGet("schedule")]
        [Authorize(Policy = AuthorizationPolicies.AdminOnly)]
        [ProducesResponseType(typeof(PagedResponse<GetScheduleResponse>), StatusCodes.Status200OK)]
        public async Task<IActionResult> GetSchedule([FromQuery] GetScheduleQuery query, CancellationToken cancellationToken)
        {
            return await SendAsync(query, cancellationToken);
        }

        /// <summary>Phân công ca làm việc cho nhân viên.</summary>
        [HttpPost("schedule")]
        [Authorize(Policy = AuthorizationPolicies.AdminOnly)]
        [ProducesResponseType(typeof(AssignShiftResponse), StatusCodes.Status201Created)]
        public async Task<IActionResult> AssignShift([FromBody] AssignShiftCommand command, CancellationToken cancellationToken)
        {
            return await SendAsync(command, cancellationToken, StatusCodes.Status201Created);
        }

        /// <summary>Hủy phân công ca làm việc.</summary>
        [HttpDelete("schedule/{id:guid}")]
        [Authorize(Policy = AuthorizationPolicies.AdminOnly)]
        [ProducesResponseType(typeof(CancelShiftAssignmentResponse), StatusCodes.Status200OK)]
        public async Task<IActionResult> CancelShift(Guid id, CancellationToken cancellationToken)
        {
            return await SendAsync(new CancelShiftAssignmentCommand { Id = id }, cancellationToken);
        }

        /// <summary>Ghi nhận giờ vào ca.</summary>
        [HttpPost("schedule/{id:guid}/check-in")]
        [ProducesResponseType(typeof(RecordAttendanceResponse), StatusCodes.Status200OK)]
        public async Task<IActionResult> CheckIn(Guid id, CancellationToken cancellationToken)
        {
            return await AttendAsync(id, true, cancellationToken);
        }

        /// <summary>Ghi nhận giờ ra ca.</summary>
        [HttpPost("schedule/{id:guid}/check-out")]
        [ProducesResponseType(typeof(RecordAttendanceResponse), StatusCodes.Status200OK)]
        public async Task<IActionResult> CheckOut(Guid id, CancellationToken cancellationToken)
        {
            return await AttendAsync(id, false, cancellationToken);
        }

        /// <summary>Lấy lịch làm việc của người dùng đang đăng nhập.</summary>
        [HttpGet("me/schedule")]
        [ProducesResponseType(typeof(PagedResponse<GetScheduleResponse>), StatusCodes.Status200OK)]
        public async Task<IActionResult> GetMySchedule([FromQuery] GetMyScheduleRequest request, CancellationToken cancellationToken)
        {
            if (!TryGetUserId(out var userId))
            {
                return Forbid();
            }

            var query = new GetScheduleQuery
            {
                PageIndex = request.PageIndex,
                PageSize = request.PageSize,
                Date = request.Date,
                EmployeeId = userId,
                RequireEmployee = true
            };
            return await SendAsync(query, cancellationToken);
        }

        /// <summary>Lấy danh sách bảng lương có phân trang và lọc.</summary>
        [HttpGet("salaries")]
        [Authorize(Policy = AuthorizationPolicies.AdminOnly)]
        [ProducesResponseType(typeof(PagedResponse<GetSalariesResponse>), StatusCodes.Status200OK)]
        public async Task<IActionResult> GetSalaries([FromQuery] GetSalariesQuery query, CancellationToken cancellationToken)
        {
            return await SendAsync(query, cancellationToken);
        }

        /// <summary>Tạo bảng lương cho nhân viên.</summary>
        [HttpPost("{userId:guid}/salaries")]
        [Authorize(Policy = AuthorizationPolicies.AdminOnly)]
        [ProducesResponseType(typeof(SaveSalaryResponse), StatusCodes.Status201Created)]
        public async Task<IActionResult> CreateSalary(Guid userId, [FromBody] SaveSalaryCommand command, CancellationToken cancellationToken)
        {
            command.UserId = userId;
            return await SendAsync(command, cancellationToken, StatusCodes.Status201Created);
        }

        /// <summary>Cập nhật bảng lương của nhân viên.</summary>
        [HttpPut("{userId:guid}/salaries/{id:guid}")]
        [Authorize(Policy = AuthorizationPolicies.AdminOnly)]
        [ProducesResponseType(typeof(SaveSalaryResponse), StatusCodes.Status200OK)]
        public async Task<IActionResult> UpdateSalary(Guid userId, Guid id, [FromBody] SaveSalaryCommand command, CancellationToken cancellationToken)
        {
            command.UserId = userId;
            command.Id = id;
            return await SendAsync(command, cancellationToken);
        }

        private async Task<IActionResult> AttendAsync(Guid id, bool checkIn, CancellationToken cancellationToken)
        {
            Guid? ownerId = null;
            if (!User.HasRole(RoleEnum.ADMIN))
            {
                if (!TryGetUserId(out var userId))
                {
                    return Forbid();
                }

                ownerId = userId;
            }

            return await SendAsync(new RecordAttendanceCommand { Id = id, CheckIn = checkIn, OwnerId = ownerId }, cancellationToken);
        }

        private bool TryGetUserId(out Guid userId)
        {
            return User.TryGetUserId(out userId);
        }

        private async Task<IActionResult> SendAsync<TResponse>(IRequest<TResponse> request, CancellationToken cancellationToken, int successStatus = StatusCodes.Status200OK)
        {
            try
            {
                return StatusCode(successStatus, await _sender.Send(request, cancellationToken));
            }
            catch (ValidationException exception)
            {
                return BadRequest(CreateValidationError(exception));
            }
            catch (KeyNotFoundException exception)
            {
                return NotFound(new { message = exception.Message });
            }
            catch (StaffConflictException exception)
            {
                return Conflict(new { message = exception.Message });
            }
            catch (StaffForbiddenException)
            {
                return Forbid();
            }
        }

        private static object CreateValidationError(ValidationException exception)
        {
            return new
            {
                message = "Dữ liệu không hợp lệ.",
                errors = exception.Errors.GroupBy(error => error.PropertyName)
                    .ToDictionary(group => group.Key, group => group.Select(error => error.ErrorMessage).ToArray())
            };
        }
    }
}
