using System.Text.Json;
using FluentValidation;
using FluentValidation.Results;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using SmartPool.API.Controllers;
using SmartPool.Application.Features.ManageEquipments.Maintenance;
using SmartPool.Application.Interfaces.Repositories;

namespace SmartPool.UnitTests.Features.ManageEquipments.Maintenance;

public sealed class MaintenanceSchedulingTests
{
    [Fact(DisplayName = "UTCID01 (N) - Lập lịch hợp lệ -> Trả HTTP 201 với lịch được tạo")]
    public async Task UTCID01_CreateValidSchedule_ReturnsCreatedSchedule()
    {
        var request = ValidRequest();
        var expected = new MaintenanceItem(Guid.NewGuid(), request.EquipmentId, "Máy bơm", "Khu A", request.TaskName,
            request.RepeatUnit, request.RepeatEvery, request.StartDate, request.AssignedTo, "Nhân viên A",
            request.RemindBeforeDays, "Pending", false, null);
        var operations = new Mock<IMaintenanceOperations>(MockBehavior.Strict);
        using var cts = new CancellationTokenSource();
        operations.Setup(o => o.CreateAsync(request, cts.Token)).ReturnsAsync(expected);
        var controller = Controller(operations);

        var result = await controller.Create(request, cts.Token);

        var created = Assert.IsType<ObjectResult>(result);
        Assert.Equal(201, created.StatusCode);
        Assert.Same(expected, created.Value);
        operations.Verify(o => o.CreateAsync(request, cts.Token), Times.Once);
        operations.VerifyNoOtherCalls();
    }

    [Fact(DisplayName = "UTCID02 (A) - CreateAsync ném ValidationException -> Controller trả HTTP 400 kèm errors")]
    public async Task UTCID02_ValidationException_ReturnsBadRequestWithErrors()
    {
        var request = ValidRequest();
        var operations = new Mock<IMaintenanceOperations>(MockBehavior.Strict);
        operations.Setup(o => o.CreateAsync(request, CancellationToken.None))
            .ThrowsAsync(new ValidationException([new ValidationFailure("TaskName", "Nhập tên công việc.")]));

        var result = await Controller(operations).Create(request, CancellationToken.None);

        var badRequest = Assert.IsType<BadRequestObjectResult>(result);
        Assert.Equal(400, badRequest.StatusCode);
        using var body = JsonDocument.Parse(JsonSerializer.Serialize(badRequest.Value));
        Assert.Equal("Vui lòng kiểm tra thông tin lịch bảo trì.", body.RootElement.GetProperty("message").GetString());
        Assert.Equal("Nhập tên công việc.", body.RootElement.GetProperty("errors").GetProperty("TaskName")[0].GetString());
        operations.Verify(o => o.CreateAsync(request, CancellationToken.None), Times.Once);
        operations.VerifyNoOtherCalls();
    }

    [Fact(DisplayName = "UTCID03 (A) - CreateAsync ném MaintenanceException -> Controller giữ status code lỗi")]
    public async Task UTCID03_MaintenanceException_ReturnsItsStatusCode()
    {
        var request = ValidRequest();
        var operations = new Mock<IMaintenanceOperations>(MockBehavior.Strict);
        operations.Setup(o => o.CreateAsync(request, CancellationToken.None))
            .ThrowsAsync(new MaintenanceException(404, "Thiết bị không tồn tại hoặc đã bị xóa."));

        var result = await Controller(operations).Create(request, CancellationToken.None);

        var response = Assert.IsType<ObjectResult>(result);
        Assert.Equal(404, response.StatusCode);
        using var body = JsonDocument.Parse(JsonSerializer.Serialize(response.Value));
        Assert.Equal("Thiết bị không tồn tại hoặc đã bị xóa.", body.RootElement.GetProperty("message").GetString());
        operations.Verify(o => o.CreateAsync(request, CancellationToken.None), Times.Once);
        operations.VerifyNoOtherCalls();
    }

    [Fact(DisplayName = "UTCID04 (A) - CreateAsync ném exception hệ thống -> Controller trả HTTP 500")]
    public async Task UTCID04_UnexpectedException_ReturnsInternalServerError()
    {
        var request = ValidRequest();
        var operations = new Mock<IMaintenanceOperations>(MockBehavior.Strict);
        operations.Setup(o => o.CreateAsync(request, CancellationToken.None))
            .ThrowsAsync(new InvalidOperationException("Database unavailable"));

        var result = await Controller(operations).Create(request, CancellationToken.None);

        var response = Assert.IsType<ObjectResult>(result);
        Assert.Equal(500, response.StatusCode);
        using var body = JsonDocument.Parse(JsonSerializer.Serialize(response.Value));
        Assert.Equal("Không thể xử lý lịch bảo trì. Vui lòng thử lại.", body.RootElement.GetProperty("message").GetString());
        operations.Verify(o => o.CreateAsync(request, CancellationToken.None), Times.Once);
        operations.VerifyNoOtherCalls();
    }

    [Fact(DisplayName = "UTCID05 (B) - Request ở giới hạn hợp lệ -> Validator chấp nhận")]
    public void UTCID05_RequestAtInclusiveLimits_PassesValidation()
    {
        var request = ValidRequest();
        request.TaskName = new string('A', 255);
        request.RepeatEvery = 365;
        request.RemindBeforeDays = 365;

        var result = new MaintenanceRequestValidator().Validate(request);

        Assert.True(result.IsValid);
        Assert.Empty(result.Errors);
    }

