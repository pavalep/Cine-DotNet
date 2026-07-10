using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Media;
using Cine.Avalonia.Services;
using Cine.Avalonia.Constants;
using Cine.Core.Services;
using Cine.Avalonia.ViewModels;
using Cine.Avalonia.Views.Resources;
using Cine.Avalonia.Views.Dialogs;
using Cine.Avalonia.Views.Components;
using Cine.Avalonia.Views.Shell;
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

    public SeekBar SeekBarControl => SeekBar;
    public global::Avalonia.Controls.Border ControlsBoxElement => ControlsBoxBorder;

    // Events for primary menu actions that need window-level handling
    // (now handled via HeaderBar.PrimaryPipToggled etc.)

    public ControlsBox()
    {
        InitializeComponent();
        DataContextChanged += OnDataContextChanged;
        if (DataContext is MainViewModel vm) _viewModel = vm;
    }

    private void OnDataContextChanged(object? sender, EventArgs e)
    {
        _viewModel = DataContext as MainViewModel;
        WireMenuPanelEvents();
    }

    private void WireMenuPanelEvents()
    {
        if (_viewModel == null) return;

        var host = PanelHost;
        if (host == null) return;

        // Volume Panel
        host.MainVolumePanel.MuteClicked += OnToggleMute;
        host.MainVolumePanel.Volume25Clicked += OnPresetVolume25;
        host.MainVolumePanel.Volume50Clicked += OnPresetVolume50;
        host.MainVolumePanel.Volume100Clicked += OnPresetVolume100;

        // Playlist Panel — hide request
        host.MainPlaylistPanel.HideRequested += (_, _) =>
        {
            host.MainPlaylistPanel.IsVisible = false;
            host.UpdatePanelDismissState();
        };
    }

    private void OnFirstLoadedForPlayPause(object? sender, EventArgs e)
    {
        Loaded -= OnFirstLoadedForPlayPause;
        _hasPendingPlayPauseSync = false;
    }

    private bool _hasPendingPlayPauseSync;

    // ====================================================================
    //  PANEL TOGGLE — toggle IsVisible on MainWindow-root panels
    //  Panels live at the MainWindow root level (outside ContentClip),
    //  so they are never clipped. We reference them through PanelHost.
    // ====================================================================

    private MainWindow? PanelHost => TopLevel.GetTopLevel(this) as MainWindow;

    public void ToggleSubtitlePanel()
    {
        var host = PanelHost;
        var panel = host?.MainSubtitlePanel;
        if (host == null || panel == null || _viewModel == null) return;

        if (panel.IsVisible)
        {
            panel.IsVisible = false;
            host.UpdatePanelDismissState();
        }
        else
        {
            HideAllInlinePanels();

            panel.SetTrackData(
                tracks: _viewModel.SubtitleTracks,
                manager: _viewModel.Subtitles,
                getDelay: () => _viewModel.Subtitles?.SubtitleDelay ?? 0,
                setDelay: value =>
                {
                    if (_viewModel.Subtitles != null)
                        _viewModel.Subtitles.SubtitleDelay = (float)value;
                },
                resetDelay: () =>
                {
                    if (_viewModel.Subtitles != null)
                        _viewModel.Subtitles.SubtitleDelay = 0;
                },
                dismiss: () => { panel.IsVisible = false; host.UpdatePanelDismissState(); }
            );

            panel.IsVisible = true;
            host.EnablePanelDismiss();
        }
    }

    public void ToggleAudioTrackPanel()
    {
        var host = PanelHost;
        var panel = host?.MainAudioTrackPanel;
        if (host == null || panel == null || _viewModel == null) return;

        if (panel.IsVisible)
        {
            panel.IsVisible = false;
            host.UpdatePanelDismissState();
        }
        else
        {
            HideAllInlinePanels();

            panel.SetTrackData(
                tracks: _viewModel.AudioTracks,
                getDelay: () => _viewModel.Audio?.AudioDelay ?? 0,
                setDelay: value =>
                {
                    if (_viewModel.Audio != null)
                        _viewModel.Audio.AudioDelay = (float)value;
                },
                resetDelay: () => _viewModel.Audio?.ResetAudioDelay(),
                dismiss: () => { panel.IsVisible = false; host.UpdatePanelDismissState(); }
            );

            panel.IsVisible = true;
            host.EnablePanelDismiss();
        }
    }

    private void ToggleVolumePanel()
    {
        var host = PanelHost;
        var panel = host?.MainVolumePanel;
        if (host == null || panel == null) return;

        if (panel.IsVisible)
        {
            // Same button -> close this panel
            panel.IsVisible = false;
            host.UpdatePanelDismissState();
        }
        else
        {
            // Different button -> hide siblings, show this one
            HideAllInlinePanels();
            panel.IsVisible = true;
            host.EnablePanelDismiss();
        }
    }

    private void TogglePanel(Control? panel)
    {
        var host = PanelHost;
        if (host == null || panel == null) return;

        if (panel.IsVisible)
        {
            // Same button -> close this panel
            panel.IsVisible = false;
            host.UpdatePanelDismissState();
        }
        else
        {
            // Different button -> hide siblings, show this one
            HideAllInlinePanels();
            panel.IsVisible = true;
            host.EnablePanelDismiss();
        }
    }

    public void HideAllInlinePanels()
    {
        var host = PanelHost;
        if (host == null) return;
        host.HideAllPanels();
    }

    public void TogglePlaylistPanel()
    {
        TogglePanel(PanelHost?.MainPlaylistPanel);
    }

    // ====================================================================
    //  PLAY/PAUSE
    // ====================================================================

    /// <summary>
    /// SINGLE authoritative method for updating the play/pause icon.
    /// All callers (MainWindow, event handlers) must go through this method.
    /// Never read from ViewModel here — always receive the isPlaying value as a parameter.
    /// </summary>
    public void SyncPlayPauseIcon(bool isPlaying)
    {
        if (PlayPauseIcon == null)
        {
            if (!this.IsLoaded && !_hasPendingPlayPauseSync)
            {
                _hasPendingPlayPauseSync = true;
                Loaded += OnFirstLoadedForPlayPause;
                return;
            }

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
            var showPlay = !isPlaying;
            PlayPauseIcon.Kind = Material.Icons.MaterialIconKind.Play;
            PlayPauseAltIcon.Kind = Material.Icons.MaterialIconKind.Pause;
            PlayPauseIcon.Opacity = showPlay ? 1 : 0;
            PlayPauseAltIcon.Opacity = showPlay ? 0 : 1;
        }
    }

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
        if (_replayMode)
        {
            _replayMode = false;
            _viewModel.PlayPause();
            Cine.Core.Log.ForContext<ControlsBox>().Debug("OnPlayPause replay mode: _viewModel.PlayPause() called");
            return;
        }
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

    private double _volumeBeforeMute = 50;

    private void OnToggleMute(object? sender, RoutedEventArgs e)
    {
        if (_viewModel == null) return;
        if (_viewModel.IsMuted)
        {
            _viewModel.IsMuted = false;
            _viewModel.VolumeValue = _volumeBeforeMute > 0 ? _volumeBeforeMute : 50;
        }
        else
        {
            _volumeBeforeMute = _viewModel.VolumeValue;
            _viewModel.IsMuted = true;
            _viewModel.VolumeValue = 0;
        }
    }

    private void OnVolumeSliderPointerPressed(object? sender, PointerPressedEventArgs e) => e.Handled = true;

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

    // --- Subtitle preferences ---

    private void OnSubtitlePreferences(object? sender, RoutedEventArgs e)
    {
        var w = TopLevel.GetTopLevel(this) as Window;
        if (w != null) new PreferencesWindow().Show(w);
    }

    // --- Equalizer inline panel ---

    public void TriggerEqualizer()
    {
        if (_viewModel == null) return;

        var host = PanelHost;
        if (host == null) return;
        var panel = host.MainEqualizerPanel;

        // Set audio manager on the panel (safe to call multiple times)
        if (_viewModel.Audio != null)
            panel.SetAudioManager(_viewModel.Audio);

        if (panel.IsVisible)
        {
            panel.IsVisible = false;
            host.UpdatePanelDismissState();
        }
        else
        {
            HideAllInlinePanels();
            panel.IsVisible = true;
            host.EnablePanelDismiss();
        }
    }

    private void OnEqualizerClick(object? sender, RoutedEventArgs e)
    {
        TriggerEqualizer();
    }

    // --- Playlist inline panel ---

    public void OpenPlaylistDialog()
    {
        OnOpenPlaylistDialog(this, new RoutedEventArgs());
    }

    private void OnOpenPlaylistDialog(object? sender, RoutedEventArgs e)
    {
        TogglePlaylistPanel();
    }

    // --- Inline panel toggle buttons (replacing old flyout controls) ---

    private void OnVolumeBtnClick(object? sender, RoutedEventArgs e) => ToggleVolumePanel();
    private void OnChaptersBtnClick(object? sender, RoutedEventArgs e) => TogglePanel(PanelHost?.MainChaptersPanel);
    private void OnSubtitlesBtnClick(object? sender, RoutedEventArgs e) => ToggleSubtitlePanel();
    private void OnAudioBtnClick(object? sender, RoutedEventArgs e) => ToggleAudioTrackPanel();
}
