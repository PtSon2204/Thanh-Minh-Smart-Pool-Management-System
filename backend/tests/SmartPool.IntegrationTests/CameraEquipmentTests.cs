using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using SmartPool.Application.Features.ManageEquipments;
using SmartPool.Application.Features.ManageEquipments.Commands.CreateEquipment;
using SmartPool.Application.Features.ManageEquipments.Queries.GetEquipments;
using SmartPool.Application.Interfaces.Services;
using SmartPool.Infrastructure.Cameras;
using SmartPool.Infrastructure.Persistence;
using SmartPool.Infrastructure.Persistence.DbContext;
using SmartPool.Infrastructure.Persistence.Repositories;
using SmartPool.Domain.Entities;

namespace SmartPool.IntegrationTests;

public sealed class CameraEquipmentTests
{
    private static string Ffmpeg => Environment.GetEnvironmentVariable("SMARTPOOL_CAMERA_TEST_FFMPEG")
        ?? throw new InvalidOperationException("Set SMARTPOOL_CAMERA_TEST_FFMPEG to an FFmpeg executable to run camera integration tests.");

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task RealRtsp_ValidatesDecodedFramePersistsEncryptedConfigurationRejectsDuplicateAndReportsDisconnect(bool hevc)
    {
        await using var server = await LocalRtspCamera.StartAsync(Ffmpeg, hevc);
        server.RequireAuthentication = true;
        await using var db = new SmartPoolDbContext(await IntegrationTestDatabase.CreateOptionsAsync());
        var source = new Equipment { Id = Guid.NewGuid(), Name = "Camera category source", Category = "Camera", IsDeleted = false };
        db.Equipments.Add(source);
        await db.SaveChangesAsync();
        var options = Options.Create(new CameraOptions
        {
            AllowedEndpoints = [new CameraEndpoint { Host = "127.0.0.1", Port = server.Port }], FfmpegPath = Ffmpeg,
            ConnectionTimeoutSeconds = 8, FrameStaleSeconds = 3, ReconnectDelaySeconds = 10
        });
        var protection = new EphemeralDataProtectionProvider();
        await using var cameras = new CameraService(new CameraAddressPolicy(options), new CameraFrameDecoder(options), protection, options, TimeProvider.System, NullLogger<CameraService>.Instance);
        var operations = new EquipmentOperations(db);
        var handler = new CreateEquipmentHandler(new Repository<Equipment>(db), new UnitOfWork(db), new CreateEquipmentValidator(), TimeProvider.System, cameras, operations);
        var request = new CreateEquipmentCommand
        {
            Name = "Camera integration " + Guid.NewGuid(), Category = "Camera",
            CameraConnection = new() { StreamUrl = server.Url, Username = "camera-test", Password = "camera-password-test" }
        };
        Guid? createdId = null;
        try
        {
            var created = await handler.Handle(request, CancellationToken.None);
            createdId = created.Id;
            Assert.Equal("Active", created.Status);
            Assert.Equal("Connected", created.CameraConnection!.State);
            Assert.NotNull(cameras.GetFrame(created.Id));
            var stored = await db.Equipments.AsNoTracking().SingleAsync(x => x.Id == created.Id);
            Assert.False(stored.IsDeleted);
            Assert.DoesNotContain("camera-password-test", stored.Metadata!);
            Assert.DoesNotContain(server.Url, stored.Metadata!);
            Assert.DoesNotContain("camera-test", stored.Metadata!);
            Assert.Null(created.Metadata);
            Assert.True(CameraMetadata.TryGetProtectedConfiguration(stored.Metadata, out _));
            var list = await new GetEquipmentsHandler(operations, new GetEquipmentsValidator(), TimeProvider.System, cameras)
                .Handle(new() { SearchTerm = request.Name }, CancellationToken.None);
            Assert.Null(Assert.Single(list.Items).Metadata);
            var countBeforeDuplicate = server.AcceptedConnections;
            var duplicate = await Assert.ThrowsAsync<CameraConnectionException>(() => handler.Handle(request, CancellationToken.None));
            Assert.Equal("Duplicate", duplicate.Code);
            Assert.Equal(countBeforeDuplicate, server.AcceptedConnections);

            server.DisconnectClients();
            await WaitForAsync(() => cameras.GetStatus(created.Id).State != "Connected");
            Assert.Null(cameras.GetFrame(created.Id));
            Assert.Equal("Active", (await db.Equipments.AsNoTracking().SingleAsync(x => x.Id == created.Id)).Status);
        }
        finally
        {
            if (createdId != null) await db.Equipments.Where(x => x.Id == createdId).ExecuteDeleteAsync();
            await db.Equipments.Where(x => x.Id == source.Id).ExecuteDeleteAsync();
        }
    }

