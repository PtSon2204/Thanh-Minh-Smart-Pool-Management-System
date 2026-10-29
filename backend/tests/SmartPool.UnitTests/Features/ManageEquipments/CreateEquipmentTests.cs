using System.Text.Json;
using FluentValidation;
using Moq;
using SmartPool.Application.Features.ManageEquipments.Commands.CreateEquipment;
using SmartPool.Application.Interfaces.Repositories;
using SmartPool.Domain.Entities;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging.Abstractions;
using MediatR;
using SmartPool.API.Controllers;

namespace SmartPool.UnitTests.Features.ManageEquipments;

public sealed class CreateEquipmentTests
{
    [Theory]
    [InlineData("", "Pump")]
    [InlineData("   ", "Pump")]
    [InlineData(null, "Pump")]
    [InlineData("Pump", "")]
    [InlineData("Pump", "   ")]
    [InlineData("Pump", null)]
    public async Task MissingRequiredData_DoesNotInsertOrSave(string? name, string? category)
    {
        var repository = new Mock<IRepository<Equipment>>(MockBehavior.Strict);
        var work = new Mock<IUnitOfWork>(MockBehavior.Strict);
        var handler = new CreateEquipmentHandler(repository.Object, work.Object, new CreateEquipmentValidator(), TimeProvider.System);
        await Assert.ThrowsAsync<ValidationException>(() => handler.Handle(new CreateEquipmentCommand
        {
            Name = name!, Category = category!
        }, CancellationToken.None));
        repository.VerifyNoOtherCalls();
        work.VerifyNoOtherCalls();
    }

