using Cine.Avalonia.Managers;
using Cine.Avalonia.Services;
using Cine.Avalonia.Storage;
using Cine.Avalonia.Core;
using Cine.Media.Events;
using Cine.Media.Interfaces;
using Cine.Media.Models;
using NSubstitute;
using Shouldly;
using Xunit;

namespace Cine.Tests.Managers;

public class AudioManagerTests
{
    private readonly IMediaPlayer _player;
    private readonly AudioManager _sut;

    public AudioManagerTests()
    {
        _player = Substitute.For<IMediaPlayer>();
        _player.VolumeMax.Returns(150.0);
        var audioStore = new AudioSettingsStore();
        var eventBus = Substitute.For<IEventBus>();
        _sut = new AudioManager(_player, audioStore, eventBus);
    }

    // ── Constructor ──────────────────────────────────────────────

    [Fact]
    public void Constructor_InitializesFromPlayer()
    {
        _sut.VolumeMax.ShouldBe(150.0);
        _sut.AudioTracks.ShouldNotBeNull();
        _sut.AudioTracks.Count.ShouldBeGreaterThanOrEqualTo(2);
    }

    // ── Volume / Mute ────────────────────────────────────────────

    [Fact]
    public void VolumeValue_SetsPlayerVolume()
    {
        _sut.VolumeValue = 75;

        _player.Volume.ShouldBe(75);
        _sut.Volume.ShouldBe(75);
    }

    [Fact]
    public void VolumeValue_ClampsToVolumeMax()
    {
        _sut.VolumeValue = 999;

        _player.Volume.ShouldBe(150);
    }

    [Fact]
    public void VolumeValue_ClampsToZero()
    {
        _sut.VolumeValue = -10;

        _player.Volume.ShouldBe(0);
    }

    [Fact]
    public void IsMuted_CallsPlayerMute()
    {
        _sut.IsMuted = true;

        _player.Received(1).Mute(true);
        _sut.IsMuted.ShouldBeTrue();
    }

    [Fact]
    public void ToggleMute_Toggles()
    {
        _sut.ToggleMute();
        _player.Received(1).Mute(true);

        _sut.ToggleMute();
        _player.Received(1).Mute(false);
    }

    [Fact]
    public void IncreaseVolume_IncreasesByFive()
    {
        _sut.VolumeValue = 50;
        _sut.IncreaseVolume();
        _sut.Volume.ShouldBe(55);
    }

    [Fact]
    public void DecreaseVolume_DecreasesByFive()
    {
        _sut.VolumeValue = 50;
        _sut.DecreaseVolume();
        _sut.Volume.ShouldBe(45);
    }

    [Fact]
    public void VolumeValue_FiresPropertyChanged()
    {
        var changedProperties = new List<string>();
        _sut.PropertyChanged += (_, e) => changedProperties.Add(e.PropertyName!);

        _sut.VolumeValue = 75;

        changedProperties.ShouldContain(nameof(_sut.VolumeValue));
        changedProperties.ShouldContain(nameof(_sut.Volume));
        changedProperties.ShouldContain(nameof(_sut.VolumeText));
    }

    [Fact]
    public void IsMuted_FiresPropertyChanged()
    {
        var changedProperties = new List<string>();
        _sut.PropertyChanged += (_, e) => changedProperties.Add(e.PropertyName!);

        _sut.IsMuted = true;

        changedProperties.ShouldContain(nameof(_sut.IsMuted));
    }

    // ── Equalizer ────────────────────────────────────────────────

    [Fact]
    public void SetEqualizerBand_ClampsAndApplies()
    {
        _sut.SetEqualizerBand(0, 50); // clamp to 20
        _sut.EqualizerBands[0].ShouldBe(20);
    }

    [Fact]
    public void ApplyEqualizerPreset_SetsBands()
    {
        _sut.ApplyEqualizerPreset("Rock");
        _sut.EqualizerPresetName.ShouldBe("Rock");
    }

