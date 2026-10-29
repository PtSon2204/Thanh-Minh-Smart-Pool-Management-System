using System.Text.Json;
using FluentValidation;
using MediatR;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using SmartPool.API.Controllers;
using SmartPool.Application.Features.ManageEquipments.Commands.UpdateEquipment;
using SmartPool.Application.Interfaces.Repositories;
using SmartPool.Domain.Entities;

namespace SmartPool.UnitTests.Features.ManageEquipments;

public sealed class UpdateEquipmentUseCaseTests
{
    [Fact(DisplayName = "UTCID01 (N) - Cập nhật thiết bị hợp lệ -> Trim field, cập nhật metadata/thời gian và lưu")]
    public async Task UTCID01_ValidCommand_UpdatesAndSavesEquipment()
    {
        var entity = Equipment();
        var previousUpdatedAt = entity.UpdatedAt;
        var previousCreatedAt = entity.CreatedAt;
        var repo = new Mock<IRepository<Equipment>>(MockBehavior.Strict);
        var uow = new Mock<IUnitOfWork>(MockBehavior.Strict);
        using var cts = new CancellationTokenSource();
        repo.Setup(r => r.GetByIdAsync(entity.Id, cts.Token)).ReturnsAsync(entity);
        repo.Setup(r => r.Update(entity));
        uow.Setup(x => x.SaveChangesAsync(cts.Token)).ReturnsAsync(1);
        using var metadataDocument = JsonDocument.Parse("{\"power\":\"3kW\"}");
        var time = new FixedTime();
        var command = new UpdateEquipmentCommand
        {
            Id = entity.Id, Name = "  Pump  ", Category = "  Equipment  ", Location = "  Zone  ",
            Metadata = metadataDocument.RootElement.Clone()
        };

        var result = await Handler(repo, uow, time).Handle(command, cts.Token);

        Assert.True(result);
        Assert.Equal("Pump", entity.Name);
        Assert.Equal("Equipment", entity.Category);
        Assert.Equal("Zone", entity.Location);
        Assert.Equal("{\"power\":\"3kW\"}", entity.Metadata);
        Assert.Equal("Active", entity.Status);
        Assert.Equal(previousCreatedAt, entity.CreatedAt);
        Assert.Equal(time.GetUtcNow().UtcDateTime, entity.UpdatedAt);
        Assert.NotEqual(previousUpdatedAt, entity.UpdatedAt);
        repo.Verify(r => r.GetByIdAsync(entity.Id, cts.Token), Times.Once);
        repo.Verify(r => r.Update(entity), Times.Once);
        uow.Verify(x => x.SaveChangesAsync(cts.Token), Times.Once);
        repo.VerifyNoOtherCalls();
        uow.VerifyNoOtherCalls();
    }

    [Fact(DisplayName = "UTCID02 (A) - Id không tìm thấy -> Trả false, không cập nhật/lưu")]
    public async Task UTCID02_MissingEquipment_ReturnsFalseWithoutSaving()
    {
        var id = Guid.NewGuid();
        var repo = new Mock<IRepository<Equipment>>(MockBehavior.Strict);
        var uow = new Mock<IUnitOfWork>(MockBehavior.Strict);
        repo.Setup(r => r.GetByIdAsync(id, CancellationToken.None)).ReturnsAsync((Equipment?)null);

        var result = await Handler(repo, uow, TimeProvider.System).Handle(ValidCommand(id), CancellationToken.None);

        Assert.False(result);
        repo.Verify(r => r.GetByIdAsync(id, CancellationToken.None), Times.Once);
        repo.VerifyNoOtherCalls();
        uow.VerifyNoOtherCalls();
    }

    [Fact(DisplayName = "UTCID03 (A) - Thiết bị đã xóa mềm -> Trả false, không cập nhật/lưu")]
    public async Task UTCID03_DeletedEquipment_ReturnsFalseWithoutSaving()
    {
        var entity = Equipment();
        entity.IsDeleted = true;
        var repo = new Mock<IRepository<Equipment>>(MockBehavior.Strict);
        var uow = new Mock<IUnitOfWork>(MockBehavior.Strict);
        repo.Setup(r => r.GetByIdAsync(entity.Id, CancellationToken.None)).ReturnsAsync(entity);

        var result = await Handler(repo, uow, TimeProvider.System).Handle(ValidCommand(entity.Id), CancellationToken.None);

        Assert.False(result);
        repo.Verify(r => r.GetByIdAsync(entity.Id, CancellationToken.None), Times.Once);
        repo.VerifyNoOtherCalls();
        uow.VerifyNoOtherCalls();
    }

    [Fact(DisplayName = "UTCID04 (A) - Chuyển từ thiết bị thường sang Camera -> Handler từ chối chuyển nhóm")]
    public async Task UTCID04_ChangingToCameraCategory_IsRejected()
    {
        var entity = Equipment();
        var repo = new Mock<IRepository<Equipment>>(MockBehavior.Strict);
        var uow = new Mock<IUnitOfWork>(MockBehavior.Strict);
        repo.Setup(r => r.GetByIdAsync(entity.Id, CancellationToken.None)).ReturnsAsync(entity);
        var command = ValidCommand(entity.Id);
        command.Category = "Camera";

        var error = await Assert.ThrowsAsync<ValidationException>(() => Handler(repo, uow, TimeProvider.System)
            .Handle(command, CancellationToken.None));

        Assert.Contains(error.Errors, item => item.PropertyName == nameof(UpdateEquipmentCommand.Category)
            && item.ErrorMessage == "Không thể chuyển đổi giữa Camera và nhóm thiết bị khác.");
        repo.Verify(r => r.GetByIdAsync(entity.Id, CancellationToken.None), Times.Once);
        repo.VerifyNoOtherCalls();
        uow.VerifyNoOtherCalls();
    }

