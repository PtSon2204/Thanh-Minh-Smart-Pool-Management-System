using SmartPool.Application.Features.ManageEquipments.Cameras;
using SmartPool.Application.Features.ManageEquipments;
using System.Linq.Expressions;
using FluentValidation;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Moq;
using SmartPool.Application.Features.ManageEquipments.Commands.CreateEquipment;
using SmartPool.Application.Interfaces.Repositories;
using SmartPool.Application.Interfaces.Services;
using SmartPool.Domain.Entities;
using SmartPool.Infrastructure.Cameras;
using SmartPool.Infrastructure;

namespace SmartPool.UnitTests.Features.ManageEquipments;

public sealed class CameraEquipmentTests
{
    [Fact]
    public void SimulatorRetry_IsFast_AndPhysicalCameraKeepsConfiguredDelay()
    {
        var options = new CameraOptions { ReconnectDelaySeconds = 7 };
        Assert.Equal(TimeSpan.FromMilliseconds(250), options.GetReconnectDelay(true));
        Assert.Equal(TimeSpan.FromSeconds(7), options.GetReconnectDelay(false));
        options.SimulatorReconnectDelayMilliseconds = 0;
        Assert.Equal(TimeSpan.FromMilliseconds(100), options.GetReconnectDelay(true));
        options.SimulatorReconnectDelayMilliseconds = 60000;
        Assert.Equal(TimeSpan.FromSeconds(2), options.GetReconnectDelay(true));
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task ViewConfiguration_EnablesFastRetryOnlyForSimulatorSession(bool simulator)
    {
        var id = Guid.NewGuid();
        var equipment = new Mock<IEquipmentOperations>();
        equipment.Setup(x => x.GetByIdAsync(id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Equipment { Id = id, Category = "Camera", Status = "Active" });
        var streams = new Mock<ICameraStreamService>();
        streams.Setup(x => x.GetConfiguration()).Returns(new CameraWebRtcConfiguration([]));
        var cameras = new Mock<ICameraService>();
        cameras.Setup(x => x.GetStatus(id)).Returns(new CameraConnectionStatus("Connecting", null)
            { AnalysisSession = simulator ? "simulator-session" : null });
        var handler = new SmartPool.Application.Features.ManageEquipments.Queries.GetCameraViewConfig.GetCameraViewConfigHandler(
            equipment.Object, streams.Object, cameras.Object);
        var result = await handler.Handle(new(id), default);
        Assert.Equal(simulator, result.FastReconnect);
    }

    [Theory]
    [InlineData("Production", false)]
    [InlineData("Staging", false)]
    [InlineData("Development", true)]
    [InlineData("Testing", true)]
    public void SimulatorEndpoint_RequiresDevelopmentOrTesting(string environment, bool allowed)
    {
        var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?> {
            ["JwtSettings:SecretKey"] = "simulator-options-test-signing-key-32-bytes",
            ["CloudinarySettings:CloudName"] = "test", ["CloudinarySettings:ApiKey"] = "test", ["CloudinarySettings:ApiSecret"] = "test"
        }).Build();
        var services = new ServiceCollection();
        var host = new Mock<IHostEnvironment>(); host.SetupGet(x => x.EnvironmentName).Returns(environment);
        services.AddSingleton(host.Object);
        services.AddLogging();
        services.AddInfrastructureServices(config);
        // Không khởi tạo key-encryption certificate phụ thuộc OS trong test validation options.
        services.Configure<CameraOptions>(camera => camera.AllowedEndpoints = [new() { Host = "127.0.0.1", Port = 8554, Simulator = true }]);
        using var provider = services.BuildServiceProvider();
        if (allowed) Assert.True(provider.GetRequiredService<IOptions<CameraOptions>>().Value.AllowedEndpoints.Single().Simulator);
        else Assert.Throws<OptionsValidationException>(() => provider.GetRequiredService<IOptions<CameraOptions>>().Value);
    }

    [Fact]
    public async Task Simulator_DistinguishesStreamsAndKeepsCredentialFreeRtsp()
    {
        var policy = new CameraAddressPolicy(Options.Create(new CameraOptions
        { AllowedEndpoints = [new() { Host = "127.0.0.1", Port = 8554, Simulator = true }] }));
        var first = await policy.ValidateAsync(new() { StreamUrl = "rtsp://127.0.0.1:8554/pool1" }, default);
        var second = await policy.ValidateAsync(new() { StreamUrl = "rtsp://127.0.0.1:8554/pool2" }, default);
        var repeated = await policy.ValidateAsync(new() { StreamUrl = "rtsp://127.0.0.1:8554/pool1" }, default);
        Assert.True(first.Simulator);
        Assert.NotEqual(first.Identity, second.Identity);
        Assert.Equal(first.Identity, repeated.Identity);
        Assert.Empty(first.Uri.UserInfo);
    }

    [Fact]
    public async Task PhysicalCamera_KeepsExistingIdentityAndDoesNotEnableSimulatorReset()
    {
        var policy = new CameraAddressPolicy(Options.Create(new CameraOptions
        { AllowedEndpoints = [new() { Host = "127.0.0.1", Port = 554 }] }));
        var target = await policy.ValidateAsync(new() { StreamUrl = "rtsp://127.0.0.1/live" }, default);
        var expected = Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes("127.0.0.1:554")));
        Assert.Equal(expected, target.Identity);
        Assert.False(target.Simulator);
    }

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
        var handler = new CreateEquipmentHandler(operations.Object, new CreateEquipmentValidator(), TimeProvider.System, cameras.Object);
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
    }
}
