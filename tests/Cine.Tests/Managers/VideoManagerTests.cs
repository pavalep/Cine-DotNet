using Cine.Avalonia.Managers;
using Cine.Media.Interfaces;
using Cine.Media.Models;
using NSubstitute;
using Shouldly;
using Xunit;

namespace Cine.Tests.Managers;

public class VideoManagerTests
{
    private readonly IMediaPlayer _player;
    private readonly VideoManager _sut;

    public VideoManagerTests()
    {
        _player = Substitute.For<IMediaPlayer>();
        _sut = new VideoManager(_player);
    }

    // ── Constructor ──────────────────────────────────────────────

    [Fact]
    public void Constructor_BuildsTrackMenus()
    {
        _sut.VideoTracks.ShouldNotBeNull();
        _sut.VideoTracks.Count.ShouldBeGreaterThanOrEqualTo(3);
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

    [Fact]
    public void GammaValue_SetsPlayerGamma()
    {
        _sut.GammaValue = 1.5;
        _player.Gamma.ShouldBe(1.5);
    }

    [Fact]
    public void SaturationValue_SetsPlayerSaturation()
    {
        _sut.SaturationValue = 1.2;
        _player.Saturation.ShouldBe(1.2);
    }

    [Fact]
    public void HueValue_SetsPlayerHue()
    {
        _sut.HueValue = 10.0;
        _player.Hue.ShouldBe(10.0);
    }

    [Fact]
    public void ZoomValue_SetsPlayerZoom()
    {
        _sut.ZoomValue = 1.5;
        _player.Zoom.ShouldBe(1.5);
    }

    [Fact]
    public void AspectRatioValue_SetsPlayerAspectRatio()
    {
        _sut.AspectRatioValue = 16.0 / 9.0;
        _player.AspectRatio.ShouldBe(16.0 / 9.0);
    }

    // ── Zoom & Aspect Resets ─────────────────────────────────────

    [Fact]
    public void ResetZoom_SetsToZero()
    {
        _sut.ZoomValue = 1.5;
        _sut.ResetZoom();
        _player.Zoom.ShouldBe(0);
    }

    [Fact]
    public void ResetAspectRatio_SetsToNegativeOne()
    {
        _sut.AspectRatioValue = 16.0 / 9.0;
        _sut.ResetAspectRatio();
        _player.AspectRatio.ShouldBe(-1);
    }

    // ── Rotation & Flip ──────────────────────────────────────────

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

    [Fact]
    public void ResetRotation_SendsCommand()
    {
        _sut.ResetRotation();
        _player.Received(1).Command("set", "video-rotate", "0");
    }

    [Fact]
    public void FlipHorizontal_SendsCommand()
    {
        _sut.FlipHorizontal();
        _player.Received(1).Command("vf", "toggle", "hflip");
    }

    [Fact]
    public void FlipVertical_SendsCommand()
    {
        _sut.FlipVertical();
        _player.Received(1).Command("vf", "toggle", "vflip");
    }

    // ── Filter Properties Fire PropertyChanged ───────────────────

    [Fact]
    public void ContrastValue_FiresPropertyChanged()
    {
        var changedProperties = new List<string>();
        _sut.PropertyChanged += (_, e) => changedProperties.Add(e.PropertyName!);

        _sut.ContrastValue = 0.5;

        changedProperties.ShouldContain(nameof(_sut.ContrastValue));
    }

    [Fact]
    public void ZoomValue_FiresPropertyChanged()
    {
        var changedProperties = new List<string>();
        _sut.PropertyChanged += (_, e) => changedProperties.Add(e.PropertyName!);

        _sut.ZoomValue = 1.5;

        changedProperties.ShouldContain(nameof(_sut.ZoomValue));
    }

    // ── RefreshVideoTracks ───────────────────────────────────────

    [Fact]
    public void RefreshVideoTracks_PopulatesTracks()
    {
        var sources = new[]
        {
            new SubtitleSource { PathOrId = "1", Language = "eng", IsEnabled = true },
        };

        _sut.RefreshVideoTracks(sources);

        _sut.VideoTracks.Any(t => t.DisplayName.Contains("English")).ShouldBeTrue();
    }

    [Fact]
    public void RefreshVideoTracks_EmptySources_ShowsNoTracks()
    {
        _sut.RefreshVideoTracks(Array.Empty<SubtitleSource>());
        _sut.VideoTracks.Any(t => t.DisplayName.Contains("No video")).ShouldBeTrue();
    }

    [Fact]
    public void RefreshVideoTracks_NullSources_ShowsNoTracks()
    {
        _sut.RefreshVideoTracks(null!);
        _sut.VideoTracks.Any(t => t.DisplayName.Contains("No video")).ShouldBeTrue();
    }

    // ── HasMultipleVideoTracks ───────────────────────────────────

    [Fact]
    public void HasMultipleVideoTracks_SingleTrack_ReturnsFalse()
    {
        var sources = new[]
        {
            new SubtitleSource { PathOrId = "1", Language = "eng", IsEnabled = true },
        };

        _sut.RefreshVideoTracks(sources);
        _sut.HasMultipleVideoTracks.ShouldBeFalse();
    }

    [Fact]
    public void HasMultipleVideoTracks_MultipleTracks_ReturnsTrue()
    {
        var sources = new[]
        {
            new SubtitleSource { PathOrId = "1", Language = "eng", IsEnabled = true },
            new SubtitleSource { PathOrId = "2", Language = "jpn", IsEnabled = false },
        };

        _sut.RefreshVideoTracks(sources);
        _sut.HasMultipleVideoTracks.ShouldBeTrue();
    }

    // ── Reset All ────────────────────────────────────────────────

    [Fact]
    public void ResetAllVideo_ResetsFilters()
    {
        _sut.ContrastValue = 0.5;
        _sut.BrightnessValue = -0.2;
        _sut.ZoomValue = 1.5;

        _sut.ResetAllVideo();

        _player.Contrast.ShouldBe(0);
        _player.Brightness.ShouldBe(0);
        _player.Zoom.ShouldBe(0);
        _player.Received(1).Command("set", "video-rotate", "0");
    }

    // ── Dispose ──────────────────────────────────────────────────

    [Fact]
    public void Dispose_MultipleCalls_DoesNotThrow()
    {
        _sut.Dispose();
        Should.NotThrow(() => _sut.Dispose());
    }
}
