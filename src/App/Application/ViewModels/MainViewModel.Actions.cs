using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Avalonia.Threading;
using Cine.Avalonia.Extensions;
using Cine.Core;
using Cine.Media.Events;
using Cine.Media.Interfaces;
using Cine.Media.Models;

namespace Cine.Avalonia.ViewModels;

/// <summary>
/// File operations, renderer mode, audio EQ presets, and player event handlers.
/// </summary>
public partial class MainViewModel
{
    // ─────────────────────────────────────────────────────
    //  File Operations
    // ─────────────────────────────────────────────────────

    private async Task OnOpenFiles()
    {
        if (_fileDialog == null) return;
        var paths = await _fileDialog.OpenFilesAsync();
        if (paths != null && paths.Length > 0)
            await OpenFiles(paths);
    }

    private async Task OnOpenFolder()
    {
        if (_fileDialog == null) return;
        var path = await _fileDialog.OpenFolderAsync();
        if (!string.IsNullOrEmpty(path))
            await OpenFile(path);
    }

    private async Task OnAddFiles()
    {
        if (_fileDialog == null) return;
        var paths = await _fileDialog.AddFilesAsync();
        if (paths != null)
            foreach (var p in paths)
            {
                _playlistCoordinator.Add(p);
                Playlist.Add(p);
            }
    }

    private async Task OnAddAudio()
    {
        if (_fileDialog == null) return;
        try
        {
            var path = await _fileDialog.OpenAudioAsync();
            if (!string.IsNullOrWhiteSpace(path))
            {
                var player = _player;
                await Task.Run(() => player?.AddAudio(path));
                global::Cine.Core.Log.ForContext<MainViewModel>().Info("Audio track added: {Path}", Path.GetFileName(path));
            }
        }
        catch (Exception ex)
        {
            global::Cine.Core.Log.ForContext<MainViewModel>().Error(ex, "AddAudio failed");
        }
    }

    /// <summary>Load an external subtitle file directly (bypasses file dialog).</summary>
    public void LoadExternalSubtitle(string filePath)
    {
        if (string.IsNullOrWhiteSpace(filePath) || _player == null) return;
        try
        {
            var player = _player;
            _ = Task.Run(() =>
            {
                player.AddSubtitle(filePath);
                Log.ForContext<MainViewModel>().Info("External subtitle loaded: {Path}", Path.GetFileName(filePath));
            });
        }
        catch (Exception ex)
        {
            global::Cine.Core.Log.ForContext<MainViewModel>().Error(ex, "LoadExternalSubtitle failed");
            OnError?.Invoke(this, $"Failed to load subtitle: {ex.Message}");
        }
    }

    /// <summary>Load an external audio file directly (bypasses file dialog).</summary>
    public void LoadExternalAudio(string filePath)
    {
        if (string.IsNullOrWhiteSpace(filePath) || _player == null) return;
        try
        {
            var player = _player;
            _ = Task.Run(() =>
            {
                player.AddAudio(filePath);
                Log.ForContext<MainViewModel>().Info("External audio loaded: {Path}", Path.GetFileName(filePath));
            });
        }
        catch (Exception ex)
        {
            global::Cine.Core.Log.ForContext<MainViewModel>().Error(ex, "LoadExternalAudio failed");
            OnError?.Invoke(this, $"Failed to load audio track: {ex.Message}");
        }
    }

    /// <summary>
    /// Open a file: updates UI state, then offloads the blocking mpv command
    /// to a thread-pool thread so neither the UI nor mpv's own render/event
    /// threads are starved.
    /// </summary>
    public async Task OpenFile(string path)
    {
        if (string.IsNullOrWhiteSpace(path)) return;

        // ── Avalonia / app-layer bookkeeping (no mpv coupling) ──
        Audio?.OnFileClosing();
        Subtitles?.OnFileClosing();
        AddRecentFile(path);
        FilePath = path;
        _currentAudioTrackId = -1;

        // ── mpv hand-off ──
        try
        {
            _player.Open(path);
        }
        catch
        {
            Log.ForContext<MainViewModel>().Warning("Open failed for {Path}", path);
            FilePath = string.Empty;
        }
        finally
        {
            RefreshState();
        }
    }

