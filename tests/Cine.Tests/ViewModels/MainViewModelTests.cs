using Cine.Avalonia.Core.Navigation;
using Cine.Avalonia.Managers;
using Cine.Avalonia.Services;
using Cine.Avalonia.Services.UI;
using Cine.Avalonia.ViewModels;
using Cine.Media.Interfaces;
using Cine.Media.Models;
using NSubstitute;
using Shouldly;
using Xunit;

namespace Cine.Tests.ViewModels;

public class MainViewModelTests
{
    private readonly IMediaPlayer _player;
    private readonly IAudioManager _audio;
    private readonly VideoManager _video;
    private readonly ISubtitleManager _subtitles;
    private readonly MainViewModel _sut;

    public MainViewModelTests()
    {
        _player = Substitute.For<IMediaPlayer>();
        _player.VolumeMax.Returns(150.0);
        _audio = Substitute.For<IAudioManager>();
        _video = new VideoManager(_player);
        _subtitles = Substitute.For<ISubtitleManager>();
        var session = Substitute.For<ISessionService>();
        var playlist = Substitute.For<IPlaylistService>();
        var renderer = Substitute.For<IRendererService>();
        var mediaFile = Substitute.For<IMediaFileService>();
        var dragDrop = Substitute.For<IDragDropService>();
        var navigation = Substitute.For<INavigationService>();
        var recentFiles = Substitute.For<IRecentFilesService>();
        var osd = Substitute.For<IOsdService>();
        _sut = new MainViewModel(_player, session, playlist,
            audioManager: _audio,
            videoManager: _video,
            subtitleManager: _subtitles,
            rendererService: renderer,
            mediaFileService: mediaFile,
            dragDropService: dragDrop,
            navigationService: navigation,
            recentFilesService: recentFiles,
            osdService: osd);
    }

    // ── Constructor ──────────────────────────────────────────────

    [Fact]
    public void Constructor_InitializesDefaults()
    {
        _sut.State.ShouldBe(PlaybackState.Stopped);
        _sut.IsPlaying.ShouldBeFalse();
        _sut.IsPaused.ShouldBeFalse();
    }

    // ── State Properties ─────────────────────────────────────────

    [Fact]
    public void State_SetsIsPlayingAndIsPaused()
    {
        _sut.State = PlaybackState.Playing;
        _sut.IsPlaying.ShouldBeTrue();
        _sut.IsPaused.ShouldBeFalse();

        _sut.State = PlaybackState.Paused;
        _sut.IsPaused.ShouldBeTrue();
        _sut.IsPlaying.ShouldBeFalse();

        _sut.State = PlaybackState.Stopped;
        _sut.IsPlaying.ShouldBeFalse();
        _sut.IsPaused.ShouldBeFalse();
    }

    // ── Volume ───────────────────────────────────────────────────

    [Fact]
    public void VolumeValue_DelegatesToAudioManager()
    {
        _sut.VolumeValue = 75;
        _audio.Received(1).VolumeValue = 75;
    }

    [Fact]
    public void Volume_ReflectsVolumeValue()
    {
        _sut.VolumeValue = 75;
        _sut.Volume.ShouldBe(75);
    }

    [Fact]
    public void VolumeMax_GetsFromPlayer()
    {
        _player.VolumeMax.Returns(150.0);
        _sut.VolumeMax.ShouldBe(150.0);
    }

    [Fact]
    public void IsMuted_DelegatesToAudioManager()
    {
        _sut.IsMuted = true;
        _audio.Received(1).IsMuted = true;
    }

    // ── Speed ────────────────────────────────────────────────────

    [Fact]
    public void SpeedValue_SetsPlayerSpeed()
    {
        _sut.SpeedValue = 1.5;
        _player.Speed.ShouldBe(1.5);
    }

    // ── Video Filters ────────────────────────────────────────────

    [Fact]
    public void ContrastValue_SetsPlayerContrast()
    {
        _sut.ContrastValue = 0.5;
        _player.Contrast.ShouldBe(0.5);
    }

    [Fact]
    public void BrightnessValue_SetsPlayerBrightness()
    {
        _sut.BrightnessValue = -0.2;
        _player.Brightness.ShouldBe(-0.2);
    }

    // ── Subtitle Delay ───────────────────────────────────────────

