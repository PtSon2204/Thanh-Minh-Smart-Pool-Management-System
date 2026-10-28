using System.Text.Json;
using FluentValidation;
using FluentValidation.Results;
using MediatR;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using SmartPool.API.Controllers;
using SmartPool.Application.Features.ManageEquipments.Maintenance;
using SmartPool.Application.Features.ManageEquipments.Maintenance.Commands.UpdateMaintenanceSchedule;
using SmartPool.Application.Interfaces.Repositories;

namespace SmartPool.UnitTests.Features.ManageEquipments.Maintenance;

public sealed class MaintenanceUpdateTests
{
    [Fact(DisplayName = "UTCID01 (N) - Cập nhật lịch hợp lệ -> Trả HTTP 200 với lịch sau cập nhật")]
    public async Task UTCID01_UpdateValidSchedule_ReturnsUpdatedSchedule()
    {
        var id = Guid.NewGuid();
        var command = ValidCommand();
        var expected = new MaintenanceItem(id, command.EquipmentId, "Máy bơm", "Khu A", command.TaskName,
            command.RepeatUnit, command.RepeatEvery, command.StartDate, command.AssignedTo, "Nhân viên A",
            command.RemindBeforeDays, "Pending", false, null);
        var sender = new Mock<ISender>(MockBehavior.Strict);
        using var cts = new CancellationTokenSource();
        sender.Setup(s => s.Send(command, cts.Token)).ReturnsAsync(expected);

        var result = await Controller(sender).Update(id, command, cts.Token);

        var ok = Assert.IsType<OkObjectResult>(result);
        Assert.Same(expected, ok.Value);
        Assert.Equal(id, command.Id);
        sender.Verify(s => s.Send(command, cts.Token), Times.Once);
        sender.VerifyNoOtherCalls();
    }

    [Fact(DisplayName = "UTCID02 (A) - UpdateAsync ném MaintenanceException 404 -> Controller trả 404")]
    public async Task UTCID02_ScheduleNotFound_ReturnsNotFoundStatus()
    {
        var id = Guid.NewGuid();
        var command = ValidCommand();
        var sender = new Mock<ISender>(MockBehavior.Strict);
        sender.Setup(s => s.Send(command, CancellationToken.None))
            .ThrowsAsync(new MaintenanceException(404, "Không tìm thấy lịch bảo trì."));

        var result = await Controller(sender).Update(id, command, CancellationToken.None);

        var response = Assert.IsType<ObjectResult>(result);
        Assert.Equal(404, response.StatusCode);
        AssertResponseMessage(response, "Không tìm thấy lịch bảo trì.");
        sender.Verify(s => s.Send(command, CancellationToken.None), Times.Once);
        sender.VerifyNoOtherCalls();
    }

    [Fact(DisplayName = "UTCID03 (A) - UpdateAsync ném ValidationException -> Controller trả HTTP 400 kèm errors")]
    public async Task UTCID03_ValidationException_ReturnsBadRequestWithErrors()
    {
        var id = Guid.NewGuid();
        var command = ValidCommand();
        var sender = new Mock<ISender>(MockBehavior.Strict);
        sender.Setup(s => s.Send(command, CancellationToken.None))
            .ThrowsAsync(new ValidationException([new ValidationFailure("TaskName", "Nhập tên công việc.")]));

        var result = await Controller(sender).Update(id, command, CancellationToken.None);

        var badRequest = Assert.IsType<BadRequestObjectResult>(result);
        Assert.Equal(400, badRequest.StatusCode);
        AssertResponseMessage(badRequest, "Vui lòng kiểm tra thông tin lịch bảo trì.");
        using var body = JsonDocument.Parse(JsonSerializer.Serialize(badRequest.Value));
        Assert.Equal("Nhập tên công việc.", body.RootElement.GetProperty("errors").GetProperty("TaskName")[0].GetString());
        sender.Verify(s => s.Send(command, CancellationToken.None), Times.Once);
        sender.VerifyNoOtherCalls();
    }