    /// <summary>
    /// Open a batch of files: updates playlist (Avalonia), then delegates to
    /// <see cref="OpenFile"/> for the mpv hand-off.
    /// </summary>
    public async Task OpenFiles(string[] paths)
    {
        if (paths == null || paths.Length == 0) return;

        _playlistCoordinator.Clear();
        _playlistCoordinator.AddRange(paths);
        Playlist.Clear();
        PlaylistItems.Clear();
        foreach (var path in paths)
            Playlist.Add(path);

        await OpenFile(paths[0]);
        SavePlaylist();
    }

    // ─────────────────────────────────────────────────────
    //  Audio EQ Presets
    // ─────────────────────────────────────────────────────

    private static double[] GetPreset(string name) => name switch
    {
        "Classical" => new[] { 0.0, 0.0, 0.0, 0.0, 0.0, 0.0, -4.0, -4.0, -4.0, -6.0 },
        "Rock" => new[] { 4.0, 3.0, 2.0, 1.0, 0.0, 0.0, 1.0, 2.0, 3.0, 4.0 },
        "Pop" => new[] { -1.0, 0.0, 2.0, 3.0, 4.0, 3.0, 2.0, 0.0, -1.0, -1.0 },
        "Jazz" => new[] { 3.0, 2.0, 1.0, 2.0, 3.0, 3.0, 2.0, 1.0, 1.0, 2.0 },
        "Bass Boost" => new[] { 6.0, 5.0, 4.0, 2.0, 0.0, 0.0, 0.0, 0.0, 0.0, 0.0 },
        _ => new double[10]
    };

    // ─────────────────────────────────────────────────────
    //  Event Handlers
    // ─────────────────────────────────────────────────────

    private TimeSpan _lastPosTextTime = TimeSpan.Zero;
    private double _lastSeekValue;

    private void OnPositionChanged(object? sender, PositionChangedEventArgs e)
    {
        Dispatcher.UIThread.OnUiThread(() =>
        {
            if (IsSeeking) return;

            _isUpdatingPositionFromPlayer = true;
            try
            {
                State = _player.State;

                if (Math.Abs((e.Position - _lastPosTextTime).TotalSeconds) >= 0.1)
                {
                    _lastPosTextTime = e.Position;
                    PositionText = FormatTime(e.Position);
                    DurationText = FormatTime(e.Duration);
                }

                if (Math.Abs(e.NormalizedPosition - _lastSeekValue) >= 0.001)
                {
                    _lastSeekValue = e.NormalizedPosition;
                    SeekValue = e.NormalizedPosition;
                }
            }
            finally
            {
                _isUpdatingPositionFromPlayer = false;
            }
        });
    }

    private void OnPlaybackStateChanged(object? sender, PlaybackStateChangedEventArgs e)
    {
        Dispatcher.UIThread.OnUiThread(() =>
        {
            Log.ForContext<MainViewModel>().Debug("OnPlaybackStateChanged: oldState={Old} newState={New}", _state, e.State);
            State = e.State;
        });
    }

    private void OnVolumeChanged(object? sender, VolumeChangedEventArgs e)
    {
        Dispatcher.UIThread.OnUiThread(() =>
        {
            var playerVolume = Math.Clamp(_player.Volume, 0, VolumeMax);
            if (Math.Abs(_volumeValue - playerVolume) >= 0.001)
            {
                _volumeValue = playerVolume;
                OnPropertyChanged(nameof(VolumeValue));
                OnPropertyChanged(nameof(Volume));
                OnPropertyChanged(nameof(VolumeText));
            }

            var playerMuted = _player.IsMuted;
            if (_isMuted != playerMuted)
            {
                _isMuted = playerMuted;
                OnPropertyChanged(nameof(IsMuted));
            }
        });
    }