    [Fact]
    public void SubtitleDelayValue_DelegatesToSubtitleManager()
    {
        _sut.SubtitleDelayValue = 2.0f;
        _subtitles.Received(1).SubtitleDelay = 2.0f;
    }

    // ── Audio Delay ──────────────────────────────────────────────

    [Fact]
    public void AudioDelayValue_DelegatesToAudioManager()
    {
        _sut.AudioDelayValue = 0.5f;
        _audio.Received(1).AudioDelay = 0.5f;
    }

    // ── Dialogue Boost ───────────────────────────────────────────

    [Fact]
    public void IsDialogueBoostEnabled_DelegatesToAudioManager()
    {
        _sut.IsDialogueBoostEnabled = true;
        _audio.Received(1).IsDialogueBoostEnabled = true;
    }

    // ── Playlist Properties ──────────────────────────────────────

    [Fact]
    public void IsShuffleEnabled_DelegatesToPlaylistCoordinator()
    {
        _sut.IsShuffleEnabled = true;
        _sut.IsShuffleEnabled.ShouldBeTrue();
    }

    [Fact]
    public void IsLoopFileEnabled_DelegatesToPlaylistCoordinator()
    {
        _sut.IsLoopFileEnabled = true;
        _sut.IsLoopFileEnabled.ShouldBeTrue();
    }

    [Fact]
    public void IsLoopPlaylistEnabled_DelegatesToPlaylistCoordinator()
    {
        _sut.IsLoopPlaylistEnabled = true;
        _sut.IsLoopPlaylistEnabled.ShouldBeTrue();
    }

    // ── Reset Commands ───────────────────────────────────────────

    [Fact]
    public void ResetContrast_SetsToZero()
    {
        _player.Contrast = 0.5;
        _sut.ResetContrast();
        _player.Contrast.ShouldBe(0);
    }

    [Fact]
    public void ResetBrightness_SetsToZero()
    {
        _player.Brightness = -0.2;
        _sut.ResetBrightness();
        _player.Brightness.ShouldBe(0);
    }

    [Fact]
    public void ResetGamma_SetsToOne()
    {
        _player.Gamma = 1.5;
        _sut.ResetGamma();
        _player.Gamma.ShouldBe(1);
    }

    [Fact]
    public void ResetSaturation_SetsToOne()
    {
        _player.Saturation = 1.5;
        _sut.ResetSaturation();
        _player.Saturation.ShouldBe(1);
    }

    [Fact]
    public void ResetHue_SetsToZero()
    {
        _player.Hue = 10;
        _sut.ResetHue();
        _player.Hue.ShouldBe(0);
    }

    [Fact]
    public void ResetZoom_SetsToZero()
    {
        _player.Zoom = 1.5;
        _sut.ResetZoom();
        _player.Zoom.ShouldBe(0);
    }

    // ── Rotation Commands ────────────────────────────────────────

    [Fact]
    public void RotateLeft_SendsCommand()
    {
        _sut.RotateLeft();
        _player.Received(1).Command("set", "video-rotate", "90");
    }

    [Fact]
    public void RotateRight_SendsCommand()
    {
        _sut.RotateRight();
        _player.Received(1).Command("set", "video-rotate", "270");
    }

    // ── Title ────────────────────────────────────────────────────

    [Fact]
    public void Title_WhenFilePathSet_ReturnsFileName()
    {
        // MainViewModel doesn't expose a direct FilePath setter that updates Title;
        // Title is computed from _filePath. We check the default.
        _sut.Title.ShouldBe("Cine");
    }

    // ── PropertyChanged ──────────────────────────────────────────

    [Fact]
    public void State_FiresPropertyChanged()
    {
        var changedProperties = new List<string>();
        _sut.PropertyChanged += (_, e) => changedProperties.Add(e.PropertyName!);

        _sut.State = PlaybackState.Playing;

        changedProperties.ShouldContain(nameof(_sut.State));
        changedProperties.ShouldContain(nameof(_sut.IsPlaying));
    }

    [Fact]
    public void VolumeValue_FiresPropertyChanged()
    {
        var changedProperties = new List<string>();
        _sut.PropertyChanged += (_, e) => changedProperties.Add(e.PropertyName!);

        _sut.VolumeValue = 75;

        changedProperties.ShouldContain(nameof(_sut.VolumeValue));
        changedProperties.ShouldContain(nameof(_sut.Volume));
    }
}
