using Cine.Avalonia.Managers;
using Cine.Avalonia.Storage;
using Cine.Avalonia.Core;
using Cine.Media.Events;
using Cine.Media.Interfaces;
using Cine.Media.Models;
using NSubstitute;
using Shouldly;
using Xunit;

namespace Cine.Tests.Managers;

public class SubtitleManagerTests
{
    private readonly IMediaPlayer _player;
    private readonly SubtitleManager _sut;

    public SubtitleManagerTests()
    {
        _player = Substitute.For<IMediaPlayer>();
        var store = new SubtitleSettingsStore();
        var eventBus = Substitute.For<IEventBus>();
        _sut = new SubtitleManager(_player, store, eventBus);
    }

    // ── Constructor ──────────────────────────────────────────────

    [Fact]
    public void Constructor_InitializesDefaults()
    {
        _sut.SubtitleTracks.ShouldNotBeNull();
        _sut.SubtitleTracks.Count.ShouldBeGreaterThanOrEqualTo(2);
        _sut.IsSubtitleEnabled.ShouldBeFalse();
        _sut.IsSessionOverrideActive.ShouldBeFalse();
    }

    // ── IsSubtitleEnabled ─────────────────────────────────────────

    [Fact]
    public void IsSubtitleEnabled_SetsPlayerVisibility()
    {
        _sut.IsSubtitleEnabled = true;
        _player.Received(1).SetSubtitleVisibility(true);

        _sut.IsSubtitleEnabled = false;
        _player.Received(1).SetSubtitleVisibility(false);
    }

    [Fact]
    public void IsSubtitleEnabled_FiresPropertyChanged()
    {
        var changedProperties = new List<string>();
        _sut.PropertyChanged += (_, e) => changedProperties.Add(e.PropertyName!);

        _sut.IsSubtitleEnabled = true;

        changedProperties.ShouldContain(nameof(_sut.IsSubtitleEnabled));
    }

    // ── Subtitle Timing ───────────────────────────────────────────

    [Fact]
    public void SubtitleDelay_SetsPlayerDelay()
    {
        _sut.SubtitleDelay = 2.0f;
        _player.SubtitleDelay.ShouldBe(2.0f);
        _sut.SubtitleDelay.ShouldBe(2.0f);
    }

    [Fact]
    public void SubtitleDelay_FiresPropertyChanged()
    {
        var changedProperties = new List<string>();
        _sut.PropertyChanged += (_, e) => changedProperties.Add(e.PropertyName!);

        _sut.SubtitleDelay = 2.0f;

        changedProperties.ShouldContain(nameof(_sut.SubtitleDelay));
    }

    // ── Subtitle Position ────────────────────────────────────────

    [Fact]
    public void SubtitlePosition_SetsPlayerPosition()
    {
        _sut.SubtitlePosition = 80;
        _player.Received(1).SetSubtitlePosition(80);
        _sut.SubtitlePosition.ShouldBe(80);
    }

    [Fact]
    public void SubtitlePosition_ClampsToRange()
    {
        _sut.SubtitlePosition = 300;
        _player.Received(1).SetSubtitlePosition(200);

        _sut.SubtitlePosition = -10;
        _player.Received(1).SetSubtitlePosition(0);
    }

    // ── Subtitle Styling ─────────────────────────────────────────

    [Fact]
    public void SubtitleFontScale_SetsPlayerFontSize()
    {
        _sut.SubtitleFontScale = 1.5;
        _player.Received(1).SetSubtitleFontSize(1.5 * 24);
        _sut.SubtitleFontScale.ShouldBe(1.5);
    }

    [Fact]
    public void SubtitleFont_SetsPlayerFont()
    {
        _sut.SubtitleFont = "Roboto";
        _player.Received(1).SetSubtitleFont("Roboto");
    }

    [Fact]
    public void SubtitleBorderSize_SetsPlayerBorderSize()
    {
        _sut.SubtitleBorderSize = 3.0;
        _player.Received(1).SetSubtitleBorderSize(3.0);
    }

    [Fact]
    public void SubtitleBorderSize_ClampsToRange()
    {
        _sut.SubtitleBorderSize = 15;
        _player.Received(1).SetSubtitleBorderSize(10);

        _sut.SubtitleBorderSize = -1;
        _player.Received(1).SetSubtitleBorderSize(0);
    }

    [Fact]
    public void SubtitleShadowOffset_SetsPlayerShadowOffset()
    {
        _sut.SubtitleShadowOffset = 2.5;
        _player.Received(1).SetSubtitleShadowOffset(2.5);
    }

    [Fact]
    public void SubtitleColor_SetsPlayerColor()
    {
        _sut.SubtitleColor = "#FFFF00";
        _player.Received(1).SetSubtitleColor("#FFFF00");
    }

    // ── Player Event: Opened ─────────────────────────────────────

