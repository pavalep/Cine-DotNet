using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using Avalonia.Threading;
using Cine.Avalonia.Controls;
using Cine.Avalonia.Extensions;
using Cine.Avalonia.Models;
using Cine.Avalonia.Services;
using Cine.Avalonia.ViewModels;
using Cine.Core;
using Cine.Media.Models;
using Material.Icons;

namespace Cine.Avalonia;

/// <summary>
/// Window state management: OnClosed, property watchers, window position
/// persistence, backdrop opacity, and OSD notification helpers.
/// Extracted from MainWindow.Core.cs to keep partial files manageable.
/// </summary>
public partial class MainWindow
{
    private static string WindowStatePath => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "Cine", "window_state.json");

    protected override void OnClosed(EventArgs e)
    {
        // P5.2: Save window position, size, and state
        Result.From(() =>
        {
            var dir = Path.GetDirectoryName(WindowStatePath);
            if (dir != null) Directory.CreateDirectory(dir);
            var state = new
            {
                Width,
                Height,
                X = Position.X,
                Y = Position.Y,
                Maximized = WindowState == WindowState.Maximized
            };
            File.WriteAllText(WindowStatePath, JsonSerializer.Serialize(state));
        });

        _autoHideTimer?.Stop();
        _autoHideTimer = null;
        _sessionSaveTimer?.Stop();
        _sessionSaveTimer = null;
        _propertyWatcher?.Dispose();
        _propertyWatcher = null;
        _stateManager?.Dispose();
        _stateManager = null;
        _audioManager?.Dispose();
        _audioManager = null;
        _videoManager?.Dispose();
        _videoManager = null;
        _subtitleManager?.Dispose();
        _subtitleManager = null;
        _viewModel?.SaveSession();
        MpvVideoView.Shutdown();
        _playerService?.Dispose();
        _pipWindowManager?.Dispose();
        base.OnClosed(e);
    }

    protected override void OnSizeChanged(SizeChangedEventArgs e)
    {
        base.OnSizeChanged(e);
    }

    // =========================================================================
    // P6.6: Window backdrop opacity — reduce controls opacity when unfocused
    // =========================================================================

    private const double FocusedOpacity = 1.0;
    private const double UnfocusedOpacity = 0.66;

    private void OnWindowActivated(object? sender, EventArgs e)
    {
        FadeHeaderAndControls(FocusedOpacity);
        UpdateFocusBorder(focused: true);
    }

    private void OnWindowDeactivated(object? sender, EventArgs e)
    {
        FadeHeaderAndControls(UnfocusedOpacity);
        UpdateFocusBorder(focused: false);
    }

    // =========================================================================
    // OSD Notification helpers
    // =========================================================================

    private void ShowOsdNotification(string text, double durationMs = 2000)
        => OsdNotificationControl.Show(text, durationMs);
    private void ShowOsdNotification(MaterialIconKind icon, string text, double durationMs = 2000)
        => OsdNotificationControl.ShowWithIcon(icon, text, durationMs);
    private void ShowOsdNotificationWithProgress(MaterialIconKind icon, string text, double value, double durationMs = 1500)
        => OsdNotificationControl.ShowWithProgress(icon, text, value, durationMs);

    /// <summary>
    /// Debounced volume OSD: accumulates rapid volume changes from scroll/wheel
    /// and only shows the OSD after 80ms of inactivity. Prevents flicker.
    /// </summary>
    private void ShowVolumeOsdDebounced(string text, double progress)
    {
        _volumeOsdTimer ??= new DispatcherTimer(TimeSpan.FromMilliseconds(80), DispatcherPriority.Normal,
            (_, _) =>
            {
                _volumeOsdTimer?.Stop();
                ShowOsdNotificationWithProgress(MaterialIconKind.VolumeHigh,
                    _volumeOsdTimer?.Tag?.ToString() ?? text, _pendingVolumeLevel);
            });

        _volumeOsdTimer.Stop();
        _volumeOsdTimer.Tag = text;
        _volumeOsdTimer.Start();
    }

    // =========================================================================
    // P8.3: Typed property watchers — replaces string-based PropertyChanged switch
    // =========================================================================

    private PropertyWatcher? _propertyWatcher;

    private void SetupPropertyWatchers()
    {
        if (_viewModel == null) return;
        _propertyWatcher?.Dispose();
        _propertyWatcher = new PropertyWatcher(_viewModel);

        _propertyWatcher
            .Watch(() => _viewModel.FilePath, filePath =>
            {
                if (!string.IsNullOrEmpty(filePath))
                {
                    if (_isLoading) return;
                    _isLoading = true;
                    _suppressFirstVolumeOsd = true;
                    // Only show loader if StartPage is already hidden (switching files).
                    // On landing page, StartPage IS the loading indicator.
                    if (StartPage?.IsVisible == false)
                        _ = _spinnerOverlay.Start();
                    // Don't hide StartPage here — OnMediaOpened handles fade-out
                    // once the player actually opens the file. This avoids a race
                    // where the watcher hides StartPage before the video is ready.
                    _headerBar.ShowOpenMenu();
                    _headerBar.ShowPrimaryMenu();
                    _headerBar.ShowBackButton();
                    _headerBar.SetPipVisibility(Bounds.Width >= MediumBreakpoint);
                    _headerBar.SetTitle(_viewModel.Title);
                    Title = $"Cine — {_viewModel.Title}";
                }
                else
                {
                    _isLoading = false;
                    _spinnerOverlay.Stop();
                    // Hide replay overlay if it was shown — it would appear on top of StartPage
                    _replayOverlay.Hide();
                    if (StartPage?.IsVisible == false)
                    {
                        StartPage.IsVisible = true;
                        StartPage.Opacity = 0;
                        Dispatcher.UIThread.Post(() =>
                        {
                            if (StartPage != null) StartPage.Opacity = 1;
                        }, DispatcherPriority.Render);
                    }
                    // Refresh the recent files list when returning to StartPage
                    // This ensures cards are rebuilt with correct sizes for the current window state
                    StartPage?.RefreshRecentList();
                    PlaybackBackground.IsVisible = true;
                    _controlsBox?.SetControlsVisibility(false);
                    _headerBar.HideOpenMenu();
                    _headerBar.HidePrimaryMenu();
                    _headerBar.HideBackButton();
                    _headerBar.SetPipVisibility(false);
                    _headerBar.SetTitle("Cine");
                    // ShowUiControls should NOT be called here — when file closes,
                    // controls should stay hidden since StartPage covers them.
                }   // closes else block
            })
            .Watch(nameof(MainViewModel.IsSubtitleEnabled), () => _controlsBox?.SubtitleOverlay?.RefreshIcon())
            .Watch(nameof(MainViewModel.IsAudioEnabled), () => _controlsBox?.AudioTrackSelector?.RefreshIcon())
            .Watch(nameof(MainViewModel.IsMuted), () =>
            {
                _controlsBox.VolumeFlyoutCtrl?.RefreshIcon();
                if (_viewModel.IsMuted || _viewModel.VolumeValue == 0)
                    ShowOsdNotification(MaterialIconKind.VolumeOff, "Muted");
                // Unmute volume is handled by the debounced VolumeValue watcher
            })
            .Watch(() => _viewModel.VolumeValue, vol =>
            {
                _controlsBox.VolumeFlyoutCtrl?.RefreshIcon();
                // Suppress volume OSD during initial file load: the player fires
                // VolumeChanged during init which can trigger duplicate OSDs.
                // The flag is set true when FilePath changes and cleared after
                // the first VolumeValue change post-load.
                if (_suppressFirstVolumeOsd)
                {
                    _suppressFirstVolumeOsd = false;
                    return;
                }
                // Debounce rapid volume changes: only show OSD after scrolling settles
                if (vol > 0 && !_viewModel.IsMuted)
                {
                    var pct = (vol / _viewModel.VolumeMax) * 100.0;
                    _pendingVolumeLevel = pct;
                    ShowVolumeOsdDebounced($"Volume: {vol:F0}%", pct);
                }
            })
            .Watch(() => _viewModel.SpeedValue, speed =>
                ShowOsdNotification(MaterialIconKind.Speedometer, $"Speed: {speed:F1}x", 3000))
            .Watch(() => _viewModel.SeekValue, _ =>
            {
                if (_viewModel is { IsSeeking: false })
                {
                    var seekBar = _controlsBox?.SeekBarControl;
                    if (seekBar != null)
                    {
                        _lastPosition = _viewModel?.Position ?? TimeSpan.Zero;
                        _lastDuration = _viewModel?.Duration ?? TimeSpan.Zero;
                        seekBar.UpdatePosition(_lastPosition);
                        seekBar.UpdateDuration(_lastDuration);
                    }
                }
            });
    }
}
