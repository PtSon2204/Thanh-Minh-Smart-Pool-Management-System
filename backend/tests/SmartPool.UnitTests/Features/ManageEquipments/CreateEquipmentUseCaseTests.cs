using System.Text.Json;
using FluentValidation;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging.Abstractions;
using MediatR;
using Moq;
using SmartPool.API.Controllers;
using SmartPool.Application.Features.ManageEquipments.Commands.CreateEquipment;
using SmartPool.Application.Features.ManageEquipments.Queries.GetEquipments;
using SmartPool.Application.Interfaces.Repositories;
using SmartPool.Domain.Entities;

namespace SmartPool.UnitTests.Features.ManageEquipments;

public sealed class CreateEquipmentUseCaseTests
{
    [Fact(DisplayName = "UTCID01 (N) - Dữ liệu hợp lệ -> Chuẩn hóa field, đặt mặc định và lưu thiết bị")]
    public async Task UTCID01_ValidRequest_CreatesEquipmentWithDefaults()
    {
        var repo = new Mock<IRepository<Equipment>>(MockBehavior.Strict);
        var uow = new Mock<IUnitOfWork>(MockBehavior.Strict);
        Equipment? saved = null;
        using var cts = new CancellationTokenSource();
        repo.Setup(r => r.AddAsync(It.IsAny<Equipment>(), cts.Token))
            .Callback<Equipment, CancellationToken>((entity, _) => saved = entity).Returns(Task.CompletedTask);
        uow.Setup(x => x.SaveChangesAsync(cts.Token)).ReturnsAsync(1);
        var time = new FixedTime();

        var result = await Handler(repo, uow, time).Handle(new CreateEquipmentCommand
        {
            Name = "  Máy bơm  ", Category = "  Pump  ", Location = "  Khu A  "
        }, cts.Token);

        Assert.NotNull(saved);
        Assert.NotEqual(Guid.Empty, saved.Id);
        Assert.Equal("Máy bơm", saved.Name);
        Assert.Equal("Pump", saved.Category);
        Assert.Equal("Khu A", saved.Location);
        Assert.Null(saved.Metadata);
        Assert.Equal("Active", saved.Status);
        Assert.False(saved.IsDeleted);
        Assert.Equal(time.GetUtcNow().UtcDateTime, saved.CreatedAt);
        Assert.Equal(saved.CreatedAt, saved.UpdatedAt);
        Assert.Empty(saved.MaintenanceSchedules);
        Assert.Equal(saved.Id, result.Id);
        Assert.Equal(saved.Name, result.Name);
        Assert.Equal(saved.Category, result.Category);
        Assert.Equal(saved.Location, result.Location);
        Assert.Equal("Active", result.Status);
        repo.Verify(r => r.AddAsync(saved, cts.Token), Times.Once);
        uow.Verify(x => x.SaveChangesAsync(cts.Token), Times.Once);
        repo.VerifyNoOtherCalls();
        uow.VerifyNoOtherCalls();
    }

    [Fact(DisplayName = "UTCID02 (A) - Name rỗng -> ValidationException, không thêm thiết bị")]
    public async Task UTCID02_EmptyName_FailsBeforeRepositoryCalls()
    {
        var repo = new Mock<IRepository<Equipment>>(MockBehavior.Strict);
        var uow = new Mock<IUnitOfWork>(MockBehavior.Strict);

        var error = await Assert.ThrowsAsync<ValidationException>(() => Handler(repo, uow, TimeProvider.System)
            .Handle(new CreateEquipmentCommand { Name = " ", Category = "Pump" }, CancellationToken.None));

        Assert.Contains(error.Errors, e => e.PropertyName == nameof(CreateEquipmentCommand.Name)
            && e.ErrorMessage == "Tên thiết bị không được để trống.");
        repo.VerifyNoOtherCalls();
        uow.VerifyNoOtherCalls();
    }

    [Fact(DisplayName = "UTCID03 (A) - Category rỗng -> ValidationException, không thêm thiết bị")]
    public async Task UTCID03_EmptyCategory_FailsBeforeRepositoryCalls()
    {
        var repo = new Mock<IRepository<Equipment>>(MockBehavior.Strict);
        var uow = new Mock<IUnitOfWork>(MockBehavior.Strict);

        var error = await Assert.ThrowsAsync<ValidationException>(() => Handler(repo, uow, TimeProvider.System)
            .Handle(new CreateEquipmentCommand { Name = "Pump", Category = " " }, CancellationToken.None));

        Assert.Contains(error.Errors, e => e.PropertyName == nameof(CreateEquipmentCommand.Category)
            && e.ErrorMessage == "Nhóm thiết bị không được để trống.");
        repo.VerifyNoOtherCalls();
        uow.VerifyNoOtherCalls();
    }

    [Fact(DisplayName = "UTCID04 (B) - Name dài 255 và Category dài 50 sau trim -> Validator chấp nhận")]
    public void UTCID04_MaximumNameAndCategoryLengths_AreAccepted()
    {
        var result = new CreateEquipmentValidator().Validate(new CreateEquipmentCommand
        {
            Name = " " + new string('N', 255) + " ",
            Category = new string('C', 50)
        });

        Assert.True(result.IsValid);
        Assert.Empty(result.Errors);
    }

    [Fact(DisplayName = "UTCID05 (A) - Name dài 256 ký tự -> Validator từ chối")]
    public void UTCID05_OverlengthName_FailsValidation()
    {
        var result = new CreateEquipmentValidator().Validate(new CreateEquipmentCommand
        {
            Name = new string('N', 256), Category = "Pump"
        });

        Assert.Contains(result.Errors, e => e.PropertyName == nameof(CreateEquipmentCommand.Name)
            && e.ErrorMessage == "Tên thiết bị không được vượt quá 255 ký tự.");
    }

