using System;
using System.Linq;
using Avalonia.Threading;
using Cine.Avalonia.Extensions;
using Cine.Avalonia.Models;
using Cine.Media.Events;
using Cine.Media.Models;

namespace Cine.Avalonia.ViewModels;

/// <summary>
/// Track menu building, track selection, and player opened event handler.
/// </summary>
public partial class MainViewModel
{
    /// <summary>Initializes track menus with "Add..." and "None" pseudo-entries.</summary>
    private void BuildEmptyTrackMenus()
    {
        SubtitleTracks.Add(new TrackMenuItem("Add Subtitle Track…", TrackType.Subtitle, -1, OnSelectSubtitle));
        SubtitleTracks.Add(new TrackMenuItem("None", TrackType.Subtitle, -2, OnSelectSubtitle));

        AudioTracks.Add(new TrackMenuItem("Add Audio Track…", TrackType.Audio, -1, OnSelectAudio));
        AudioTracks.Add(new TrackMenuItem("None", TrackType.Audio, -2, OnSelectAudio));

        VideoTracks.Add(new TrackMenuItem("No video tracks", TrackType.Video, -1, OnSelectVideo));
    }

    /// <summary>
    /// Fired when a new file is loaded. Forces a track list refresh.
    /// </summary>
    private void OnPlayerOpened(object? sender, EventArgs e)
    {
        SubtitleSource[] subtitleSources;
        SubtitleSource[] audioSources;
        SubtitleSource[] videoSources;
        try
        {
            subtitleSources = _player.SubtitleSources ?? Array.Empty<SubtitleSource>();
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
                OnTrackListChanged(null, new TrackListChangedEventArgs(
                    audioSources, videoSources, subtitleSources));
            }
            catch (Exception ex)
            {
                global::Cine.Core.Log.ForContext<MainViewModel>().Warning("OnPlayerOpened UI update failed: {Error}", ex.Message);
            }
        });
    }

    // ── Track selection handlers ──

    private void OnSelectSubtitle(TrackMenuItem item)
    {
        if (item.DisplayName == "Add Subtitle Track…")
        {
            _ = OnAddSubtitle();
            return;
        }

        if (item.DisplayName == "None")
        {
            _player.SelectSubtitleTrack(-1);
            _currentSubtitleTrackId = -1;
            foreach (var t in SubtitleTracks) t.RefreshSelection(false);
            item.RefreshSelection(true);
            return;
        }

        if (item.TrackIndex >= 0)
        {
            _player.SelectSubtitleTrack(item.TrackIndex);
            _currentSubtitleTrackId = item.TrackIndex;
            foreach (var t in SubtitleTracks) t.RefreshSelection(false);
            item.RefreshSelection(true);
        }
    }

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
    /// Rebuilds typed track menu items from player track list events.
    /// </summary>
    private void OnTrackListChanged(object? sender, TrackListChangedEventArgs e)
    {
        Dispatcher.UIThread.OnUiThread(() =>
        {
            // Subtitle tracks
            SubtitleTracks.Clear();
            SubtitleTracks.Add(new TrackMenuItem("Add Subtitle Track…", TrackType.Subtitle, -1, OnSelectSubtitle));
            SubtitleTracks.Add(new TrackMenuItem("None", TrackType.Subtitle, -2, OnSelectSubtitle));
            if (e.SubtitleTracks != null)
            {
                int idx = 0;
                foreach (var track in e.SubtitleTracks)
                {
                    var trackId = int.TryParse(track.PathOrId, out var parsedId) ? parsedId : idx;
                    var item = new TrackMenuItem(
                        FormatTrack("Sub", track),
                        TrackType.Subtitle,
                        trackId,
                        OnSelectSubtitle,
                        track
                    );
                    item.IsSelected = track.IsEnabled;
                    SubtitleTracks.Add(item);
                    idx++;
                }
            }
            IsSubtitleEnabled = e.SubtitleTracks?.Any(t => t.IsEnabled) ?? false;

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

            // Auto-restore saved track selections
            if (_pendingSubtitleTrackId.HasValue)
            {
                var subTrack = SubtitleTracks.FirstOrDefault(t =>
                    t.TrackIndex == _pendingSubtitleTrackId.Value && !t.IsPseudoEntry);
                if (subTrack != null && subTrack.SelectCommand?.CanExecute(subTrack) == true)
                    subTrack.SelectCommand.Execute(subTrack);
                _pendingSubtitleTrackId = null;
            }
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
