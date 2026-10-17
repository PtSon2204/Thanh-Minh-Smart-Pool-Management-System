using System.Linq.Expressions;
using FluentValidation;
using Microsoft.Extensions.Options;
using Moq;
using SmartPool.Application.Features.ManageEquipments.Commands.CreateEquipment;
using SmartPool.Application.Interfaces.Repositories;
using SmartPool.Application.Interfaces.Services;
using SmartPool.Domain.Entities;
using SmartPool.Infrastructure.Cameras;

namespace SmartPool.UnitTests.Features.ManageEquipments;

public sealed class CameraEquipmentTests
{
    [Theory]
    [InlineData("rtsp://127.0.0.1:80/live", "RTSP")]
    [InlineData("rtsp://127.0.0.2:554/live", "RTSP")]
    [InlineData("rtsp://user:password@127.0.0.1:554/live", "RTSP")]
    [InlineData("http://127.0.0.1:554/live", "HTTP")]
    [InlineData("file:///etc/passwd", "RTSP")]
    [InlineData("rtsp://169.254.169.254:554/live", "RTSP")]
    public async Task AddressPolicy_RejectsTargetsOutsideApprovedEndpointOrProtocol(string url, string protocol)
    {
        var policy = new CameraAddressPolicy(Options.Create(new CameraOptions { AllowedEndpoints = [new() { Host = "127.0.0.1", Port = 554 }] }));
        await Assert.ThrowsAsync<CameraConnectionException>(() => policy.ValidateAsync(new() { StreamUrl = url, Protocol = protocol }, CancellationToken.None));
    }

    [Fact]
    public async Task AddressPolicy_DefaultDeniesAndDoesNotTrustUnpinnedHostnames()
    {
        await Assert.ThrowsAsync<CameraConnectionException>(() => new CameraAddressPolicy(Options.Create(new CameraOptions()))
            .ValidateAsync(new() { StreamUrl = "rtsp://127.0.0.1:554/live" }, CancellationToken.None));
        await Assert.ThrowsAsync<CameraConnectionException>(() => new CameraAddressPolicy(Options.Create(new CameraOptions { AllowedEndpoints = [new() { Host = "localhost" }] }))
            .ValidateAsync(new() { StreamUrl = "rtsp://localhost:554/live" }, CancellationToken.None));
    }

    [Fact]
    public void Validator_RequiresCameraConfigurationAndRejectsConfigurationOnOtherGroups()
    {
        var validator = new CreateEquipmentValidator();
        Assert.False(validator.Validate(new CreateEquipmentCommand { Name = "Camera", Category = "Camera" }).IsValid);
        Assert.False(validator.Validate(new CreateEquipmentCommand { Name = "Pump", Category = "Pump", CameraConnection = new() }).IsValid);
        Assert.True(validator.Validate(new CreateEquipmentCommand { Name = "Camera", Category = "camera", CameraConnection = new() { StreamUrl = "rtsp://127.0.0.1:554/live" } }).IsValid);
        Assert.False(validator.Validate(new CreateEquipmentCommand { Name = "Camera", Category = "Camera", CameraConnection = new() { StreamUrl = "rtsp://user:password@127.0.0.1:554/live" } }).IsValid);
    }

    [Theory]
    [InlineData("{\"camera\":{\"password\":\"secret\"}}")]
    [InlineData("{\"url\":\"rtsp://user:secret@127.0.0.1/live\"}")]
    public void Validator_KeepsCredentialsOutOfPublicSpecifications(string json)
    {
        var command = new CreateEquipmentCommand
        {
            Name = "Camera", Category = "Camera", CameraConnection = new() { StreamUrl = "rtsp://127.0.0.1/live" },
            Metadata = System.Text.Json.JsonSerializer.Deserialize<System.Text.Json.JsonElement>(json)
        };
        Assert.Contains(new CreateEquipmentValidator().Validate(command).Errors, x => x.PropertyName == "Metadata");
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task CameraCreate_PublishesOnlyAfterStorageAndDisposesUncommittedRegistration(bool storageFails)
    {
        var repository = new Mock<IRepository<Equipment>>();
        repository.Setup(x => x.ExistsAsync(It.IsAny<Expression<Func<Equipment, bool>>>(), It.IsAny<CancellationToken>())).ReturnsAsync(true);
        var work = new Mock<IUnitOfWork>(MockBehavior.Strict);
        var operations = new Mock<IEquipmentOperations>();
        var cameras = new Mock<ICameraService>();
        var registration = new Mock<ICameraRegistration>();
        registration.SetupGet(x => x.Identity).Returns("identity");
        registration.SetupGet(x => x.ProtectedConfiguration).Returns("ciphertext");
        registration.Setup(x => x.DisposeAsync()).Returns(ValueTask.CompletedTask);
        cameras.Setup(x => x.GetIdentityAsync(It.IsAny<CameraConnectionConfiguration>(), It.IsAny<CancellationToken>())).ReturnsAsync("identity");
        cameras.Setup(x => x.PrepareAsync(It.IsAny<CameraConnectionConfiguration>(), It.IsAny<CancellationToken>())).ReturnsAsync(registration.Object);
        cameras.Setup(x => x.GetStatus(It.IsAny<Guid>())).Returns(new CameraConnectionStatus("Connected", DateTimeOffset.UtcNow));
        Equipment? stored = null;
        operations.Setup(x => x.RegisterCameraAsync(It.IsAny<Equipment>(), "identity", It.IsAny<Action>(), It.IsAny<CancellationToken>()))
            .Returns<Equipment, string, Action, CancellationToken>((entity, _, publish, _) =>
            {
                if (storageFails) throw new InvalidOperationException("storage unavailable");
                stored = entity;
                publish();
                return Task.CompletedTask;
            });
        var handler = new CreateEquipmentHandler(repository.Object, work.Object, new CreateEquipmentValidator(), TimeProvider.System, cameras.Object, operations.Object);
        var command = new CreateEquipmentCommand
        {
            Name = "Camera", Category = "Camera", CameraConnection = new() { StreamUrl = "rtsp://127.0.0.1:554/live", Username = "test", Password = "secret" }
        };
        if (storageFails)
        {
            await Assert.ThrowsAsync<InvalidOperationException>(() => handler.Handle(command, CancellationToken.None));
            registration.Verify(x => x.Confirm(), Times.Never);
            registration.Verify(x => x.Publish(It.IsAny<Guid>()), Times.Never);
        }
        else
        {
            var response = await handler.Handle(command, CancellationToken.None);
            Assert.NotNull(stored);
            Assert.DoesNotContain("secret", stored.Metadata!);
            Assert.Null(response.Metadata);
            Assert.Equal("Active", response.Status);
            registration.Verify(x => x.Publish(response.Id), Times.Once);
            registration.Verify(x => x.Confirm(), Times.Once);
        }
        registration.Verify(x => x.DisposeAsync(), Times.Once);
        repository.Verify(x => x.AddAsync(It.IsAny<Equipment>(), It.IsAny<CancellationToken>()), Times.Never);
        work.VerifyNoOtherCalls();
    }
}