    [Fact(DisplayName = "UTCID06 (A) - Category dài 51 ký tự -> Validator từ chối")]
    public void UTCID06_OverlengthCategory_FailsValidation()
    {
        var result = new CreateEquipmentValidator().Validate(new CreateEquipmentCommand
        {
            Name = "Pump", Category = new string('C', 51)
        });

        Assert.Contains(result.Errors, e => e.PropertyName == nameof(CreateEquipmentCommand.Category)
            && e.ErrorMessage == "Nhóm thiết bị không được vượt quá 50 ký tự.");
    }

    [Fact(DisplayName = "UTCID07 (A) - Location dài 256 ký tự -> Validator từ chối")]
    public void UTCID07_OverlengthLocation_FailsValidation()
    {
        var result = new CreateEquipmentValidator().Validate(new CreateEquipmentCommand
        {
            Name = "Pump", Category = "Pump", Location = new string('L', 256)
        });

        Assert.Contains(result.Errors, e => e.PropertyName == nameof(CreateEquipmentCommand.Location)
            && e.ErrorMessage == "Vị trí không được vượt quá 255 ký tự.");
    }

    [Fact(DisplayName = "UTCID08 (A) - Name chứa NUL -> Validator từ chối")]
    public void UTCID08_NullCharacterInName_FailsValidation()
    {
        var result = new CreateEquipmentValidator().Validate(new CreateEquipmentCommand
        {
            Name = "Pump\0", Category = "Pump"
        });

        Assert.Contains(result.Errors, e => e.PropertyName == nameof(CreateEquipmentCommand.Name)
            && e.ErrorMessage == "Tên thiết bị chứa ký tự không hợp lệ.");
    }

    [Fact(DisplayName = "UTCID09 (N) - Metadata là JSON hợp lệ -> Lưu metadata dạng raw JSON")]
    public async Task UTCID09_ValidMetadata_IsStoredAsRawJson()
    {
        var repo = new Mock<IRepository<Equipment>>();
        var uow = new Mock<IUnitOfWork>();
        Equipment? saved = null;
        repo.Setup(r => r.AddAsync(It.IsAny<Equipment>(), It.IsAny<CancellationToken>()))
            .Callback<Equipment, CancellationToken>((entity, _) => saved = entity).Returns(Task.CompletedTask);
        uow.Setup(x => x.SaveChangesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(1);
        using var document = JsonDocument.Parse("{\"power\":\"2kW\",\"sensors\":[1,2]}");

        var result = await Handler(repo, uow, TimeProvider.System).Handle(new CreateEquipmentCommand
        {
            Name = "Pump", Category = "Pump", Metadata = document.RootElement.Clone()
        }, CancellationToken.None);

        Assert.Equal("{\"power\":\"2kW\",\"sensors\":[1,2]}", saved!.Metadata);
        Assert.Equal(saved.Id, result.Id);
        repo.Verify(r => r.AddAsync(saved, It.IsAny<CancellationToken>()), Times.Once);
        uow.Verify(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact(DisplayName = "UTCID10 (A) - SaveChangesAsync lỗi -> Handler truyền exception, không trả kết quả thành công")]
    public async Task UTCID10_SaveFailure_IsPropagated()
    {
        var repo = new Mock<IRepository<Equipment>>();
        var uow = new Mock<IUnitOfWork>();
        repo.Setup(r => r.AddAsync(It.IsAny<Equipment>(), It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
        var failure = new InvalidOperationException("storage unavailable");
        uow.Setup(x => x.SaveChangesAsync(It.IsAny<CancellationToken>())).ThrowsAsync(failure);

        var actual = await Assert.ThrowsAsync<InvalidOperationException>(() => Handler(repo, uow, TimeProvider.System)
            .Handle(new CreateEquipmentCommand { Name = "Pump", Category = "Pump" }, CancellationToken.None));

        Assert.Same(failure, actual);
        uow.Verify(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact(DisplayName = "UTCID11 (A) - Sender ném exception hệ thống -> Controller trả HTTP 500, không lộ chi tiết lưu trữ")]
    public async Task UTCID11_ControllerHidesStorageFailureDetails()
    {
        var sender = new Mock<ISender>(MockBehavior.Strict);
        sender.Setup(s => s.Send(It.IsAny<CreateEquipmentCommand>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("internal database connection details"));
        var controller = new EquipmentsController(sender.Object, NullLogger<EquipmentsController>.Instance);

        var response = Assert.IsType<ObjectResult>(await controller.Create(
            new CreateEquipmentCommand { Name = "Pump", Category = "Pump" }, CancellationToken.None));

        Assert.Equal(500, response.StatusCode);
        using var json = JsonDocument.Parse(JsonSerializer.Serialize(response.Value));
        var message = json.RootElement.GetProperty("message").GetString();
        Assert.Equal("Thêm thiết bị thất bại. Vui lòng thử lại.", message);
        Assert.DoesNotContain("internal database", message);
        sender.Verify(s => s.Send(It.IsAny<CreateEquipmentCommand>(), It.IsAny<CancellationToken>()), Times.Once);
        sender.VerifyNoOtherCalls();
    }

    private static CreateEquipmentHandler Handler(Mock<IRepository<Equipment>> repo,
        Mock<IUnitOfWork> unitOfWork, TimeProvider timeProvider) =>
        new(repo.Object, unitOfWork.Object, new CreateEquipmentValidator(), timeProvider);

    private sealed class FixedTime : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => new(2026, 10, 28, 0, 0, 0, TimeSpan.Zero);
    }
}