    [Fact(DisplayName = "UTCID04 (A) - UpdateAsync ném lỗi xung đột 409 -> Controller giữ status code")]
    public async Task UTCID04_Conflict_ReturnsConflictStatus()
    {
        var id = Guid.NewGuid();
        var command = ValidCommand();
        const string message = "Chỉ cập nhật lịch đang chờ hoặc tạm dừng.";
        var sender = new Mock<ISender>(MockBehavior.Strict);
        sender.Setup(s => s.Send(command, CancellationToken.None))
            .ThrowsAsync(new MaintenanceException(409, message));

        var result = await Controller(sender).Update(id, command, CancellationToken.None);

        var response = Assert.IsType<ObjectResult>(result);
        Assert.Equal(409, response.StatusCode);
        AssertResponseMessage(response, message);
        sender.Verify(s => s.Send(command, CancellationToken.None), Times.Once);
        sender.VerifyNoOtherCalls();
    }

    [Fact(DisplayName = "UTCID05 (A) - UpdateAsync ném exception hệ thống -> Controller trả HTTP 500")]
    public async Task UTCID05_UnexpectedException_ReturnsInternalServerError()
    {
        var id = Guid.NewGuid();
        var command = ValidCommand();
        var sender = new Mock<ISender>(MockBehavior.Strict);
        sender.Setup(s => s.Send(command, CancellationToken.None))
            .ThrowsAsync(new InvalidOperationException("Database unavailable"));

        var result = await Controller(sender).Update(id, command, CancellationToken.None);

        var response = Assert.IsType<ObjectResult>(result);
        Assert.Equal(500, response.StatusCode);
        AssertResponseMessage(response, "Không thể xử lý lịch bảo trì. Vui lòng thử lại.");
        sender.Verify(s => s.Send(command, CancellationToken.None), Times.Once);
        sender.VerifyNoOtherCalls();
    }

    [Fact(DisplayName = "UTCID06 (B) - Request tại giới hạn hợp lệ -> Validator chấp nhận")]
    public void UTCID06_RequestAtInclusiveLimits_PassesValidation()
    {
        var request = ValidRequest();
        request.TaskName = new string('A', 255);
        request.RepeatEvery = 365;
        request.RemindBeforeDays = 365;

        var result = new MaintenanceRequestValidator().Validate(request);

        Assert.True(result.IsValid);
        Assert.Empty(result.Errors);
    }

    [Fact(DisplayName = "UTCID07 (A) - EquipmentId rỗng -> Validator báo phải chọn thiết bị")]
    public void UTCID07_EmptyEquipmentId_FailsValidation()
    {
        var request = ValidRequest();
        request.EquipmentId = Guid.Empty;

        AssertError(request, nameof(MaintenanceRequest.EquipmentId), "Chọn thiết bị.");
    }

    [Fact(DisplayName = "UTCID08 (A) - TaskName rỗng -> Validator báo nhập tên công việc")]
    public void UTCID08_EmptyTaskName_FailsValidation()
    {
        var request = ValidRequest();
        request.TaskName = string.Empty;

        AssertError(request, nameof(MaintenanceRequest.TaskName), "Nhập tên công việc.");
    }

    [Fact(DisplayName = "UTCID09 (A) - TaskName dài 256 ký tự -> Validator từ chối")]
    public void UTCID09_OverlengthTaskName_FailsValidation()
    {
        var request = ValidRequest();
        request.TaskName = new string('A', 256);

        AssertError(request, nameof(MaintenanceRequest.TaskName), "Tên công việc tối đa 255 ký tự và không chứa ký tự không hợp lệ.");
    }

    [Fact(DisplayName = "UTCID10 (A) - TaskName chứa NUL -> Validator từ chối")]
    public void UTCID10_TaskNameContainsNullCharacter_FailsValidation()
    {
        var request = ValidRequest();
        request.TaskName = "Bảo trì\0máy bơm";

        AssertError(request, nameof(MaintenanceRequest.TaskName), "Tên công việc tối đa 255 ký tự và không chứa ký tự không hợp lệ.");
    }