    [Fact]
    public void ApplyEqualizerPreset_Flat_ResetsAllBands()
    {
        _sut.ApplyEqualizerPreset("Rock");
        _sut.ApplyEqualizerPreset("Flat");

        _sut.EqualizerBands.ShouldAllBe(b => b == 0);
    }

    [Fact]
    public void ToggleAudioNormalization_Toggles()
    {
        _sut.ToggleAudioNormalization();
        _sut.IsAudioNormalizationEnabled.ShouldBeTrue();

        _sut.ToggleAudioNormalization();
        _sut.IsAudioNormalizationEnabled.ShouldBeFalse();
    }

    // ── Audio Delay ──────────────────────────────────────────────

    [Fact]
    public void AudioDelay_SetsPlayerDelay()
    {
        _sut.AudioDelay = 0.5f;

        _player.AudioDelay.ShouldBe(0.5f);
        _sut.AudioDelay.ShouldBe(0.5f);
    }

    [Fact]
    public void ResetAudioDelay_SetsToZero()
    {
        _sut.AudioDelay = 0.5f;
        _sut.ResetAudioDelay();
        _sut.AudioDelay.ShouldBe(0f);
    }

    // ── Player Event Handler: VolumeChanged ─────────────────────

    [Fact]
    public void OnPlayerVolumeChanged_UpdatesVolume()
    {
        _player.Volume.Returns(80.0);

        _player.VolumeChanged += Raise.Event<EventHandler<VolumeChangedEventArgs>>(
            new VolumeChangedEventArgs(80.0));

        _sut.Volume.ShouldBe(80.0);
    }

    [Fact]
    public void OnPlayerVolumeChanged_UpdatesMute()
    {
        _player.VolumeChanged += Raise.Event<EventHandler<VolumeChangedEventArgs>>(
            new VolumeChangedEventArgs(isMuted: true));

        _sut.IsMuted.ShouldBeTrue();
    }

    // ── Player Event Handler: TrackListChanged ──────────────────

    [Fact]
    public void OnPlayerTrackListChanged_RefreshesAudioTracks()
    {
        var sources = new[]
        {
            new SubtitleSource { PathOrId = "1", Language = "eng", IsEnabled = true },
            new SubtitleSource { PathOrId = "2", Language = "jpn", IsEnabled = false },
        };

        _player.TrackListChanged += Raise.Event<EventHandler<TrackListChangedEventArgs>>(
            new TrackListChangedEventArgs(sources, Array.Empty<SubtitleSource>(), Array.Empty<SubtitleSource>()));

        _sut.AudioTracks.Count.ShouldBeGreaterThan(2);
        _sut.AudioTracks.Any(t => t.DisplayName.Contains("English")).ShouldBeTrue();
        _sut.AudioTracks.Any(t => t.DisplayName.Contains("Japanese")).ShouldBeTrue();
    }

    // ── Reset ────────────────────────────────────────────────────

    [Fact]
    public void ResetAllAudio_ResetsVolumeAndMute()
    {
        _sut.VolumeValue = 80;
        _sut.IsMuted = true;

        _sut.ResetAllAudio();

        _sut.Volume.ShouldBe(50);
        _sut.IsMuted.ShouldBeFalse();
    }

    // ── Dispose ──────────────────────────────────────────────────

    [Fact]
    public void Dispose_UnsubscribesFromPlayerEvents()
    {
        _sut.Dispose();

        // After dispose, player events should no longer affect the SUT
        _player.Volume.Returns(90.0);
        _player.VolumeChanged += Raise.Event<EventHandler<VolumeChangedEventArgs>>(
            new VolumeChangedEventArgs(90.0));

        _sut.Volume.ShouldNotBe(90.0);
    }

    [Fact]
    public void Dispose_MultipleCalls_DoesNotThrow()
    {
        _sut.Dispose();
        Should.NotThrow(() => _sut.Dispose());
    }
}
