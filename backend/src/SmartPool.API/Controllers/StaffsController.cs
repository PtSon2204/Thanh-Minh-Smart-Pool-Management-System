using FluentValidation;
using MediatR;
using Microsoft.AspNetCore.Mvc;
using SmartPool.API.Authorization;
using SmartPool.API.Contracts;
using SmartPool.API.Extensions;
using SmartPool.Application.Common.Models;
using SmartPool.Application.Features.ManageStaffs;
using SmartPool.Application.Features.ManageStaffs.Commands.AssignShift;
using SmartPool.Application.Features.ManageStaffs.Commands.AssignShiftBatch;
using SmartPool.Application.Features.ManageStaffs.Commands.CancelShiftAssignment;
using SmartPool.Application.Features.ManageStaffs.Commands.CreateStaff;
using SmartPool.Application.Features.ManageStaffs.Commands.CorrectAttendance;
using SmartPool.Application.Features.ManageStaffs.Commands.RecordAttendance;
using SmartPool.Application.Features.ManageStaffs.Commands.UpdateStaff;
using SmartPool.Application.Features.ManageStaffs.Queries.GetSchedule;
using SmartPool.Application.Features.ManageStaffs.Queries.GetScheduleEmployees;
using SmartPool.Application.Features.ManageStaffs.Queries.GetAttendanceHistory;
using SmartPool.Application.Features.ManageStaffs.Queries.GetStaffOptions;
using SmartPool.Application.Features.ManageStaffs.Queries.GetStaffs;
using SmartPool.Domain.Enums;

namespace SmartPool.API.Controllers
{
    [ApiController]
    [Route("api/staffs")]
    public sealed class StaffsController : ControllerBase
    {
        private readonly ISender _sender;

        public StaffsController(ISender sender)
        {
            _sender = sender;
        }

        /// <summary>Lấy danh sách nhân viên có phân trang và lọc.</summary>
        [HttpGet]
        [RequirePermission("staff", "read")]
        [ProducesResponseType(typeof(PagedResponse<GetStaffsResponse>), StatusCodes.Status200OK)]
        public async Task<IActionResult> GetStaffs([FromQuery] GetStaffsQuery query, CancellationToken cancellationToken)
        {
            return await SendAsync(query, cancellationToken);
        }

        /// <summary>Tạo hồ sơ nhân viên từ tài khoản đủ điều kiện.</summary>
        [HttpPost]
        [RequirePermission("staff", "create")]
        [ProducesResponseType(typeof(CreateStaffResponse), StatusCodes.Status201Created)]
        public async Task<IActionResult> CreateStaff([FromBody] CreateStaffCommand command, CancellationToken cancellationToken)
        {
            return await SendAsync(command, cancellationToken, StatusCodes.Status201Created);
        }

        /// <summary>Lấy tài khoản và vai trò có thể dùng để tạo nhân viên.</summary>
        [HttpGet("options")]
        [RequirePermission("staff", "read")]
        [ProducesResponseType(typeof(GetStaffOptionsResponse), StatusCodes.Status200OK)]
        public async Task<IActionResult> GetOptions(CancellationToken cancellationToken)
        {
            return await SendAsync(new GetStaffOptionsQuery(), cancellationToken);
        }

        /// <summary>Cập nhật hồ sơ nhân viên.</summary>
        [HttpPut("{userId:guid}")]
        [RequirePermission("staff", "update")]
        [ProducesResponseType(typeof(UpdateStaffResponse), StatusCodes.Status200OK)]
        public async Task<IActionResult> UpdateStaff(Guid userId, [FromBody] UpdateStaffCommand command, CancellationToken cancellationToken)
        {
            command.UserId = userId;
            return await SendAsync(command, cancellationToken);
        }

        /// <summary>Lấy lịch làm việc có phân trang và lọc.</summary>
        [HttpGet("schedule")]
        [RequirePermission("staff_schedule", "read")]
        [ProducesResponseType(typeof(PagedResponse<GetScheduleResponse>), StatusCodes.Status200OK)]
        public async Task<IActionResult> GetSchedule([FromQuery] GetScheduleQuery query, CancellationToken cancellationToken)
        {
            if (!User.HasRole(RoleEnum.ADMIN))
            {
                return Forbid();
            }

            return await SendAsync(query, cancellationToken);
        }

        /// <summary>Lấy danh sách nhân viên có thể được phân ca.</summary>
        [HttpGet("schedule/employees")]
        [RequirePermission("staff_schedule", "create")]
        [ProducesResponseType(typeof(IReadOnlyList<GetScheduleEmployeeResponse>), StatusCodes.Status200OK)]
        public async Task<IActionResult> GetScheduleEmployees(CancellationToken cancellationToken)
        {
            return await SendAsync(new GetScheduleEmployeesQuery(), cancellationToken);
        }

