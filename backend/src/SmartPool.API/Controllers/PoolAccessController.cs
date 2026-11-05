using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SmartPool.API.Authorization;
using SmartPool.API.Extensions;
using SmartPool.Application.Features.AccessControlPool.Contracts;
using SmartPool.Application.Features.AccessControlPool.Commands.ConfirmEntry;
using SmartPool.Application.Features.AccessControlPool.Queries.GetDailyEntrySummary;
using SmartPool.Application.Features.AccessControlPool.Queries.GetEntryHistory;
using SmartPool.Application.Features.AccessControlPool.Queries.LookupTicket;
using Microsoft.EntityFrameworkCore;
using SmartPool.Application.Common.Time;
using SmartPool.Domain.Entities;
using SmartPool.Infrastructure.Persistence.DbContext;
using System.Security.Claims;

namespace SmartPool.API.Controllers
{
    [ApiController]
    [Route("api/pool-access")]
    public sealed class PoolAccessController : ControllerBase
    {
        private readonly ISender _sender;

        public PoolAccessController(ISender sender)
        {
            _sender = sender;
        }

        /// <summary>Tra cứu thông tin và điều kiện vào bể của vé.</summary>
        [HttpPost("lookup")]
        [RequirePermission("ticket_lookup", "read")]
        [ProducesResponseType(typeof(LookupTicketResponse), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        public async Task<IActionResult> Lookup([FromBody] LookupTicketQuery query, CancellationToken cancellationToken)
        {
            var result = await _sender.Send(query, cancellationToken);
            return ToActionResult(result);
        }

        /// <summary>Xác nhận một lượt vào bể.</summary>
        [HttpPost("entries")]
        [RequirePermission("pool_entry", "create")]
        [ProducesResponseType(typeof(ConfirmEntryResponse), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        public async Task<IActionResult> Confirm([FromBody] ConfirmEntryCommand command, CancellationToken cancellationToken)
        {
            var operatorId = GetOperatorId();
            if (operatorId is null)
            {
                return Unauthorized();
            }

            command.OperatorId = operatorId.Value;
            var result = await _sender.Send(command, cancellationToken);
            return ToActionResult(result);
        }

        /// <summary>Lấy lịch sử lượt vào bể có phân trang.</summary>
        [HttpGet("entries")]
        [RequirePermission("pool_entry", "read")]
        [ProducesResponseType(typeof(SmartPool.Application.Common.Models.PagedResponse<GetEntryHistoryResponse>), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        public async Task<IActionResult> GetHistory([FromQuery] GetEntryHistoryQuery query, CancellationToken cancellationToken)
        {
            var result = await _sender.Send(query, cancellationToken);
            return ToActionResult(result);
        }

        /// <summary>Lấy tổng số lượt vào bể đã được chấp nhận trong ngày.</summary>
        [HttpGet("summary")]
        [RequirePermission("pool_entry", "read")]
        [ProducesResponseType(typeof(GetDailyEntrySummaryResponse), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        public async Task<IActionResult> GetSummary([FromQuery] GetDailyEntrySummaryQuery query, CancellationToken cancellationToken)
        {
            var result = await _sender.Send(query, cancellationToken);
            return ToActionResult(result);
        }

        /// <summary>Lấy số lượng khách hiện tại (số lượt quét vào bể thành công hôm nay).</summary>
        [AllowAnonymous]
        [HttpGet("occupancy")]
        [ProducesResponseType(typeof(GetDailyEntrySummaryResponse), StatusCodes.Status200OK)]
        public async Task<IActionResult> GetOccupancy([FromServices] TimeProvider timeProvider, CancellationToken cancellationToken)
        {
            var today = SmartPool.Application.Common.Time.VietnamTimeBoundary.GetDate(timeProvider);
            var query = new GetDailyEntrySummaryQuery { Date = today };
            var result = await _sender.Send(query, cancellationToken);
            return ToActionResult(result);
        }

        public sealed record UpdatePoolConfigRequest(
            int? MaxCapacity,
            int? WarningThresholdPercent,
            string? MorningStartTime,
            string? MorningEndTime,
            string? AfternoonStartTime,
            string? AfternoonEndTime
        );

        public sealed record ToggleMaintenanceRequest(
            string Status, // "Maintenance" or "Operating"
            string? Reason
        );

        /// <summary>
        /// Lấy toàn bộ thông tin vận hành bể bơi: sức chứa, giờ hoạt động, số vé tại quầy, vé online và số khách đang bơi thời gian thực.
        /// </summary>
        [AllowAnonymous]
        [HttpGet("live-status")]
        public async Task<IActionResult> GetLiveStatus(
            [FromServices] SmartPoolDbContext dbContext,
            [FromServices] TimeProvider timeProvider,
            CancellationToken cancellationToken)
        {
            var config = await dbContext.PoolOperationalConfigs.FirstOrDefaultAsync(cancellationToken);
            if (config is null)
            {
                config = new PoolOperationalConfig
                {
                    Id = Guid.Parse("00000000-0000-0000-0000-000000000001"),
                    PoolName = "Bể bơi Thành Minh",
                    MaxCapacity = 100,
                    WarningThresholdPercent = 80,
                    MorningStartTime = new TimeOnly(6, 0),
                    MorningEndTime = new TimeOnly(11, 30),
                    AfternoonStartTime = new TimeOnly(13, 30),
                    AfternoonEndTime = new TimeOnly(19, 30),
                    OperationalStatus = "Operating",
                    UpdatedAt = DateTime.UtcNow
                };
                dbContext.PoolOperationalConfigs.Add(config);
                await dbContext.SaveChangesAsync(cancellationToken);
            }

            var today = VietnamTimeBoundary.GetDate(timeProvider);
            var currentTime = VietnamTimeBoundary.GetTime(timeProvider);
            var (dayStartUtc, dayEndUtc) = VietnamTimeBoundary.GetUtcDayBounds(today);

            // 1. Số vé mua tại quầy hôm nay
            var counterTicketsToday = await dbContext.Orders.AsNoTracking()
                .Where(o => o.CreatedAt >= dayStartUtc && o.CreatedAt < dayEndUtc && o.EmployeeId != null && o.Status != "CANCELLED")
                .SelectMany(o => o.OrderDetails)
                .SumAsync(d => (int?)d.Quantity, cancellationToken) ?? 0;

            // 2. Số vé mua online hôm nay
            var onlineTicketsToday = await dbContext.Orders.AsNoTracking()
                .Where(o => o.CreatedAt >= dayStartUtc && o.CreatedAt < dayEndUtc && o.EmployeeId == null && o.UserId != null && o.Status != "CANCELLED")
                .SelectMany(o => o.OrderDetails)
                .SumAsync(d => (int?)d.Quantity, cancellationToken) ?? 0;

            // 3. Số khách thực tế đang ở trong bể hôm nay (quét vé vào thành công)
            var currentSwimmers = await dbContext.EntryLogs.AsNoTracking()
                .CountAsync(e => e.ScanTime >= dayStartUtc && e.ScanTime < dayEndUtc && e.Status.ToLower() == "allowed", cancellationToken);

            // 4. Trạng thái theo ca làm việc thực tế
            string effectiveStatus;
            string currentShiftName;

            if (config.OperationalStatus == "Maintenance")
            {
                effectiveStatus = "Maintenance";
                currentShiftName = "Bảo trì thiết bị";
            }
            else if (currentTime >= config.MorningStartTime && currentTime <= config.MorningEndTime)
            {
                effectiveStatus = "Operating";
                currentShiftName = "Ca sáng (06:00 - 11:30)";
            }
            else if (currentTime >= config.AfternoonStartTime && currentTime <= config.AfternoonEndTime)
            {
                effectiveStatus = "Operating";
                currentShiftName = "Ca chiều (13:30 - 19:30)";
            }
            else if (currentTime > config.MorningEndTime && currentTime < config.AfternoonStartTime)
            {
                effectiveStatus = "BreakTime";
                currentShiftName = "Nghỉ trưa (11:30 - 13:30)";
            }
            else
            {
                effectiveStatus = "Closed";
                currentShiftName = "Đóng cửa";
            }

            var maxCap = config.MaxCapacity > 0 ? config.MaxCapacity : 100;
            var thresholdVal = (int)Math.Round((double)maxCap * config.WarningThresholdPercent / 100.0);
            var isCrowded = currentSwimmers >= thresholdVal;
            var occupancyRate = Math.Round((double)currentSwimmers * 100.0 / maxCap, 1);
            var canBookOnline = config.OperationalStatus != "Maintenance";

            return Ok(new
            {
                config.Id,
                config.PoolName,
                config.MaxCapacity,
                config.WarningThresholdPercent,
                MorningStartTime = config.MorningStartTime.ToString("HH:mm"),
                MorningEndTime = config.MorningEndTime.ToString("HH:mm"),
                AfternoonStartTime = config.AfternoonStartTime.ToString("HH:mm"),
                AfternoonEndTime = config.AfternoonEndTime.ToString("HH:mm"),
                OperationalStatus = config.OperationalStatus,
                EffectiveStatus = effectiveStatus,
                StatusReason = config.StatusReason,
                config.IsAutoSuspended,
                config.UpdatedAt,
                config.UpdatedBy,

                // Số liệu thống kê động hôm nay:
                CounterTicketsToday = counterTicketsToday,
                OnlineTicketsToday = onlineTicketsToday,
                CurrentSwimmers = currentSwimmers,
                OccupancyRate = occupancyRate,
                IsCrowded = isCrowded,
                CanBookOnline = canBookOnline,
                CurrentShiftName = currentShiftName
            });
        }

        /// <summary>
        /// Quản trị viên (Admin) cấu hình lại sức chứa tối đa, ngưỡng cảnh báo, giờ mở/đóng ca sáng và ca chiều.
        /// </summary>
        [HttpPut("operational-config")]
        [Authorize(Policy = AuthorizationPolicies.AdminOnly)]
        public async Task<IActionResult> UpdateOperationalConfig(
            [FromBody] UpdatePoolConfigRequest request,
            [FromServices] SmartPoolDbContext dbContext,
            CancellationToken cancellationToken)
        {
            var config = await dbContext.PoolOperationalConfigs.FirstOrDefaultAsync(cancellationToken);
            if (config is null)
            {
                config = new PoolOperationalConfig { Id = Guid.Parse("00000000-0000-0000-0000-000000000001") };
                dbContext.PoolOperationalConfigs.Add(config);
            }

            if (request.MaxCapacity.HasValue && request.MaxCapacity > 0)
                config.MaxCapacity = request.MaxCapacity.Value;

            if (request.WarningThresholdPercent.HasValue && request.WarningThresholdPercent > 0 && request.WarningThresholdPercent <= 100)
                config.WarningThresholdPercent = request.WarningThresholdPercent.Value;

            if (TimeOnly.TryParse(request.MorningStartTime, out var morningStart))
                config.MorningStartTime = morningStart;

            if (TimeOnly.TryParse(request.MorningEndTime, out var morningEnd))
                config.MorningEndTime = morningEnd;

            if (TimeOnly.TryParse(request.AfternoonStartTime, out var afternoonStart))
                config.AfternoonStartTime = afternoonStart;

            if (TimeOnly.TryParse(request.AfternoonEndTime, out var afternoonEnd))
                config.AfternoonEndTime = afternoonEnd;

            var adminName = User.FindFirstValue(ClaimTypes.Name) ?? "Admin";
            config.UpdatedAt = DateTime.UtcNow;
            config.UpdatedBy = adminName;

            await dbContext.SaveChangesAsync(cancellationToken);

            return Ok(new { message = "Cập nhật cấu hình vận hành bể bơi thành công.", config });
        }

        /// <summary>
        /// Quản trị viên (Admin) chủ động xác nhận tạm dừng bể bơi (Maintenance) hoặc mở lại hoạt động (Operating).
        /// </summary>
        [HttpPost("toggle-maintenance")]
        [Authorize(Policy = AuthorizationPolicies.AdminOnly)]
        public async Task<IActionResult> ToggleMaintenance(
            [FromBody] ToggleMaintenanceRequest request,
            [FromServices] SmartPoolDbContext dbContext,
            CancellationToken cancellationToken)
        {
            var config = await dbContext.PoolOperationalConfigs.FirstOrDefaultAsync(cancellationToken);
            if (config is null)
            {
                config = new PoolOperationalConfig { Id = Guid.Parse("00000000-0000-0000-0000-000000000001") };
                dbContext.PoolOperationalConfigs.Add(config);
            }

            var adminName = User.FindFirstValue(ClaimTypes.Name) ?? "Admin";
            var targetStatus = request.Status?.Trim() == "Maintenance" ? "Maintenance" : "Operating";

            config.OperationalStatus = targetStatus;
            config.StatusReason = targetStatus == "Maintenance"
                ? (string.IsNullOrWhiteSpace(request.Reason) ? "Bể bơi tạm dừng hoạt động để bảo trì thiết bị an toàn." : request.Reason.Trim())
                : null;
            config.IsAutoSuspended = targetStatus == "Maintenance";
            config.UpdatedAt = DateTime.UtcNow;
            config.UpdatedBy = adminName;

            await dbContext.SaveChangesAsync(cancellationToken);

            return Ok(new
            {
                message = targetStatus == "Maintenance"
                    ? "Đã xác nhận tạm dừng hoạt động bể bơi để bảo trì."
                    : "Đã mở lại hoạt động bình thường cho bể bơi.",
                config
            });
        }

        private Guid? GetOperatorId()
        {
            return User.TryGetUserId(out var operatorId)
                ? operatorId
                : null;
        }

        private ActionResult ToActionResult<T>(PoolAccessValidationResult<T> result)
        {
            return result.IsValid
                ? Ok(result.Value)
                : BadRequest(new ValidationProblemDetails(new Dictionary<string, string[]>(result.Errors!)));
        }
    }
}
