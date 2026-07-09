using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Interactivity;
using Avalonia.Media;
using Cine.Avalonia.Services;
using Cine.Avalonia.Constants;
using Cine.Avalonia.ViewModels;
using Cine.Avalonia.Views.Resources;
using Cine.Avalonia.Views.Dialogs;
using Cine.Avalonia.Views.Components;
using AvaloniaLayout = Avalonia.Layout;
using Button = global::Avalonia.Controls.Button;
using PointerPressedEventArgs = Avalonia.Input.PointerPressedEventArgs;
using Control = Avalonia.Controls.Control;
using ToolTip = Avalonia.Controls.ToolTip;

namespace Cine.Avalonia.Views.Components;

public partial class ControlsBox : AvaloniaUserControl
{
    private MainViewModel? _viewModel;
    private bool _replayMode;
    private PlaylistDialog? _playlistDialog;
    private IFlyoutService? _flyoutManager;

    public SeekBar SeekBarControl => SeekBar;
    public SubtitleOverlay? SubtitleOverlay => SubOverlayCtrl;
    public AudioTrackSelector? AudioTrackSelector => AudioOverlay;
    public VolumeFlyout? VolumeFlyoutCtrl => VolumeFlyoutField;
    public ChaptersFlyout? ChaptersFlyoutCtrl => ChaptersFlyoutField;
    public global::Avalonia.Controls.Border ControlsBoxElement => ControlsBoxBorder;

    public ControlsBox()
    {
        InitializeComponent();
        DataContextChanged += OnDataContextChanged;
        // Also capture DataContext if already set before handler was attached
        if (DataContext is MainViewModel vm) _viewModel = vm;
        // Event subscription will be set when FlyoutManager is assigned
    }

    private void OnDataContextChanged(object? sender, EventArgs e)
    {
        _viewModel = DataContext as MainViewModel;
    }

    private void OnFirstLoadedForPlayPause(object? sender, EventArgs e)
    {
        Loaded -= OnFirstLoadedForPlayPause;
        _hasPendingPlayPauseSync = false;
        // The icon update will be triggered by the next state change from MainWindow
    }

    // --- Public API for MainWindow ---

    public bool HasActiveFlyouts => _flyoutManager?.HasActiveFlyouts == true;

    /// <summary>
    /// Flyout ecosystem manager. When set, all flyouts controlled by this control
    /// are registered for mutual exclusion — opening one auto-closes all others.
    /// Also forwarded to SubtitleOverlayCtrl and AudioTrackSelectorCtrl.
    /// </summary>
    public IFlyoutService? FlyoutManager
    {
        get => _flyoutManager;
        set
        {
            _flyoutManager = value;
            if (value == null) return;

            // Pass to child controls (volume and chapters register their own keys)
            if (SubOverlayCtrl != null) SubOverlayCtrl.FlyoutManager = value;
            if (AudioOverlay != null) AudioOverlay.FlyoutManager = value;
            if (VolumeFlyoutField != null) VolumeFlyoutField.FlyoutManager = value;
            if (ChaptersFlyoutField != null) ChaptersFlyoutField.FlyoutManager = value;
            // VideoTrackSelector removed
        }
    }

    private bool _hasPendingPlayPauseSync;

    /// <summary>
    /// SINGLE authoritative method for updating the play/pause icon.
    /// All callers (MainWindow, event handlers) must go through this method.
    /// Never read from ViewModel here — always receive the isPlaying value as a parameter.
    /// </summary>
    public void SyncPlayPauseIcon(bool isPlaying)
    {
        if (PlayPauseIcon == null)
        {
            // Control tree not ready yet — defer once via UI thread.
            // Use a flag to avoid accumulating multiple Loaded handlers.
            if (!this.IsLoaded && !_hasPendingPlayPauseSync)
            {
                _hasPendingPlayPauseSync = true;
                Loaded += OnFirstLoadedForPlayPause;
                return;
            }

            // If loaded but still null (e.g. template not applied), defer once
            global::Avalonia.Threading.Dispatcher.UIThread.Post(() =>
            {
                if (PlayPauseIcon != null) SyncPlayPauseIcon(isPlaying);
            }, global::Avalonia.Threading.DispatcherPriority.Render);
            return;
        }

        if (_replayMode)
        {
            PlayPauseIcon.Kind = Material.Icons.MaterialIconKind.Replay;
            PlayPauseAltIcon.Kind = Material.Icons.MaterialIconKind.Replay;
            PlayPauseIcon.Opacity = 1;
            PlayPauseAltIcon.Opacity = 0;
            PlayPauseIcon.InvalidateVisual();
            Cine.Core.Log.ForContext<ControlsBox>().Debug("SyncPlayPauseIcon: replay mode -> Replay");
        }
        else
        {
            Cine.Core.Log.ForContext<ControlsBox>().Debug("SyncPlayPauseIcon: isPlaying={IsPlaying}", isPlaying);
            // Crossfade: show play icon when paused, pause icon when playing
            var showPlay = !isPlaying;
            PlayPauseIcon.Kind = Material.Icons.MaterialIconKind.Play;
            PlayPauseAltIcon.Kind = Material.Icons.MaterialIconKind.Pause;
            PlayPauseIcon.Opacity = showPlay ? 1 : 0;
            PlayPauseAltIcon.Opacity = showPlay ? 0 : 1;
        }
    }