        /// <summary>Phân công ca làm việc cho nhân viên.</summary>
        [HttpPost("schedule")]
        [RequirePermission("staff_schedule", "create")]
        [ProducesResponseType(typeof(AssignShiftResponse), StatusCodes.Status201Created)]
        public async Task<IActionResult> AssignShift([FromBody] AssignShiftCommand command, CancellationToken cancellationToken)
        {
            return await SendAsync(command, cancellationToken, StatusCodes.Status201Created);
        }

        /// <summary>Phân công nhiều ca làm việc trong một giao dịch.</summary>
        [HttpPost("schedule/batch")]
        [RequirePermission("staff_schedule", "create")]
        [ProducesResponseType(typeof(IReadOnlyList<AssignShiftResponse>), StatusCodes.Status201Created)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
        public async Task<IActionResult> AssignShiftBatch([FromBody] AssignShiftBatchCommand command, CancellationToken cancellationToken)
        {
            return await SendAsync(command, cancellationToken, StatusCodes.Status201Created);
        }

        /// <summary>Hủy phân công ca làm việc.</summary>
        [HttpDelete("schedule/{id:guid}")]
        [RequirePermission("staff_schedule", "delete")]
        [ProducesResponseType(typeof(CancelShiftAssignmentResponse), StatusCodes.Status200OK)]
        public async Task<IActionResult> CancelShift(Guid id, CancellationToken cancellationToken)
        {
            return await SendAsync(new CancelShiftAssignmentCommand { Id = id }, cancellationToken);
        }

        /// <summary>Ghi nhận giờ vào ca.</summary>
        [HttpPost("schedule/{id:guid}/check-in")]
        [RequirePermission("attendance", "update")]
        [ProducesResponseType(typeof(RecordAttendanceResponse), StatusCodes.Status200OK)]
        public async Task<IActionResult> CheckIn(Guid id, [FromBody] RecordAttendanceCommand command, CancellationToken cancellationToken)
        {
            return await AttendAsync(id, true, command, cancellationToken);
        }

        /// <summary>Ghi nhận giờ ra ca.</summary>
        [HttpPost("schedule/{id:guid}/check-out")]
        [RequirePermission("attendance", "update")]
        [ProducesResponseType(typeof(RecordAttendanceResponse), StatusCodes.Status200OK)]
        public async Task<IActionResult> CheckOut(Guid id, [FromBody] RecordAttendanceCommand command, CancellationToken cancellationToken)
        {
            return await AttendAsync(id, false, command, cancellationToken);
        }

        /// <summary>Bổ sung hoặc điều chỉnh giờ chấm công theo thời điểm thực tế.</summary>
        [HttpPatch("schedule/{id:guid}/attendance")]
        [RequirePermission("attendance", "update")]
        [ProducesResponseType(typeof(RecordAttendanceResponse), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status409Conflict)]
        public async Task<IActionResult> CorrectAttendance(Guid id, [FromBody] ManualAttendanceCorrectionCommand command, CancellationToken cancellationToken)
        {
            if (!User.HasRole(RoleEnum.ADMIN) || !TryGetUserId(out var userId))
                return Forbid();

            command.AssignmentId = id;
            command.PerformedByUserId = userId;
            return await SendAsync(command, cancellationToken);
        }

        /// <summary>Lấy lịch sử bổ sung và điều chỉnh chấm công.</summary>
        [HttpGet("schedule/{id:guid}/attendance/history")]
        [RequirePermission("attendance", "read")]
        [ProducesResponseType(typeof(PagedResponse<GetAttendanceHistoryResponse>), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> GetAttendanceHistory(Guid id, [FromQuery] GetAttendanceHistoryQuery query, CancellationToken cancellationToken)
        {
            if (!User.HasRole(RoleEnum.ADMIN))
                return Forbid();

            query.AssignmentId = id;
            return await SendAsync(query, cancellationToken);
        }

        /// <summary>Lấy lịch làm việc của người dùng đang đăng nhập.</summary>
        [HttpGet("me/schedule")]
        [RequirePermission("staff_schedule", "read")]
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
                FromDate = request.FromDate,
                ToDate = request.ToDate,
                EmployeeId = userId,
                RequireEmployee = true
            };
            return await SendAsync(query, cancellationToken);
        }

        private async Task<IActionResult> AttendAsync(Guid id, bool checkIn, RecordAttendanceCommand command, CancellationToken cancellationToken)
        {
            if (!User.HasRole(RoleEnum.ADMIN) || !TryGetUserId(out var userId))
            {
                return Forbid();
            }

            command.Id = id;
            command.CheckIn = checkIn;
            command.OwnerId = null;
            command.Method = "Manual";
            command.PerformedByUserId = userId;
            return await SendAsync(command, cancellationToken);
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
            catch (AssignShiftBatchConflictException exception)
            {
                var problem = new ProblemDetails
                {
                    Status = StatusCodes.Status409Conflict,
                    Title = "Không thể phân công ca làm việc.",
                    Detail = exception.Message
                };
                problem.Extensions["conflicts"] = exception.Conflicts;
                return Conflict(problem);
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
