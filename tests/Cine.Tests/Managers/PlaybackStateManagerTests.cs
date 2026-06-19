using Cine.Avalonia.Managers;
using Cine.Media.Events;
using Cine.Media.Interfaces;
using Cine.Media.Models;
using NSubstitute;
using Shouldly;
using Xunit;

namespace Cine.Tests.Managers;

public class PlaybackStateManagerTests
{
    private readonly IMediaPlayer _player;
    private readonly PlaybackStateManager _sut;

    public PlaybackStateManagerTests()
    {
        _player = Substitute.For<IMediaPlayer>();
        _sut = new PlaybackStateManager(_player);
    }

    // ── Constructor ──────────────────────────────────────────────

    [Fact]
    public void Constructor_SubscribesToPlayerEvents()
    {
        // Verify subscription by raising an event and checking the SUT reacts
        _player.PositionChanged += Raise.Event<EventHandler<PositionChangedEventArgs>>(
            new PositionChangedEventArgs(TimeSpan.FromSeconds(30), TimeSpan.FromSeconds(100)));
        _sut.Position.TotalSeconds.ShouldBe(30);
    }

    [Fact]
    public void Constructor_InitializesDefaults()
    {
        _sut.IsMediaLoaded.ShouldBeFalse();
        _sut.IsPlaying.ShouldBeFalse();
        _sut.IsPaused.ShouldBeFalse();
        _sut.Position.ShouldBe(TimeSpan.Zero);
        _sut.NormalizedPosition.ShouldBe(0);
        _sut.Duration.ShouldBe(TimeSpan.Zero);
        // Volume is 0 initially because Refresh() reads from player (default 0)
        _sut.Volume.ShouldBe(0);
        _sut.IsMuted.ShouldBeFalse();
        _sut.Speed.ShouldBe(0);
        _sut.IsReplayMode.ShouldBeFalse();
    }

    // ── Media Opened (Opened event, no args) ────────────────────

    [Fact]
    public void OnPlayerOpened_SetsMediaLoaded()
    {
        _player.Opened += Raise.Event<EventHandler>(EventArgs.Empty);

        _sut.IsMediaLoaded.ShouldBeTrue();
    }

    [Fact]
    public void OnPlayerOpened_ClearsReplayMode()
    {
        // Arrange: load media, then simulate end-of-file to set replay mode
        _player.Opened += Raise.Event<EventHandler>(EventArgs.Empty);
        _player.PlaybackStateChangedEvent += Raise.Event<EventHandler<PlaybackStateChangedEventArgs>>(
            new PlaybackStateChangedEventArgs(PlaybackState.Stopped));
        _sut.IsReplayMode.ShouldBeTrue();

        // Act: open new file
        _player.Opened += Raise.Event<EventHandler>(EventArgs.Empty);

        // Assert
        _sut.IsReplayMode.ShouldBeFalse();
    }

    // ── Playback State ───────────────────────────────────────────

    [Fact]
    public void OnPlaybackStateChanged_Playing_SetsState()
    {
        _player.PlaybackStateChangedEvent += Raise.Event<EventHandler<PlaybackStateChangedEventArgs>>(
            new PlaybackStateChangedEventArgs(isPaused: false));

        _sut.IsPlaying.ShouldBeTrue();
        _sut.IsPaused.ShouldBeFalse();
    }

    [Fact]
    public void OnPlaybackStateChanged_Paused_SetsState()
    {
        _player.PlaybackStateChangedEvent += Raise.Event<EventHandler<PlaybackStateChangedEventArgs>>(
            new PlaybackStateChangedEventArgs(isPaused: true));

        _sut.IsPaused.ShouldBeTrue();
        _sut.IsPlaying.ShouldBeFalse();
    }

    [Fact]
    public void OnPlaybackStateChanged_Stopped_SetsState()
    {
        // Raise Playing first (media not loaded, so no replay mode)
        _player.PlaybackStateChangedEvent += Raise.Event<EventHandler<PlaybackStateChangedEventArgs>>(
            new PlaybackStateChangedEventArgs(isPaused: false));
        _sut.IsPlaying.ShouldBeTrue();

        // Then raise Stopped
        _player.PlaybackStateChangedEvent += Raise.Event<EventHandler<PlaybackStateChangedEventArgs>>(
            new PlaybackStateChangedEventArgs(PlaybackState.Stopped));

        _sut.IsPlaying.ShouldBeFalse();
    }