    private void OnPlaylistChanged(object? sender, PlaylistChangedEventArgs e)
    {
        Dispatcher.UIThread.OnUiThread(() =>
        {
            Playlist.Clear();
            PlaylistItems.Clear();
            int idx = 0;
            foreach (var item in e.PlaylistItems)
            {
                Playlist.Add(item);
                PlaylistItems.Add(new PlaylistItemViewModel(this, idx, item));
                idx++;
            }
            HasMultiplePlaylistItems = Playlist.Count > 1;
            foreach (var item in PlaylistItems) item.NotifyPlayingChanged();
        });
    }

    private void OnLoopChanged(object? sender, LoopChangedEventArgs e)
    {
        Dispatcher.UIThread.OnUiThread(SyncLoopFlags);
    }

    internal void RefreshState()
    {
        _state = _player.State;
        _volumeValue = Math.Clamp(_player.Volume, 0, VolumeMax);
        _isMuted = _player.IsMuted;
        OnPropertyChanged(nameof(State));
        OnPropertyChanged(nameof(IsPlaying));
        OnPropertyChanged(nameof(IsPaused));
        OnPropertyChanged(nameof(Position));
        OnPropertyChanged(nameof(Duration));
        OnPropertyChanged(nameof(VolumeMax));
        OnPropertyChanged(nameof(VolumeValue));
        OnPropertyChanged(nameof(Volume));
        OnPropertyChanged(nameof(VolumeText));
        OnPropertyChanged(nameof(IsMuted));

        PositionText = FormatTime(_player.Position);
        DurationText = FormatTime(_player.Duration);

        Chapters.Clear();
        ChapterMarkers.Clear();
        foreach (var ch in _player.ChapterList)
        {
            Chapters.Add(ch);
            if (Duration.TotalSeconds > 0)
                ChapterMarkers.Add(ch.Time / Duration.TotalSeconds);
        }
        OnPropertyChanged(nameof(HasChapters));

        RefreshPlaylistState();
        SyncLoopFlags();
        IsShuffleEnabled = _player.IsShuffled;
    }

    private void RefreshPlaylistState()
    {
        Playlist.Clear();
        foreach (var item in _player.Playlist)
            Playlist.Add(item);
        HasMultiplePlaylistItems = Playlist.Count > 1;
    }

    private void SyncLoopFlags()
    {
        IsLoopFileEnabled = _player.LoopMode == LoopMode.File;
        IsLoopPlaylistEnabled = _player.LoopMode == LoopMode.Playlist;
    }

    private static string FormatTrack(string prefix, SubtitleSource track)
    {
        var lang = string.IsNullOrWhiteSpace(track.Language) ? "und" : track.Language;
        var state = track.IsEnabled ? "on" : "off";
        return $"{prefix}: {lang} ({state})";
    }

    private static string FormatTime(TimeSpan ts)
    {
        if (ts < TimeSpan.Zero)
            return "-" + TimeSpan.FromTicks(-ts.Ticks).ToString("hh\\:mm\\:ss");
        return ts.ToString("hh\\:mm\\:ss");
    }

    private string GetScreenshotPath() => _mediaFile.GenerateScreenshotPath();

    public void SeekTo(double normalizedValue)
    {
        if (Duration.TotalSeconds <= 0) return;

        var target = TimeSpan.FromSeconds(normalizedValue * Duration.TotalSeconds);

        _isUpdatingPositionFromPlayer = true;
        try
        {
            _seekValue = Math.Clamp(normalizedValue, 0.0, 1.0);
            OnPropertyChanged(nameof(SeekValue));
            PositionText = FormatTime(target);
        }
        finally
        {
            _isUpdatingPositionFromPlayer = false;
        }

        _player.Seek(target);
    }
}