    [Theory]
    [InlineData(false, false, false)]
    [InlineData(true, false, false)]
    [InlineData(true, true, false)]
    [InlineData(true, true, true)]
    public async Task H265_ReassemblesSingleNalFragmentedAndAggregatedRtp(bool fragmented, bool aggregated, bool omitSdp)
    {
        await using var server = await LocalRtspCamera.StartAsync(Ffmpeg, hevc: true);
        server.FragmentVideo = fragmented;
        server.AggregateParameters = aggregated;
        server.OmitSdpParameters = omitSdp;
        var options = Options.Create(new CameraOptions { AllowedEndpoints = [new() { Host = "127.0.0.1", Port = server.Port }], FfmpegPath = Ffmpeg, ConnectionTimeoutSeconds = 8 });
        await using var cameras = new CameraService(new CameraAddressPolicy(options), new CameraFrameDecoder(options), new EphemeralDataProtectionProvider(), options, TimeProvider.System, NullLogger<CameraService>.Instance);
        await using var registration = await cameras.PrepareAsync(new() { StreamUrl = server.Url }, CancellationToken.None);
        var id = Guid.NewGuid();
        registration.Publish(id);
        registration.Confirm();
        Assert.Equal("Connected", cameras.GetStatus(id).State);
        var jpeg = cameras.GetFrame(id);
        Assert.NotNull(jpeg);
        Assert.Equal(new byte[] { 0xff, 0xd8 }, jpeg[..2]);
        Assert.Equal(new byte[] { 0xff, 0xd9 }, jpeg[^2..]);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task RealRtsp_StoredConfigurationRestoresOneSharedConnection(bool hevc)
    {
        await using var server = await LocalRtspCamera.StartAsync(Ffmpeg, hevc);
        var options = Options.Create(new CameraOptions { AllowedEndpoints = [new() { Host = "127.0.0.1", Port = server.Port }], FfmpegPath = Ffmpeg, ConnectionTimeoutSeconds = 5 });
        var protection = new EphemeralDataProtectionProvider();
        string encrypted;
        var id = Guid.NewGuid();
        await using (var original = new CameraService(new CameraAddressPolicy(options), new CameraFrameDecoder(options), protection, options, TimeProvider.System, NullLogger<CameraService>.Instance))
        {
            await using var registration = await original.PrepareAsync(new() { StreamUrl = server.Url }, CancellationToken.None);
            encrypted = registration.ProtectedConfiguration;
            registration.Publish(id);
            registration.Confirm();
        }
        var count = server.AcceptedConnections;
        await using var restored = new CameraService(new CameraAddressPolicy(options), new CameraFrameDecoder(options), protection, options, TimeProvider.System, NullLogger<CameraService>.Instance);
        await restored.RestoreAsync(id, encrypted, CancellationToken.None);
        await restored.RestoreAsync(id, encrypted, CancellationToken.None);
        await WaitForAsync(() => restored.GetStatus(id).State == "Connected");
        Assert.Equal(count + 1, server.AcceptedConnections);
        Assert.NotNull(restored.GetFrame(id));
    }

    [Fact]
    public async Task RealRtsp_DatabaseFailureRollsBackAndReleasesRegistration()
    {
        await using var server = await LocalRtspCamera.StartAsync(Ffmpeg);
        var options = Options.Create(new CameraOptions { AllowedEndpoints = [new() { Host = "127.0.0.1", Port = server.Port }], FfmpegPath = Ffmpeg, ConnectionTimeoutSeconds = 5 });
        await using var cameras = new CameraService(new CameraAddressPolicy(options), new CameraFrameDecoder(options), new EphemeralDataProtectionProvider(), options, TimeProvider.System, NullLogger<CameraService>.Instance);
        await using var db = new SmartPoolDbContext(await IntegrationTestDatabase.CreateOptionsAsync());
        var id = Guid.NewGuid();
        await using (var registration = await cameras.PrepareAsync(new() { StreamUrl = server.Url }, CancellationToken.None))
        {
            var invalid = new Equipment
            {
                Id = id, Name = new string('x', 256), Category = "Camera", IsDeleted = false, Status = "Active",
                Metadata = CameraMetadata.Pack(registration, null)
            };
            await Assert.ThrowsAsync<DbUpdateException>(() => new EquipmentOperations(db).RegisterCameraAsync(invalid, registration.Identity, () => registration.Publish(id), CancellationToken.None));
        }
        Assert.False(await db.Equipments.AsNoTracking().AnyAsync(x => x.Id == id));
        Assert.Equal("Disconnected", cameras.GetStatus(id).State);
        Assert.Null(cameras.GetFrame(id));
        // A failed attempt must release the endpoint reservation, allowing a real new connection.
        await using var retry = await cameras.PrepareAsync(new() { StreamUrl = server.Url }, CancellationToken.None);
        Assert.NotNull(retry);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task RealRtsp_BadCredentialsAndInvalidVideoDoNotProduceSuccessfulRegistration(bool hevc)
    {
        await using var server = await LocalRtspCamera.StartAsync(Ffmpeg, hevc);
        server.RequireAuthentication = true;
        var options = Options.Create(new CameraOptions { AllowedEndpoints = [new() { Host = "127.0.0.1", Port = server.Port }], FfmpegPath = Ffmpeg, ConnectionTimeoutSeconds = 4 });
        await using var cameras = new CameraService(new CameraAddressPolicy(options), new CameraFrameDecoder(options), new EphemeralDataProtectionProvider(), options, TimeProvider.System, NullLogger<CameraService>.Instance);
        var authentication = await Assert.ThrowsAsync<CameraConnectionException>(() => cameras.PrepareAsync(new() { StreamUrl = server.Url, Username = "camera-test", Password = "wrong" }, CancellationToken.None));
        Assert.Equal("AuthenticationFailed", authentication.Code);
        server.RequireAuthentication = false;
        server.InvalidVideo = true;
        var invalid = await Assert.ThrowsAsync<CameraConnectionException>(() => cameras.PrepareAsync(new() { StreamUrl = server.Url }, CancellationToken.None));
        Assert.NotEqual("Duplicate", invalid.Code);
    }

    [Fact]
    public async Task RealRtsp_RedirectCannotConnectToUnapprovedEndpoint()
    {
        await using var server = await LocalRtspCamera.StartAsync(Ffmpeg);
        await using var destination = await LocalRtspCamera.StartAsync(Ffmpeg);
        server.RedirectUrl = destination.Url;
        var options = Options.Create(new CameraOptions { AllowedEndpoints = [new() { Host = "127.0.0.1", Port = server.Port }], FfmpegPath = Ffmpeg, ConnectionTimeoutSeconds = 4 });
        await using var cameras = new CameraService(new CameraAddressPolicy(options), new CameraFrameDecoder(options), new EphemeralDataProtectionProvider(), options, TimeProvider.System, NullLogger<CameraService>.Instance);
        await Assert.ThrowsAsync<CameraConnectionException>(() => cameras.PrepareAsync(new() { StreamUrl = server.Url }, CancellationToken.None));
        Assert.Equal(0, destination.AcceptedConnections);
    }

    private static async Task WaitForAsync(Func<bool> predicate)
    {
        var deadline = DateTime.UtcNow.AddSeconds(5);
        while (!predicate() && DateTime.UtcNow < deadline) await Task.Delay(50);
        Assert.True(predicate());
    }
}