    [Fact(DisplayName = "UTCID06 (A) - EquipmentId rỗng -> Validator báo phải chọn thiết bị")]
    public void UTCID06_EmptyEquipmentId_FailsValidation()
    {
        var request = ValidRequest();
        request.EquipmentId = Guid.Empty;

        var result = new MaintenanceRequestValidator().Validate(request);

        AssertError(result, nameof(MaintenanceRequest.EquipmentId), "Chọn thiết bị.");
    }

    [Fact(DisplayName = "UTCID07 (A) - TaskName rỗng -> Validator báo nhập tên công việc")]
    public void UTCID07_EmptyTaskName_FailsValidation()
    {
        var request = ValidRequest();
        request.TaskName = string.Empty;

        var result = new MaintenanceRequestValidator().Validate(request);

        AssertError(result, nameof(MaintenanceRequest.TaskName), "Nhập tên công việc.");
    }

    [Fact(DisplayName = "UTCID08 (A) - TaskName dài 256 ký tự -> Validator từ chối")]
    public void UTCID08_OverlengthTaskName_FailsValidation()
    {
        var request = ValidRequest();
        request.TaskName = new string('A', 256);

        var result = new MaintenanceRequestValidator().Validate(request);

        AssertError(result, nameof(MaintenanceRequest.TaskName), "Tên công việc tối đa 255 ký tự và không chứa ký tự không hợp lệ.");
    }

    [Fact(DisplayName = "UTCID09 (A) - TaskName chứa NUL -> Validator từ chối")]
    public void UTCID09_TaskNameContainsNullCharacter_FailsValidation()
    {
        var request = ValidRequest();
        request.TaskName = "Bảo trì\0máy bơm";

        var result = new MaintenanceRequestValidator().Validate(request);

        AssertError(result, nameof(MaintenanceRequest.TaskName), "Tên công việc tối đa 255 ký tự và không chứa ký tự không hợp lệ.");
    }

    [Fact(DisplayName = "UTCID10 (A) - RepeatUnit không thuộc None/Day/Week/Month -> Validator từ chối")]
    public void UTCID10_InvalidRepeatUnit_FailsValidation()
    {
        var request = ValidRequest();
        request.RepeatUnit = "Year";

        var result = new MaintenanceRequestValidator().Validate(request);

        AssertError(result, nameof(MaintenanceRequest.RepeatUnit), "Chọn chu kỳ bảo trì hợp lệ.");
    }

    [Fact(DisplayName = "UTCID11 (A) - RepeatEvery bằng 0 -> Validator từ chối")]
    public void UTCID11_RepeatEveryBelowMinimum_FailsValidation()
    {
        var request = ValidRequest();
        request.RepeatEvery = 0;

        var result = new MaintenanceRequestValidator().Validate(request);

        AssertError(result, nameof(MaintenanceRequest.RepeatEvery), "Chu kỳ lặp từ 1 đến 365.");
    }

    [Fact(DisplayName = "UTCID12 (A) - RepeatEvery bằng 366 -> Validator từ chối")]
    public void UTCID12_RepeatEveryAboveMaximum_FailsValidation()
    {
        var request = ValidRequest();
        request.RepeatEvery = 366;

        var result = new MaintenanceRequestValidator().Validate(request);

        AssertError(result, nameof(MaintenanceRequest.RepeatEvery), "Chu kỳ lặp từ 1 đến 365.");
    }

    [Fact(DisplayName = "UTCID13 (A) - StartDate mặc định -> Validator báo phải chọn ngày")]
    public void UTCID13_DefaultStartDate_FailsValidation()
    {
        var request = ValidRequest();
        request.StartDate = default;

        var result = new MaintenanceRequestValidator().Validate(request);

        AssertError(result, nameof(MaintenanceRequest.StartDate), "Chọn ngày bắt đầu.");
    }

    [Fact(DisplayName = "UTCID14 (A) - AssignedTo rỗng -> Validator báo phải chọn người phụ trách")]
    public void UTCID14_EmptyAssignee_FailsValidation()
    {
        var request = ValidRequest();
        request.AssignedTo = Guid.Empty;

        var result = new MaintenanceRequestValidator().Validate(request);

        AssertError(result, nameof(MaintenanceRequest.AssignedTo), "Chọn người phụ trách.");
    }

    [Fact(DisplayName = "UTCID15 (A) - RemindBeforeDays bằng -1 -> Validator từ chối")]
    public void UTCID15_ReminderBelowMinimum_FailsValidation()
    {
        var request = ValidRequest();
        request.RemindBeforeDays = -1;

        var result = new MaintenanceRequestValidator().Validate(request);

        AssertError(result, nameof(MaintenanceRequest.RemindBeforeDays), "Nhắc trước từ 0 đến 365 ngày.");
    }

    [Fact(DisplayName = "UTCID16 (A) - RemindBeforeDays bằng 366 -> Validator từ chối")]
    public void UTCID16_ReminderAboveMaximum_FailsValidation()
    {
        var request = ValidRequest();
        request.RemindBeforeDays = 366;

        var result = new MaintenanceRequestValidator().Validate(request);

        AssertError(result, nameof(MaintenanceRequest.RemindBeforeDays), "Nhắc trước từ 0 đến 365 ngày.");
    }

    private static MaintenanceController Controller(Mock<IMaintenanceOperations> operations) =>
        new(operations.Object, NullLogger<MaintenanceController>.Instance);

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

    private static void AssertError(FluentValidation.Results.ValidationResult result, string property, string message)
    {
        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, error => error.PropertyName == property && error.ErrorMessage == message);
    }
}
