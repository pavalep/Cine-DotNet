using System;
using System.Linq;
using Avalonia.Threading;
using Cine.Avalonia.Extensions;
using Cine.Avalonia.Models;
using Cine.Media.Events;
using Cine.Media.Models;

namespace Cine.Avalonia.ViewModels;

/// <summary>
/// Track menu building for audio/video tracks.
/// Subtitle tracks are owned by SubtitleManager (single source of truth).
/// </summary>
public partial class MainViewModel
{
    /// <summary>Initializes track menus with "Add..." and "None" pseudo-entries.</summary>
    private void BuildEmptyTrackMenus()
    {
        AudioTracks.Add(new TrackMenuItem("Add Audio Track…", TrackType.Audio, -1, OnSelectAudio));
        AudioTracks.Add(new TrackMenuItem("None", TrackType.Audio, -2, OnSelectAudio));

        VideoTracks.Add(new TrackMenuItem("No video tracks", TrackType.Video, -1, OnSelectVideo));
    }

    /// <summary>
    /// Fired when a new file is loaded. Rebuilds audio/video track menus.
    /// Subtitle tracks are handled by SubtitleManager via its own TrackListChanged subscription.
    /// </summary>
    private void OnPlayerOpened(object? sender, EventArgs e)
    {
        // Notify SubtitleManager so it can load per-file settings
        Subtitles?.NotifyMediaOpened(_filePath);
        // Notify AudioManager so it can load per-file settings
        Audio?.NotifyMediaOpened(_filePath);

        UpdateCropFilter();
        SubtitleSource[] audioSources;
        SubtitleSource[] videoSources;
        try
        {
            audioSources = (_player.AudioSources ?? Array.Empty<AudioTrackInfo>())
                .Select(a => new SubtitleSource
                {
                    PathOrId = a.Id.ToString(),
                    Language = a.Language,
                    Type = "audio",
                    IsEnabled = a.IsSelected
                }).ToArray();
            videoSources = (_player.VideoSources ?? Array.Empty<VideoTrackInfo>())
                .Select(v => new SubtitleSource
                {
                    PathOrId = v.Id.ToString(),
                    Language = v.Title,
                    Type = "video",
                    IsEnabled = v.IsSelected
                }).ToArray();
        }
        catch (Exception ex)
        {
            global::Cine.Core.Log.ForContext<MainViewModel>().Error(ex, "OnPlayerOpened track read failed");
            return;
        }

        Dispatcher.UIThread.OnUiThread(() =>
        {
            try
            {
                // Only handle audio/video here — subtitle tracks are handled by SubtitleManager
                OnTrackListChanged(new TrackListChangedEventArgs(
                    audioSources, videoSources, Array.Empty<SubtitleSource>()));
            }
            catch (Exception ex)
            {
                global::Cine.Core.Log.ForContext<MainViewModel>().Warning("OnPlayerOpened UI update failed: {Error}", ex.Message);
            }
        });
    }

    // ── Track selection handlers (audio/video only) ──

    private void OnSelectAudio(TrackMenuItem item)
    {
        if (item.DisplayName == "Add Audio Track…")
        {
            _ = OnAddAudio();
            return;
        }

        if (item.DisplayName == "None")
        {
            _player.SelectAudioTrack(-1);
            _currentAudioTrackId = -1;
            foreach (var t in AudioTracks) t.RefreshSelection(false);
            item.RefreshSelection(true);
            return;
        }

        if (item.TrackIndex >= 0)
        {
            _player.SelectAudioTrack(item.TrackIndex);
            _currentAudioTrackId = item.TrackIndex;
            foreach (var t in AudioTracks) t.RefreshSelection(false);
            item.RefreshSelection(true);
        }
    }

    private void OnSelectVideo(TrackMenuItem item)
    {
        if (item.DisplayName == "Add Video Track…")
        {
            return;
        }

        if (item.DisplayName == "None")
        {
            _player.SelectVideoTrack(-1);
            foreach (var t in VideoTracks) t.RefreshSelection(false);
            item.RefreshSelection(true);
            return;
        }

        if (item.TrackIndex >= 0)
        {
            _player.SelectVideoTrack(item.TrackIndex);
            foreach (var t in VideoTracks) t.RefreshSelection(false);
            item.RefreshSelection(true);
        }
    }

    /// <summary>
    /// Bridge handler for player TrackListChanged event.
    /// Subtitle tracks are handled by SubtitleManager (own subscription).
    /// Audio/video tracks are handled here.
    /// </summary>
    private void OnTrackListChanged(object? sender, TrackListChangedEventArgs e)
    {
        OnTrackListChanged(e);
    }

    /// <summary>
    /// Rebuilds audio/video track menu items from player track list events.
    /// Subtitle tracks are excluded — handled by SubtitleManager.
    /// </summary>
    private void OnTrackListChanged(TrackListChangedEventArgs e)
    {
        Dispatcher.UIThread.OnUiThread(() =>
        {
            // Audio tracks
            AudioTracks.Clear();
            AudioTracks.Add(new TrackMenuItem("Add Audio Track…", TrackType.Audio, -1, OnSelectAudio));
            AudioTracks.Add(new TrackMenuItem("None", TrackType.Audio, -2, OnSelectAudio));
            if (e.AudioTracks != null)
            {
                int idx = 0;
                foreach (var track in e.AudioTracks)
                {
                    var trackId = int.TryParse(track.PathOrId, out var parsedId) ? parsedId : idx;
                    var item = new TrackMenuItem(
                        FormatTrack("Audio", track),
                        TrackType.Audio,
                        trackId,
                        OnSelectAudio,
                        track
                    );
                    item.IsSelected = track.IsEnabled;
                    AudioTracks.Add(item);
                    idx++;
                }
            }
            IsAudioEnabled = e.AudioTracks?.Any(t => t.IsEnabled) ?? false;

            // Video tracks
            VideoTracks.Clear();
            VideoTracks.Add(new TrackMenuItem("Add Video Track…", TrackType.Video, -1, OnSelectVideo));
            VideoTracks.Add(new TrackMenuItem("None", TrackType.Video, -2, OnSelectVideo));
            if (e.VideoTracks != null && e.VideoTracks.Any())
            {
                int idx = 0;
                foreach (var track in e.VideoTracks)
                {
                    var trackId = int.TryParse(track.PathOrId, out var parsedId) ? parsedId : idx;
                    var item = new TrackMenuItem(
                        FormatTrack("Video", track),
                        TrackType.Video,
                        trackId,
                        OnSelectVideo,
                        track
                    );
                    item.IsSelected = track.IsEnabled;
                    VideoTracks.Add(item);
                    idx++;
                }
            }
            else
            {
                VideoTracks.Add(new TrackMenuItem("No video tracks", TrackType.Video, -1, OnSelectVideo));
            }
            HasMultipleVideoTracks = e.VideoTracks?.Count() > 1;

            // Restore pending track selections (audio only — subtitle is handled by SubtitleManager)
            if (_pendingAudioTrackId.HasValue)
            {
                var audTrack = AudioTracks.FirstOrDefault(t =>
                    t.TrackIndex == _pendingAudioTrackId.Value && !t.IsPseudoEntry);
                if (audTrack != null && audTrack.SelectCommand?.CanExecute(audTrack) == true)
                    audTrack.SelectCommand.Execute(audTrack);
                _pendingAudioTrackId = null;
            }
        });
    }
}