    [Fact(DisplayName = "UTCID05 (A) - Đổi nhóm giữa Camera và nhóm thường -> ValidationException")]
    public async Task UTCID05_ChangingCameraCategory_IsRejected()
    {
        var entity = Equipment(category: "Camera");
        var repo = new Mock<IRepository<Equipment>>(MockBehavior.Strict);
        var uow = new Mock<IUnitOfWork>(MockBehavior.Strict);
        repo.Setup(r => r.GetByIdAsync(entity.Id, CancellationToken.None)).ReturnsAsync(entity);

        var error = await Assert.ThrowsAsync<ValidationException>(() => Handler(repo, uow, TimeProvider.System)
            .Handle(ValidCommand(entity.Id), CancellationToken.None));

        Assert.Contains(error.Errors, item => item.PropertyName == nameof(UpdateEquipmentCommand.Category)
            && item.ErrorMessage == "Không thể chuyển đổi giữa Camera và nhóm thiết bị khác.");
        repo.Verify(r => r.GetByIdAsync(entity.Id, CancellationToken.None), Times.Once);
        repo.VerifyNoOtherCalls();
        uow.VerifyNoOtherCalls();
    }

    [Fact(DisplayName = "UTCID06 (A) - Id rỗng -> Validator từ chối")]
    public void UTCID06_EmptyId_FailsValidation()
    {
        var result = new UpdateEquipmentValidator().Validate(ValidCommand(Guid.Empty));

        Assert.Contains(result.Errors, item => item.PropertyName == nameof(UpdateEquipmentCommand.Id));
    }

    [Fact(DisplayName = "UTCID07 (A) - Name rỗng -> Validator từ chối")]
    public void UTCID07_EmptyName_FailsValidation()
    {
        var command = ValidCommand(Guid.NewGuid());
        command.Name = " ";

        var result = new UpdateEquipmentValidator().Validate(command);

        Assert.Contains(result.Errors, item => item.ErrorMessage == "Tên thiết bị không được để trống.");
    }

    [Fact(DisplayName = "UTCID08 (B) - Field đúng giới hạn sau trim -> Validator chấp nhận")]
    public void UTCID08_ValuesAtMaximumLength_AreAccepted()
    {
        var command = ValidCommand(Guid.NewGuid());
        command.Name = " " + new string('N', 255) + " ";
        command.Category = new string('C', 50);
        command.Location = new string('L', 255);

        var result = new UpdateEquipmentValidator().Validate(command);

        Assert.True(result.IsValid);
        Assert.Empty(result.Errors);
    }

    [Fact(DisplayName = "UTCID09 (A) - Field vượt giới hạn và chứa NUL -> Validator từ chối")]
    public void UTCID09_InvalidTextLengthsAndNullCharacters_FailValidation()
    {
        var command = ValidCommand(Guid.NewGuid());
        command.Name = new string('N', 256);
        command.Category = new string('C', 51);
        command.Location = "Zone\0A";

        var result = new UpdateEquipmentValidator().Validate(command);

        Assert.Contains(result.Errors, item => item.ErrorMessage == "Tên thiết bị không được vượt quá 255 ký tự.");
        Assert.Contains(result.Errors, item => item.ErrorMessage == "Nhóm thiết bị không được vượt quá 50 ký tự.");
        Assert.Contains(result.Errors, item => item.ErrorMessage == "Vị trí chứa ký tự không hợp lệ.");
    }

    [Fact(DisplayName = "UTCID10 (A) - Controller nhận false từ handler -> HTTP 404")]
    public async Task UTCID10_ControllerMapsMissingEquipmentToNotFound()
    {
        var id = Guid.NewGuid();
        var sender = new Mock<ISender>(MockBehavior.Strict);
        sender.Setup(s => s.Send(It.Is<UpdateEquipmentCommand>(c => c.Id == id), CancellationToken.None))
            .ReturnsAsync(false);

        var result = await Controller(sender).Update(id, ValidCommand(Guid.Empty), CancellationToken.None);

        Assert.IsType<NotFoundObjectResult>(result);
        sender.Verify(s => s.Send(It.Is<UpdateEquipmentCommand>(c => c.Id == id), CancellationToken.None), Times.Once);
        sender.VerifyNoOtherCalls();
    }

    private static UpdateEquipmentHandler Handler(Mock<IRepository<Equipment>> repo,
        Mock<IUnitOfWork> unitOfWork, TimeProvider clock) =>
        new(repo.Object, unitOfWork.Object, new UpdateEquipmentValidator(), clock);

    private static EquipmentsController Controller(Mock<ISender> sender) =>
        new(sender.Object, NullLogger<EquipmentsController>.Instance);

    private static UpdateEquipmentCommand ValidCommand(Guid id) => new()
    {
        Id = id, Name = "Pump", Category = "Equipment", Location = "Zone"
    };

    private static Equipment Equipment(string category = "Equipment") => new()
    {
        Id = Guid.NewGuid(), Name = "Old pump", Category = category, Location = "Old zone",
        Metadata = "{\"power\":\"2kW\"}", Status = "Active", IsDeleted = false,
        CreatedAt = new DateTime(2025, 1, 1), UpdatedAt = new DateTime(2025, 2, 1)
    };

    private sealed class FixedTime : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => new(2026, 10, 28, 0, 0, 0, TimeSpan.Zero);
    }
}