    /// <summary>
    /// Set replay mode flag without calling SyncPlayPauseIcon.
    /// Call SyncPlayPauseIcon separately to update the icon.
    /// </summary>
    public void SetReplayMode(bool replayMode)
    {
        _replayMode = replayMode;
        Cine.Core.Log.ForContext<ControlsBox>().Debug("SetReplayMode({ReplayMode})", replayMode);

    }

    public void SetControlsVisibility(bool visible)
    {
        ControlsBoxBorder.IsVisible = visible;
    }

    public void UpdateFullscreenIcon(bool isFullscreen)
    {
        if (isFullscreen)
        {
            FullscreenIcon.Kind = Material.Icons.MaterialIconKind.FullscreenExit;
            ToolTip.SetTip(BtnFullscreen, "Exit Fullscreen (F)");
            BtnFullscreen.IsChecked = true;
        }
        else
        {
            FullscreenIcon.Kind = Material.Icons.MaterialIconKind.Fullscreen;
            ToolTip.SetTip(BtnFullscreen, "Fullscreen (F)");
            BtnFullscreen.IsChecked = false;
        }
    }

    // --- Responsive layout ---

    public void SetButtonSize(Control? control, double size)
    {
        if (control == null) return;
        control.Width = size;
        control.Height = size;
        if (control is Button btn)
            btn.CornerRadius = new CornerRadius(size / 2);
        else if (control is global::Avalonia.Controls.Primitives.ToggleButton tbtn)
            tbtn.CornerRadius = new CornerRadius(size / 2);
    }

    public void SetVis(Control? c, bool v) { if (c != null) c.IsVisible = v; }
    public void SetFont(TextBlock? l, double s) { if (l != null) l.FontSize = s; }

    public void UpdateResponsiveLayout(double width)
    {
        bool isNarrow = width < UiConstants.BreakpointNarrow;
        if (isNarrow)
        {
            SetFont(SeekBar.PositionTimeLabel, Token.Size("font-size-caption"));
            SetFont(SeekBar.DurationTimeLabel, Token.Size("font-size-caption"));
        }
        else
        {
            SetFont(SeekBar.PositionTimeLabel, 13);
            SetFont(SeekBar.DurationTimeLabel, 13);
        }
    }

    // --- Transport handlers ---

    private void OnPlayPause(object? sender, RoutedEventArgs e)
    {
        if (_viewModel == null) return;
        Cine.Core.Log.ForContext<ControlsBox>().Debug("OnPlayPause CLICKED. _replayMode={ReplayMode}", _replayMode);
        // If in replay mode, clicking play restarts the video from beginning
        if (_replayMode)
        {
            _replayMode = false;
            _viewModel.PlayPause(); // ViewModel handles stop+seek+play internally
            Cine.Core.Log.ForContext<ControlsBox>().Debug("OnPlayPause replay mode: _viewModel.PlayPause() called");
            return;
        }
        // Do NOT optimistically toggle the icon — let PlaybackStateManager's StateChanged
        // event update the icon through SyncPlayPauseIcon. This avoids the double-press
        // bug where the icon shows Pause before mpv has actually started playing.
        Cine.Core.Log.ForContext<ControlsBox>().Debug("OnPlayPause: _viewModel.PlayPause() called");
        _viewModel.PlayPause();
    }