    [Fact]
    public void Validator_EnforcesDatabaseLengthsAndRejectsNullCharacters()
    {
        var validator = new CreateEquipmentValidator();
        Assert.Equal(3, validator.Validate(new CreateEquipmentCommand
        {
            Name = new string('a', 256), Category = new string('b', 51), Location = new string('c', 256)
        }).Errors.Count);
        Assert.Equal(3, validator.Validate(new CreateEquipmentCommand
        {
            Name = "name\0", Category = "category\0", Location = "location\0"
        }).Errors.Count);
        Assert.True(validator.Validate(new CreateEquipmentCommand
        {
            Name = " " + new string('a', 255) + " ", Category = new string('b', 50), Location = new string('c', 255)
        }).IsValid);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("null")]
    [InlineData("{\"power\":\"2kW\",\"sensors\":[1,2],\"dimensions\":{\"width\":20}}")]
    public async Task Create_TrimsFieldsAndSetsSystemDefaultsWithoutCreatingMaintenance(string? json)
    {
        var repository = new Mock<IRepository<Equipment>>();
        var work = new Mock<IUnitOfWork>();
        repository.Setup(x => x.ExistsAsync(It.IsAny<System.Linq.Expressions.Expression<Func<Equipment, bool>>>(), It.IsAny<CancellationToken>())).ReturnsAsync(true);
        Equipment? saved = null;
        repository.Setup(x => x.AddAsync(It.IsAny<Equipment>(), It.IsAny<CancellationToken>()))
            .Callback<Equipment, CancellationToken>((entity, _) => saved = entity).Returns(Task.CompletedTask);
        work.Setup(x => x.SaveChangesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(1);
        var handler = new CreateEquipmentHandler(repository.Object, work.Object, new CreateEquipmentValidator(), new FixedTime());
        var result = await handler.Handle(new CreateEquipmentCommand
        {
            Name = "  Máy bơm  ", Category = "  Pump  ", Location = "   ",
            Metadata = json == null ? null : JsonSerializer.Deserialize<JsonElement>(json)
        }, CancellationToken.None);
        Assert.NotNull(saved);
        Assert.NotEqual(Guid.Empty, saved.Id);
        Assert.Equal("Máy bơm", saved.Name);
        Assert.Equal("Pump", saved.Category);
        Assert.Null(saved.Location);
        Assert.Equal(json is null or "null" ? null : json, saved.Metadata);
        Assert.Equal("Active", saved.Status);
        Assert.False(saved.IsDeleted);
        Assert.Equal(new FixedTime().GetUtcNow().UtcDateTime, saved.CreatedAt);
        Assert.Equal(saved.CreatedAt, saved.UpdatedAt);
        Assert.Equal(saved.Id, result.Id);
        work.Verify(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task NewCategory_IsInsertedAndSaved()
    {
        var repository = new Mock<IRepository<Equipment>>();
        repository.Setup(x => x.ExistsAsync(It.IsAny<System.Linq.Expressions.Expression<Func<Equipment, bool>>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((System.Linq.Expressions.Expression<Func<Equipment, bool>> predicate, CancellationToken _) =>
                new[] { new Equipment { Category = "Existing", IsDeleted = false } }.Any(predicate.Compile()));
        Equipment? saved = null;
        repository.Setup(x => x.AddAsync(It.IsAny<Equipment>(), It.IsAny<CancellationToken>()))
            .Callback<Equipment, CancellationToken>((entity, _) => saved = entity).Returns(Task.CompletedTask);
        var work = new Mock<IUnitOfWork>(MockBehavior.Strict);
        work.Setup(x => x.SaveChangesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(1);
        var handler = new CreateEquipmentHandler(repository.Object, work.Object, new CreateEquipmentValidator(), TimeProvider.System);
        var result = await handler.Handle(new CreateEquipmentCommand
        {
            Name = "Pump", Category = "  New group  "
        }, CancellationToken.None);
        Assert.NotNull(saved);
        Assert.Equal("Pump", saved.Name);
        Assert.Equal("New group", saved.Category);
        Assert.Equal(saved.Id, result.Id);
        Assert.Equal(saved.Category, result.Category);
        repository.Verify(x => x.AddAsync(saved, It.IsAny<CancellationToken>()), Times.Once);
        work.Verify(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
        work.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task FirstEquipment_AllowsEnteringFirstCategory()
    {
        var repository = new Mock<IRepository<Equipment>>();
        var work = new Mock<IUnitOfWork>();
        repository.Setup(x => x.AddAsync(It.IsAny<Equipment>(), It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
        work.Setup(x => x.SaveChangesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(1);
        var handler = new CreateEquipmentHandler(repository.Object, work.Object, new CreateEquipmentValidator(), TimeProvider.System);
        var result = await handler.Handle(new CreateEquipmentCommand { Name = "Pump", Category = "  First group  " }, CancellationToken.None);
        Assert.Equal("First group", result.Category);
        work.Verify(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Controller_DoesNotExposeStorageErrorDetails()
    {
        var sender = new Mock<ISender>();
        sender.Setup(x => x.Send(It.IsAny<CreateEquipmentCommand>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("internal database connection details"));
        var controller = new EquipmentsController(sender.Object, NullLogger<EquipmentsController>.Instance);
        var result = Assert.IsType<ObjectResult>(await controller.Create(new CreateEquipmentCommand(), CancellationToken.None));
        Assert.Equal(500, result.StatusCode);
        var json = JsonSerializer.Serialize(result.Value);
        Assert.DoesNotContain("internal database", json);
    }

    [Fact]
    public async Task SaveFailure_DoesNotReturnSuccess()
    {
        var repository = new Mock<IRepository<Equipment>>();
        var work = new Mock<IUnitOfWork>();
        repository.Setup(x => x.ExistsAsync(It.IsAny<System.Linq.Expressions.Expression<Func<Equipment, bool>>>(), It.IsAny<CancellationToken>())).ReturnsAsync(true);
        repository.Setup(x => x.AddAsync(It.IsAny<Equipment>(), It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
        work.Setup(x => x.SaveChangesAsync(It.IsAny<CancellationToken>())).ThrowsAsync(new InvalidOperationException("storage unavailable"));
        var handler = new CreateEquipmentHandler(repository.Object, work.Object, new CreateEquipmentValidator(), TimeProvider.System);
        await Assert.ThrowsAsync<InvalidOperationException>(() => handler.Handle(new CreateEquipmentCommand
        {
            Name = "Pump", Category = "Pump"
        }, CancellationToken.None));
    }

    private sealed class FixedTime : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => new(2026, 10, 16, 0, 0, 0, TimeSpan.Zero);
    }
}