    [Fact]
    public void OnPlaybackStateChanged_StoppedWhenLoaded_SetsReplayMode()
    {
        // First simulate media opened
        _player.Opened += Raise.Event<EventHandler>(EventArgs.Empty);

        // Raise Stopped — manager treats this as end-of-file when media is loaded
        _player.PlaybackStateChangedEvent += Raise.Event<EventHandler<PlaybackStateChangedEventArgs>>(
            new PlaybackStateChangedEventArgs(PlaybackState.Stopped));

        _sut.IsReplayMode.ShouldBeTrue();
    }

    // ── Position ─────────────────────────────────────────────────

    [Fact]
    public void UpdatePosition_UpdatesProperties()
    {
        _player.PositionChanged += Raise.Event<EventHandler<PositionChangedEventArgs>>(
            new PositionChangedEventArgs(TimeSpan.FromSeconds(30), TimeSpan.FromSeconds(100)));

        _sut.Position.TotalSeconds.ShouldBe(30);
        _sut.Duration.TotalSeconds.ShouldBe(100);
        _sut.NormalizedPosition.ShouldBe(0.3);
    }

    [Fact]
    public void UpdatePosition_AtZeroDuration_ReturnsZero()
    {
        _player.PositionChanged += Raise.Event<EventHandler<PositionChangedEventArgs>>(
            new PositionChangedEventArgs(TimeSpan.FromSeconds(30), TimeSpan.Zero));

        _sut.NormalizedPosition.ShouldBe(0);
    }

    [Fact]
    public void UpdatePosition_FiresPropertyChangedForPosition()
    {
        var changedProperties = new List<string>();
        _sut.PropertyChanged += (_, e) => changedProperties.Add(e.PropertyName!);

        _player.PositionChanged += Raise.Event<EventHandler<PositionChangedEventArgs>>(
            new PositionChangedEventArgs(TimeSpan.FromSeconds(30), TimeSpan.FromSeconds(100)));

        changedProperties.ShouldContain(nameof(_sut.Position));
        changedProperties.ShouldContain(nameof(_sut.NormalizedPosition));
    }

    // ── Volume ───────────────────────────────────────────────────

    [Fact]
    public void UpdateVolume_SetsVolume()
    {
        _player.VolumeChanged += Raise.Event<EventHandler<VolumeChangedEventArgs>>(
            new VolumeChangedEventArgs(75.0));

        _sut.Volume.ShouldBe(75.0);
    }

    [Fact]
    public void UpdateVolume_MutesAndUnmutes()
    {
        _player.VolumeChanged += Raise.Event<EventHandler<VolumeChangedEventArgs>>(
            new VolumeChangedEventArgs(isMuted: true));

        _sut.IsMuted.ShouldBeTrue();

        _player.VolumeChanged += Raise.Event<EventHandler<VolumeChangedEventArgs>>(
            new VolumeChangedEventArgs(50.0));

        _sut.IsMuted.ShouldBeFalse();
        _sut.Volume.ShouldBe(50.0);
    }

    [Fact]
    public void UpdateVolume_FiresPropertyChanged()
    {
        var changedProperties = new List<string>();
        _sut.PropertyChanged += (_, e) => changedProperties.Add(e.PropertyName!);

        _player.VolumeChanged += Raise.Event<EventHandler<VolumeChangedEventArgs>>(
            new VolumeChangedEventArgs(50.0));

        changedProperties.ShouldContain(nameof(_sut.Volume));
    }

    // ── Speed (via Refresh reads _player.Speed) ──────────────────

    [Fact]
    public void Refresh_ReadsSpeedFromPlayer()
    {
        _player.Speed.Returns(1.5);

        _sut.Refresh();

        _sut.Speed.ShouldBe(1.5);
    }

    // ── TrackList ────────────────────────────────────────────────

    [Fact]
    public void OnTrackListChanged_RaisesTrackListChangedEvent()
    {
        var receivedArgs = default(TrackListChangedEventArgs);
        _sut.TrackListChanged += (_, e) => receivedArgs = e;

        _player.TrackListChanged += Raise.Event<EventHandler<TrackListChangedEventArgs>>(
            new TrackListChangedEventArgs(
                Array.Empty<SubtitleSource>(),
                Array.Empty<SubtitleSource>(),
                Array.Empty<SubtitleSource>()));

        receivedArgs.ShouldNotBeNull();
    }

    // ── Dispose ──────────────────────────────────────────────────

    [Fact]
    public void Dispose_UnsubscribesFromPlayerEvents()
    {
        _sut.Dispose();

        // After dispose, player events should no longer affect the SUT
        _player.Opened += Raise.Event<EventHandler>(EventArgs.Empty);
        _sut.IsMediaLoaded.ShouldBeFalse();
    }

    [Fact]
    public void Dispose_MultipleCalls_DoesNotThrow()
    {
        _sut.Dispose();
        Should.NotThrow(() => _sut.Dispose());
    }
}