    [Fact(DisplayName = "UTCID11 (A) - RepeatUnit không hợp lệ -> Validator từ chối")]
    public void UTCID11_InvalidRepeatUnit_FailsValidation()
    {
        var request = ValidRequest();
        request.RepeatUnit = "Year";

        AssertError(request, nameof(MaintenanceRequest.RepeatUnit), "Chọn chu kỳ bảo trì hợp lệ.");
    }

    [Fact(DisplayName = "UTCID12 (A) - RepeatEvery bằng 0 -> Validator từ chối")]
    public void UTCID12_RepeatEveryBelowMinimum_FailsValidation()
    {
        var request = ValidRequest();
        request.RepeatEvery = 0;

        AssertError(request, nameof(MaintenanceRequest.RepeatEvery), "Chu kỳ lặp từ 1 đến 365.");
    }

    [Fact(DisplayName = "UTCID13 (A) - RepeatEvery bằng 366 -> Validator từ chối")]
    public void UTCID13_RepeatEveryAboveMaximum_FailsValidation()
    {
        var request = ValidRequest();
        request.RepeatEvery = 366;

        AssertError(request, nameof(MaintenanceRequest.RepeatEvery), "Chu kỳ lặp từ 1 đến 365.");
    }

    [Fact(DisplayName = "UTCID14 (A) - StartDate mặc định -> Validator báo phải chọn ngày")]
    public void UTCID14_DefaultStartDate_FailsValidation()
    {
        var request = ValidRequest();
        request.StartDate = default;

        AssertError(request, nameof(MaintenanceRequest.StartDate), "Chọn ngày bắt đầu.");
    }

    [Fact(DisplayName = "UTCID15 (A) - AssignedTo rỗng -> Validator báo chọn người phụ trách")]
    public void UTCID15_EmptyAssignee_FailsValidation()
    {
        var request = ValidRequest();
        request.AssignedTo = Guid.Empty;

        AssertError(request, nameof(MaintenanceRequest.AssignedTo), "Chọn người phụ trách.");
    }

    [Fact(DisplayName = "UTCID16 (A) - RemindBeforeDays bằng -1 -> Validator từ chối")]
    public void UTCID16_ReminderBelowMinimum_FailsValidation()
    {
        var request = ValidRequest();
        request.RemindBeforeDays = -1;

        AssertError(request, nameof(MaintenanceRequest.RemindBeforeDays), "Nhắc trước từ 0 đến 365 ngày.");
    }

    [Fact(DisplayName = "UTCID17 (A) - RemindBeforeDays bằng 366 -> Validator từ chối")]
    public void UTCID17_ReminderAboveMaximum_FailsValidation()
    {
        var request = ValidRequest();
        request.RemindBeforeDays = 366;

        AssertError(request, nameof(MaintenanceRequest.RemindBeforeDays), "Nhắc trước từ 0 đến 365 ngày.");
    }

    private static MaintenanceController Controller(Mock<ISender> sender) =>
        new(sender.Object, NullLogger<MaintenanceController>.Instance);

    private static UpdateMaintenanceScheduleCommand ValidCommand() => new()
    {
        EquipmentId = Guid.NewGuid(),
        TaskName = "Vệ sinh máy bơm",
        RepeatUnit = "Month",
        RepeatEvery = 1,
        StartDate = new DateOnly(2026, 10, 27),
        AssignedTo = Guid.NewGuid(),
        RemindBeforeDays = 3
    };

    private static MaintenanceRequest ValidRequest() => new()
    {
        EquipmentId = Guid.NewGuid(),
        TaskName = "Vệ sinh máy bơm",
        RepeatUnit = "Month",
        RepeatEvery = 1,
        StartDate = new DateOnly(2026, 10, 27),
        AssignedTo = Guid.NewGuid(),
        RemindBeforeDays = 3
    };

    private static void AssertError(MaintenanceRequest request, string property, string message)
    {
        var result = new MaintenanceRequestValidator().Validate(request);
        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, error => error.PropertyName == property && error.ErrorMessage == message);
    }

    private static void AssertResponseMessage(ObjectResult response, string expected)
    {
        using var body = JsonDocument.Parse(JsonSerializer.Serialize(response.Value));
        Assert.Equal(expected, body.RootElement.GetProperty("message").GetString());
    }
}