    private void OnPrevious(object? sender, RoutedEventArgs e)
    {
        if (_viewModel != null)
        {
            if (_viewModel.HasMultiplePlaylistItems)
                _viewModel.PreviousItem();
            else
                _viewModel.PreviousChapter();
        }
    }
    private void OnNext(object? sender, RoutedEventArgs e)
    {
        if (_viewModel != null)
        {
            if (_viewModel.HasMultiplePlaylistItems)
                _viewModel.NextItem();
            else
                _viewModel.NextChapter();
        }
    }
    private void OnToggleShuffle(object? sender, RoutedEventArgs e) => _viewModel?.ToggleShuffle();
    private void OnToggleLoopFile(object? sender, RoutedEventArgs e) => _viewModel?.ToggleLoopFile();
    private void OnToggleFullscreen(object? sender, RoutedEventArgs e) => _viewModel?.ToggleFullscreen();

    // --- Volume handlers ---

    // Volume controls are now shown via FlyoutOverlay (no inline Flyout)
    // Handlers left stubbed for now:

    private double _volumeBeforeMute = 50;

    private void OnToggleMute(object? sender, RoutedEventArgs e)
    {
        if (_viewModel == null) return;
        if (_viewModel.IsMuted)
        {
            // Restore previous volume
            _viewModel.IsMuted = false;
            _viewModel.VolumeValue = _volumeBeforeMute > 0 ? _volumeBeforeMute : 50;
        }
        else
        {
            // Save current volume and mute
            _volumeBeforeMute = _viewModel.VolumeValue;
            _viewModel.IsMuted = true;
            _viewModel.VolumeValue = 0;
        }
    }
    
    private void OnVolumeSliderPointerPressed(object? sender, PointerPressedEventArgs e) => e.Handled = true;

    // --- Volume preset handlers ---

    private void OnPresetVolume25(object? sender, RoutedEventArgs e)
    {
        if (_viewModel != null) _viewModel.VolumeValue = 37.5;
    }

    private void OnPresetVolume50(object? sender, RoutedEventArgs e)
    {
        if (_viewModel != null) _viewModel.VolumeValue = 75;
    }

    private void OnPresetVolume100(object? sender, RoutedEventArgs e)
    {
        if (_viewModel != null) _viewModel.VolumeValue = 100;
    }

    // --- Track menu handlers ---

    /// <summary>
    /// Opens the audio equalizer flyout. Called from the equalizer button click
    /// or via the Ctrl+Shift+E keyboard shortcut.
    /// </summary>
    public void TriggerEqualizer()
    {
        if (_viewModel == null) return;

        // Show upgrade CTA if equalizer is not available under current tier
        if (!_viewModel.IsEqualizerEnabled)
        {
            var anchor = BtnEqualizer ?? BtnFullscreen;
            if (anchor != null)
                UpgradeCtaContent.Show(_flyoutManager, "equalizer.upgrade", anchor, this, "Equalizer");
            return;
        }

        var eqAnchor = BtnEqualizer ?? BtnFullscreen;
        if (eqAnchor == null) return;

        AudioEqualizerFlyout.Show(_flyoutManager, _viewModel.Audio, eqAnchor, this);
    }

    private void OnEqualizerClick(object? sender, RoutedEventArgs e)
    {
        TriggerEqualizer();
    }

    

    // --- Playlist dialog ---

    /// <summary>
    /// Public entry point for keyboard shortcut (Ctrl+P) to open/activate playlist.
    /// </summary>
    public void OpenPlaylistDialog()
    {
        OnOpenPlaylistDialog(this, new RoutedEventArgs());
    }

    private void OnOpenPlaylistDialog(object? sender, RoutedEventArgs e)
    {
        // Show upgrade CTA if playlist save/load is not available under current tier
        if (_viewModel != null && !_viewModel.IsPlaylistSaveLoadEnabled)
        {
            var anchor = BtnPlaylistDialog ?? BtnFullscreen;
            if (anchor != null)
                UpgradeCtaContent.Show(_flyoutManager, "playlist.upgrade", anchor, this, "Playlist");
            return;
        }

        var w = TopLevel.GetTopLevel(this) as Window;
        if (w == null) return;

        if (_playlistDialog == null)
        {
            _playlistDialog = new PlaylistDialog { DataContext = _viewModel };
            _playlistDialog.Closed += (s, args) => _playlistDialog = null;
            _playlistDialog.Show(w);
        }
        else
        {
            _playlistDialog.Activate();
        }
    }
}
