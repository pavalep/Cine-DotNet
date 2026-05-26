using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using Avalonia.Threading;
using Cine.Media.Events;
using Cine.Media.Interfaces;
using Cine.Media.Models;

namespace Cine.Avalonia.ViewModels;

/// <summary>
/// ViewModel for the main player window. Wraps IMediaPlayer for MVVM binding.
/// </summary>
public class MainViewModel : INotifyPropertyChanged
{
    private readonly IMediaPlayer _player;
    private readonly DispatcherTimer _positionTimer;

    // --- Bindable state ---
    private PlaybackState _state;
    private string _positionText = string.Empty;
    private string _durationText = string.Empty;
    private double _volumeValue;
    private double _speedValue;
    private double _seekValue;
    private bool _isMuted;
    private string _filePath = string.Empty;
    private string _chapterTitle = string.Empty;

    // --- Collections ---
    public ObservableCollection<string> SubtitleTracks { get; } = new();
    public ObservableCollection<string> AudioTracks { get; } = new();
    public ObservableCollection<ChapterInfo> Chapters { get; } = new();
    public ObservableCollection<string> Playlist { get; } = new();

    public MainViewModel(IMediaPlayer player)
    {
        _player = player ?? throw new ArgumentNullException(nameof(player));

        // Wire player events
        _player.Opened += OnMediaOpened;

        // Position polling timer (matching Python's property_observer pattern)
        _positionTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(100)
        };
        _positionTimer.Tick += OnPositionTick;
    }

    // --- Playback commands ---
    public void PlayPause()
    {
        if (_state == PlaybackState.Playing)
            _player.Pause();
        else
            _player.Play();
    }

    public void Stop() => _player.Stop();
    public void SeekForward() => _player.Seek(Position + TimeSpan.FromSeconds(5));
    public void SeekBackward() => _player.Seek(Position - TimeSpan.FromSeconds(5));
    public void SeekLargeForward() => _player.Seek(Position + TimeSpan.FromSeconds(60));
    public void SeekLargeBackward() => _player.Seek(Position - TimeSpan.FromSeconds(60));
    public void IncreaseVolume() => _player.Volume = Math.Min(150, VolumeValue + 10);
    public void DecreaseVolume() => _player.Volume = Math.Max(0, VolumeValue - 10);
    public void ToggleMute() => IsMuted = !_player.IsMuted;
    public void ToggleFullscreen() => _player.SetFullscreen(!_player.IsFullscreen);
    public void NextChapter() => _player.NextChapter();
    public void PreviousChapter() => _player.PreviousChapter();
    public void NextItem() => _player.NextPlaylistItem();
    public void PreviousItem() => _player.PreviousPlaylistItem();
    public void ToggleLoopFile() { /* TODO */ }
    public void ToggleLoopPlaylist() { /* TODO */ }
    public void ResetSpeed() => SpeedValue = 1.0;
    public void Screenshot() => _player.TakeScreenshot(GetScreenshotPath());

    // --- Properties for binding ---
    public PlaybackState State
    {
        get => _state;
        set
        {
            _state = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(IsPlaying));
            OnPropertyChanged(nameof(IsPaused));
        }
    }

    public bool IsPlaying => _state == PlaybackState.Playing;
    public bool IsPaused => _state == PlaybackState.Paused;

    public TimeSpan Position
    {
        get => _player.Position;
        set => _player.Seek(value);
    }

    public string PositionText
    {
        get => _positionText;
        set { _positionText = value; OnPropertyChanged(); }
    }

    public string DurationText
    {
        get => _durationText;
        set { _durationText = value; OnPropertyChanged(); }
    }

    public TimeSpan Duration => _player.Duration;

    public double VolumeValue
    {
        get => _volumeValue;
        set { _volumeValue = value; _player.Volume = value; OnPropertyChanged(); }
    }

    public double SpeedValue
    {
        get => _speedValue;
        set { _speedValue = value; _player.Speed = value; OnPropertyChanged(); }
    }

    public double SeekValue
    {
        get => _seekValue;
        set
        {
            if (Math.Abs(_seekValue - value) > 0.001)
            {
                _seekValue = value;
                if (Duration.TotalSeconds > 0)
                    _player.Seek(TimeSpan.FromSeconds(value * Duration.TotalSeconds));
            }
        }
    }

    public bool IsMuted
    {
        get => _isMuted;
        set { _isMuted = value; _player.IsMuted = value; OnPropertyChanged(); }
    }

    public string FilePath
    {
        get => _filePath;
        set { _filePath = value; OnPropertyChanged(); }
    }

    public string ChapterTitle
    {
        get => _chapterTitle;
        set { _chapterTitle = value; OnPropertyChanged(); }
    }

    // --- Drag & drop support ---
    public async void OpenFile(string path)
    {
        if (string.IsNullOrWhiteSpace(path)) return;
        _player.Open(path);
        FilePath = path;
        await System.Threading.Tasks.Task.Delay(50);
        RefreshState();
    }

    public void OpenFiles(string[] paths)
    {
        if (paths == null || paths.Length == 0) return;
        foreach (var path in paths)
            Playlist.Add(path);
        OpenFile(paths[0]);
    }

    // --- Internal helpers ---
    private void OnPositionTick(object? sender, EventArgs e)
    {
        PositionText = FormatTime(_player.Position);
        DurationText = FormatTime(_player.Duration);
        SeekValue = Duration.TotalSeconds > 0
            ? _player.Position.TotalSeconds / Duration.TotalSeconds
            : 0;
    }

    private void OnMediaOpened(object? sender, EventArgs e)
    {
        RefreshState();
    }

    internal void RefreshState()
    {
        OnPropertyChanged(nameof(IsPlaying));
        OnPropertyChanged(nameof(IsPaused));
        OnPropertyChanged(nameof(Position));
        OnPropertyChanged(nameof(Duration));
        OnPropertyChanged(nameof(VolumeValue));

        Chapters.Clear();
        foreach (var ch in _player.ChapterList)
            Chapters.Add(ch);
    }

    private static string FormatTime(TimeSpan ts)
    {
        if (ts < TimeSpan.Zero)
            return "-" + TimeSpan.FromTicks(-ts.Ticks).ToString("hh\\:mm\\:ss");
        return ts.ToString("hh\\:mm\\:ss");
    }

    private string GetScreenshotPath()
    {
        var dir = Environment.GetFolderPath(Environment.SpecialFolder.MyPictures);
        return Path.Combine(dir, $"cine_screenshot_{DateTime.Now:yyyyMMdd_HHmmss}.png");
    }

    // --- INotifyPropertyChanged ---
    public event PropertyChangedEventHandler? PropertyChanged;
    protected void OnPropertyChanged([CallerMemberName] string? name = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}