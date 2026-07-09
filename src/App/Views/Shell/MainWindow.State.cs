// Combined: state management (track changes, session resume) + PIP window management

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
using Cine.Avalonia.Core.Navigation;
using Cine.Avalonia.Extensions;
using Cine.Avalonia.Views.Resources;
using Cine.Avalonia.Models;
using Cine.Avalonia.Services;
using Cine.Avalonia.Services.UI;
using Cine.Avalonia.ViewModels;
using Cine.Core;
using Cine.Media.Events;
using Cine.Media.Models;
using Material.Icons;

namespace Cine.Avalonia.Views.Shell;

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
        // The DI container owns these singletons — no manual Dispose needed after Phase 2.
        _viewModel?.SaveSession();
        PlayerPage.MpvVideoView.Shutdown();
        _playerService?.Dispose();
        _pipWindowManager?.Dispose();
        base.OnClosed(e);
    }

    protected override void OnSizeChanged(SizeChangedEventArgs e)
    {
        base.OnSizeChanged(e);

        bool isMaximized = WindowState == WindowState.Maximized
                        || WindowState == WindowState.FullScreen;

        // Keep rounded-rect clip in sync with ContentClip size so the
        // native video surface doesn't overflow the rounded corners.
        if (ContentClip != null && !isMaximized)
        {
            ContentClip.Clip = CreateRoundedRectClip(
                ContentClip.Bounds.Width, ContentClip.Bounds.Height,
                ContentClip.CornerRadius);
        }

        // Also keep PlayerPage clip in sync.
        // MpvVideoView handles its own internal clip via ArrangeOverride.
        if (PlayerPage != null && !isMaximized)
        {
            PlayerPage.Clip = CreateRoundedRectClip(
                PlayerPage.Bounds.Width, PlayerPage.Bounds.Height,
                new CornerRadius(8));
        }
        if (PlayerPage?.MpvVideoView != null)
        {
            // CornerRadius drives the internal _videoImage clip in ArrangeOverride.
            PlayerPage.MpvVideoView.CornerRadius = isMaximized ? new CornerRadius(0) : new CornerRadius(8);
        }
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
                _osdService.ShowProgress(MaterialIconKind.VolumeHigh,
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
            .Watch(() => _viewModel.IsSubtitleEnabled, _ => PlayerPage.ControlsBoxControl?.SubtitleOverlay?.RefreshIcon())
            .Watch(() => _viewModel.IsAudioEnabled, _ => PlayerPage.ControlsBoxControl?.AudioTrackSelector?.RefreshIcon())
            .Watch(() => _viewModel.IsMuted, _ =>
            {
                PlayerPage.ControlsBoxControl.VolumeFlyoutCtrl?.RefreshIcon();
                if (_viewModel.IsMuted || _viewModel.VolumeValue == 0)
                    _osdService.ShowWithIcon(MaterialIconKind.VolumeOff, "Muted");
            })
            .Watch(() => _viewModel.VolumeValue, vol =>
            {
                PlayerPage.ControlsBoxControl.VolumeFlyoutCtrl?.RefreshIcon();
                if (_suppressFirstVolumeOsd)
                {
                    _suppressFirstVolumeOsd = false;
                    return;
                }
                if (vol > 0 && !_viewModel.IsMuted)
                {
                    var pct = (vol / _viewModel.VolumeMax) * 100.0;
                    _pendingVolumeLevel = pct;
                    ShowVolumeOsdDebounced($"Volume: {vol:F0}%", pct);
                }
            })
            .Watch(() => _viewModel.SpeedValue, speed =>
                _osdService.ShowWithIcon(MaterialIconKind.Speedometer, $"Speed: {speed:F1}x", 3000))
            .Watch(() => _viewModel.SeekValue, _ =>
            {
                if (_viewModel is { IsSeeking: false })
                {
                    var seekBar = PlayerPage.ControlsBoxControl?.SeekBarControl;
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

    // ─────────────────────────────────────────────────────
    //  Navigation — extracted from FilePath watcher (Phase 3)
    // ─────────────────────────────────────────────────────

    private void ShowPlayerUi()
    {
        _suppressFirstVolumeOsd = true;
        if (StartPage?.IsVisible == false)
            _ = PlayerPage.SpinnerOverlay.Start();
        PlayerPage.HeaderBarControl.ShowOpenMenu();
        PlayerPage.HeaderBarControl.ShowPrimaryMenu();
        PlayerPage.HeaderBarControl.ShowBackButton();
        PlayerPage.HeaderBarControl.SetPipVisibility(Bounds.Width >= UiConstants.BreakpointCompact);
        PlayerPage.HeaderBarControl.SetTitle(_viewModel!.Title);
        Title = $"Cine — {_viewModel.Title}";
    }

    private void ShowStartPage()
    {
        _isLoading = false;
        PlayerPage.SpinnerOverlay.Stop();
        PlayerPage.ReplayOverlay.Hide();
        // StartPage.IsVisible is managed by StartPage.OnNavigatedTo
        PlaybackBackground.IsVisible = true;
        PlayerPage.ControlsBoxControl?.SetControlsVisibility(false);
        PlayerPage.HeaderBarControl.HideOpenMenu();
        PlayerPage.HeaderBarControl.HidePrimaryMenu();
        PlayerPage.HeaderBarControl.HideBackButton();
        PlayerPage.HeaderBarControl.SetPipVisibility(false);
        PlayerPage.HeaderBarControl.SetTitle("Cine");
    }

    private void HideStartPage()
    {
        (StartPage as INavigable)?.OnNavigatedFrom();
        PlaybackBackground.IsVisible = false;
    }

    private void OnNavigated(object? sender, NavigationRequest request)
    {
        switch (request.Route)
        {
            case AppRoute.Start:
                ShowStartPage();
                (StartPage as INavigable)?.OnNavigatedTo(request.Parameter);
                _navigationService!.CurrentPage = StartPage as INavigable;
                break;
            case AppRoute.Player:
                ShowPlayerUi();
                (PlayerPage as INavigable)?.OnNavigatedTo(request.Parameter);
                _navigationService!.CurrentPage = PlayerPage as INavigable;
                // If a file path was passed (e.g. from StartPageViewModel), open it
                if (request.Parameter is string path && !string.IsNullOrWhiteSpace(path))
                    _viewModel?.OpenFile(path);
                break;
        }
    }

    // ─────────────────────────────────────────────────────
    //  PIP window management
    // ─────────────────────────────────────────────────────

    /// <summary>Handle PiP toggle from header bar / keyboard shortcut.</summary>
    private void OnPipToggled(object? sender, EventArgs e)
    {
        _pipWindowManager?.OnPipToggled(sender, e);
    }

    /// <summary>Sync PiP position display from player position events.</summary>
    private void SyncPipPosition(object? sender, PositionChangedEventArgs e)
    {
        _pipWindowManager?.SyncPosition(sender, e);
    }

    /// <summary>Sync PiP play/pause state from player state changes.</summary>
    private void SyncPipPlayState(PlaybackState state)
    {
        _pipWindowManager?.SyncPlayState(state);
    }

    /// <summary>Sync PiP replay mode when media ends.</summary>
    private void SyncPipReplayMode(bool isEnded)
    {
        _pipWindowManager?.SyncReplayMode(isEnded);
    }
}