    [Fact]
    public void OnPlayerOpened_ClearsSessionOverride()
    {
        // LoadExternalSubtitle sets session override = true
        _sut.LoadExternalSubtitle(@"C:\test\sub.srt");
        _sut.IsSessionOverrideActive.ShouldBeTrue();

        // Then raise Opened — should clear session override
        _player.Opened += Raise.Event<EventHandler>(EventArgs.Empty);

        _sut.IsSessionOverrideActive.ShouldBeFalse();
    }

    // ── Player Event: TrackListChanged ───────────────────────────

    [Fact]
    public void OnTrackListChanged_RebuildsSubtitleTracks()
    {
        var sources = new[]
        {
            new SubtitleSource { PathOrId = "1", Language = "eng", IsEnabled = true },
            new SubtitleSource { PathOrId = "2", Language = "jpn", IsEnabled = false },
        };

        _player.TrackListChanged += Raise.Event<EventHandler<TrackListChangedEventArgs>>(
            new TrackListChangedEventArgs(
                Array.Empty<SubtitleSource>(),
                Array.Empty<SubtitleSource>(),
                sources));

        _sut.SubtitleTracks.Any(t => t.DisplayName.Contains("English")).ShouldBeTrue();
        _sut.SubtitleTracks.Any(t => t.DisplayName.Contains("Japanese")).ShouldBeTrue();
    }

    // ── Player Event: SubtitlePropertyChanged ────────────────────

    [Fact]
    public void OnSubtitlePropertyChanged_Sid_UpdatesTrackId()
    {
        _player.SubtitlePropertyChanged += Raise.Event<EventHandler<SubtitlePropertyChangedEventArgs>>(
            new SubtitlePropertyChangedEventArgs("sid", 1));

        _sut.CurrentSubtitleTrackId.ShouldBe(1);
        _sut.IsSubtitleEnabled.ShouldBeTrue();
    }

    [Fact]
    public void OnSubtitlePropertyChanged_SidNegative_DisablesSubtitles()
    {
        _player.SubtitlePropertyChanged += Raise.Event<EventHandler<SubtitlePropertyChangedEventArgs>>(
            new SubtitlePropertyChangedEventArgs("sid", -1));

        _sut.CurrentSubtitleTrackId.ShouldBe(-1);
        _sut.IsSubtitleEnabled.ShouldBeFalse();
    }

    [Fact]
    public void OnSubtitlePropertyChanged_SubVisibility_UpdatesEnabled()
    {
        _player.SubtitlePropertyChanged += Raise.Event<EventHandler<SubtitlePropertyChangedEventArgs>>(
            new SubtitlePropertyChangedEventArgs("sub-visibility", true));

        _sut.IsSubtitleEnabled.ShouldBeTrue();
    }

    // ── SelectTrackById ──────────────────────────────────────────

    [Fact]
    public void SelectTrackById_SetsCurrentTrackId()
    {
        _sut.SelectSubtitleTrackById(1);
        _sut.CurrentSubtitleTrackId.ShouldBe(1);
    }

    // ── Cycle Tracks ─────────────────────────────────────────────

    [Fact]
    public void CycleSubtitleTrackForward_WithNoTracks_DoesNotThrow()
    {
        Should.NotThrow(() => _sut.CycleSubtitleTrackForward());
    }

    [Fact]
    public void CycleSubtitleTrackBackward_WithNoTracks_DoesNotThrow()
    {
        Should.NotThrow(() => _sut.CycleSubtitleTrackBackward());
    }

    // ── Reset ────────────────────────────────────────────────────

    [Fact]
    public void ResetAllSubtitles_ResetsDefaults()
    {
        _sut.SubtitleDelay = 2.0f;
        _sut.SubtitlePosition = 50;
        _sut.SubtitleFontScale = 1.5;
        _sut.SubtitleBorderSize = 5.0;
        _sut.SubtitleShadowOffset = 3.0;
        _sut.SubtitleFont = "Segoe UI";
        _sut.SubtitleColor = "#FFFF00";

        _sut.ResetAllSubtitles();

        _sut.SubtitleDelay.ShouldBe(0f);
        _sut.SubtitlePosition.ShouldBe(100);
        _sut.SubtitleFontScale.ShouldBe(1.1);
        _sut.SubtitleBorderSize.ShouldBe(2.5);
        _sut.SubtitleShadowOffset.ShouldBe(1.5);
        _sut.SubtitleFont.ShouldBe("Segoe UI");
        _sut.SubtitleColor.ShouldBe("#FFFFFF");
    }

    // ── Dispose ──────────────────────────────────────────────────

    [Fact]
    public void Dispose_UnsubscribesFromPlayerEvents()
    {
        _sut.Dispose();

        // After dispose, player events should no longer affect the SUT
        _player.SubtitlePropertyChanged += Raise.Event<EventHandler<SubtitlePropertyChangedEventArgs>>(
            new SubtitlePropertyChangedEventArgs("sid", 1));

        _sut.CurrentSubtitleTrackId.ShouldBe(-1);
    }

    [Fact]
    public void Dispose_MultipleCalls_DoesNotThrow()
    {
        _sut.Dispose();
        Should.NotThrow(() => _sut.Dispose());
    }
}
