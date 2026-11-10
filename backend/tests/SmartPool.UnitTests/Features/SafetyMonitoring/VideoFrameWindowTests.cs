using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using Moq;
using SmartPool.Application.Features.SafetyMonitoring;
using SmartPool.Application.Interfaces.Services;
using SmartPool.Infrastructure.SafetyMonitoring;

namespace SmartPool.UnitTests.Features.SafetyMonitoring;

public sealed class VideoFrameWindowTests : IDisposable
{
    private readonly string root = Path.Combine(Path.GetTempPath(), "smartpool-video-" + Guid.NewGuid().ToString("N"));
    private MonitoringFiles Files()
    {
        var environment = new Mock<IHostEnvironment>();
        environment.SetupGet(x => x.ContentRootPath).Returns(root);
        return new(Options.Create(new MonitoringOptions { StoragePath = root }), environment.Object);
    }
    private static RiskDecision Risk(string severity) => new("track-1", "episode-1", severity, .8, .1, [.1, .2, .3, .4]);

    [Fact]
    public async Task Window_ReturnsExactFramesIncludingEmptyFrames_AndNeverRepeatsLastFrameForFuturePosition()
    {
        var state = new VideoAnalysisState(Files());
        var id = Guid.NewGuid();
        state.Prepare(id, 10, 20);
        state.SetFrame(id, 0, []);
        state.SetFrame(id, 1, [Risk("WARNING")]);
        state.SetFrame(id, 2, []);
        var window = await state.WindowAsync(id, .1, .2, default);
        Assert.Equal(1, window.StartFrame);
        Assert.Equal(3, window.ReadyFrames);
        Assert.Equal("WARNING", Assert.Single(window.Frames[0]).Severity);
        Assert.Empty(window.Frames[1]);
        Assert.False(window.Complete);
        Assert.Empty((await state.WindowAsync(id, 1, 1, default)).Frames);
        Assert.Null(state.Query(id, 1).AnalyzedAt);
    }

    [Fact]
    public async Task CompletedTimeline_SurvivesMemoryRemovalAndNewService_ForReplayAndPopup()
    {
        var files = Files();
        var state = new VideoAnalysisState(files);
        var id = Guid.NewGuid();
        state.Prepare(id, 25, 3);
        state.SetFrame(id, 0, []);
        state.SetFrame(id, 1, [Risk("WARNING")]);
        state.SetFrame(id, 2, [Risk("CRITICAL")]);
        await state.CompleteAsync(id, default);
        state.Remove(id);
        var reopened = new VideoAnalysisState(files);
        var window = await reopened.WindowAsync(id, 0, 1, default);
        Assert.True(window.Complete);
        Assert.Equal(25, window.Fps);
        Assert.Equal(3, window.Frames.Count);
        Assert.Empty(window.Frames[0]);
        Assert.Equal("WARNING", Assert.Single(window.Frames[1]).Severity);
        Assert.Equal("CRITICAL", Assert.Single(window.Frames[2]).Severity);
    }

    [Fact]
    public async Task WindowHandler_ChecksJobOwnershipBeforeReadingTimeline()
    {
        var reader = new Mock<IVideoFrameAnalysisReader>();
        var service = new Mock<IVideoAnalysisService>();
        var user = new Mock<ICurrentUser>();
        user.SetupGet(x => x.UserId).Returns(Guid.NewGuid());
        service.Setup(x => x.GetAsync(It.IsAny<Guid>(), It.IsAny<Guid>(), false, It.IsAny<CancellationToken>())).ThrowsAsync(new KeyNotFoundException());
        var handler = new GetVideoFrameAnalysisWindowHandler(reader.Object, service.Object, user.Object);
        await Assert.ThrowsAsync<KeyNotFoundException>(() => handler.Handle(new(Guid.NewGuid(), 0), default));
        reader.Verify(x => x.WindowAsync(It.IsAny<Guid>(), It.IsAny<double>(), It.IsAny<double>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Theory]
    [InlineData(-1, 1)]
    [InlineData(0, 0)]
    [InlineData(0, 6)]
    [InlineData(double.NaN, 1)]
    public async Task WindowHandler_RejectsUnboundedOrInvalidRanges(double from, double duration)
    {
        var handler = new GetVideoFrameAnalysisWindowHandler(Mock.Of<IVideoFrameAnalysisReader>(), Mock.Of<IVideoAnalysisService>(), Mock.Of<ICurrentUser>());
        await Assert.ThrowsAsync<ArgumentException>(() => handler.Handle(new(Guid.NewGuid(), from, duration), default));
    }

    [Fact]
    public async Task OriginalPlayback_KeepsSourceStableAfterCompletion_AndStillRequiresTicket()
    {
        var id = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var service = new Mock<IVideoAnalysisService>();
        service.Setup(x => x.OriginalFileAsync(id, userId, false, It.IsAny<CancellationToken>())).ReturnsAsync("input.mp4");
        service.Setup(x => x.OriginalFileAsync(id, Guid.Empty, true, It.IsAny<CancellationToken>())).ReturnsAsync("input.mp4");
        var user = new Mock<ICurrentUser>(); user.SetupGet(x => x.UserId).Returns(userId);
        var tickets = new Mock<IVideoPlaybackTickets>();
        tickets.Setup(x => x.Create(id)).Returns(new VideoPlaybackTicket("valid", DateTimeOffset.UtcNow.AddMinutes(15)));
        tickets.Setup(x => x.Validate(id, "valid")).Returns(true);
        var playback = await new GetVideoPlaybackHandler(service.Object, user.Object, tickets.Object).Handle(new(id, true), default);
        Assert.Contains("original=true", playback.StreamPath);
        Assert.Equal("input.mp4", await new GetVideoStreamHandler(service.Object, tickets.Object).Handle(new(id, "valid", true), default));
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => new GetVideoStreamHandler(service.Object, tickets.Object).Handle(new(id, "wrong", true), default));
        service.Verify(x => x.LiveFileAsync(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    public void Dispose()
    {
        if (Path.GetFileName(root).StartsWith("smartpool-video-", StringComparison.Ordinal) && Directory.Exists(root)) Directory.Delete(root, true);
    }
}
