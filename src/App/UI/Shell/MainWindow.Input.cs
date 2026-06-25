using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Media;
using AvaloniaLayout = Avalonia.Layout;
using Button = Avalonia.Controls.Button;
using Cursor = Avalonia.Input.Cursor;
using KeyEventArgs = Avalonia.Input.KeyEventArgs;
using PointerWheelEventArgs = Avalonia.Input.PointerWheelEventArgs;
using PointerPressedEventArgs = Avalonia.Input.PointerPressedEventArgs;
using RoutedEventArgs = Avalonia.Interactivity.RoutedEventArgs;
using Cine.Avalonia.Controls;
using Cine.Avalonia.Services;
using Cine.Avalonia.Views.Dialogs;
using MaterialIcon = global::Material.Icons.Avalonia.MaterialIcon;
using App = global::Avalonia.Application;
using SizeChangedEventArgs = Avalonia.Controls.SizeChangedEventArgs;
using DragEventArgs = Avalonia.Input.DragEventArgs;
using DragDropEffects = Avalonia.Input.DragDropEffects;

namespace Cine.Avalonia;

public partial class MainWindow
{
    // ─────────────────────────────────────────────────────────────
    //  Keyboard Shortcut Registration
    // ─────────────────────────────────────────────────────────────

    private void RegisterKeyboardShortcuts()
    {
        if (_inputRouter == null) return;

        // ── Playback ──
        Register(Key.Space,                  () => _viewModel?.PlayPause(), "Play / Pause");
        Register(Key.K,                      () => _viewModel?.PlayPause(), "Play / Pause");
        Register(Key.P,          KeyModifiers.None, () => _viewModel?.PlayPause(), "Play / Pause");
        Register(Key.MediaPlayPause,          () => _viewModel?.PlayPause(), "Play / Pause (Media Key)");
        Register(Key.MediaStop,               () => _viewModel?.Stop(),     "Stop (Media Key)");

        // ── Escape (context-sensitive) ──
        Register(Key.Escape, () =>
        {
            if (_headerBar?.HasActiveFlyouts == true)
                _headerBar.CloseOpenFlyouts();
            else if (_playerService?.Player?.IsFullscreen == true)
                _viewModel?.ToggleFullscreen();
        }, "Close Flyout / Exit Fullscreen");

        // ── Fullscreen ──
        Register(Key.F,    KeyModifiers.None, () => _viewModel?.ToggleFullscreen(), "Toggle Fullscreen");
        Register(Key.F11,                    () => _viewModel?.ToggleFullscreen(), "Toggle Fullscreen");

        // ── Volume ──
        Register(Key.M,                       () => _viewModel?.ToggleMute(),       "Mute / Unmute");
        Register(Key.VolumeMute,              () => _viewModel?.ToggleMute(),       "Mute / Unmute (Media Key)");
        Register(Key.Up,                      () => _viewModel?.IncreaseVolume(),   "Volume Up");
        Register(Key.VolumeUp,                () => _viewModel?.IncreaseVolume(),   "Volume Up (Media Key)");
        Register(Key.Down,                    () => _viewModel?.DecreaseVolume(),   "Volume Down");
        Register(Key.VolumeDown,              () => _viewModel?.DecreaseVolume(),   "Volume Down (Media Key)");

        // ── Audio Delay ──
        Register(Key.OemMinus, KeyModifiers.Control, () => _playerService?.Player?.DecreaseAudioDelay(), "Decrease Audio Delay");
        Register(Key.Subtract, KeyModifiers.Control, () => _playerService?.Player?.DecreaseAudioDelay(), "Decrease Audio Delay");
        Register(Key.OemPlus,  KeyModifiers.Control, () => _playerService?.Player?.IncreaseAudioDelay(), "Increase Audio Delay");
        Register(Key.Add,      KeyModifiers.Control, () => _playerService?.Player?.IncreaseAudioDelay(), "Increase Audio Delay");

        // ── Seek ──
        Register(Key.Left, () => SeekThrottled(() => _viewModel?.SeekBackward()),                         "Seek Backward");
        Register(Key.Left, KeyModifiers.Shift, () => SeekThrottled(() => _viewModel?.SeekLargeBackward()), "Seek Large Backward");
        Register(Key.Left, KeyModifiers.Control, () => SeekThrottled(() => _viewModel?.PreviousChapter()), "Previous Chapter");
        Register(Key.Right,                      () => SeekThrottled(() => _viewModel?.SeekForward()),      "Seek Forward");
        Register(Key.Right, KeyModifiers.Shift,  () => SeekThrottled(() => _viewModel?.SeekLargeForward()), "Seek Large Forward");
        Register(Key.Right, KeyModifiers.Control,() => SeekThrottled(() => _viewModel?.NextChapter()),      "Next Chapter");
        Register(Key.J,     KeyModifiers.None,    () => _playerService?.Player?.SeekBackward(10),           "Seek Backward 10s");
        Register(Key.L,     KeyModifiers.None,    () => _playerService?.Player?.SeekForward(10),            "Seek Forward 10s");

        // ── Frame stepping ──
        Register(Key.OemOpenBrackets,  KeyModifiers.Control, () => _playerService?.Player?.PreviousFrame(), "Previous Frame");
        Register(Key.OemCloseBrackets, KeyModifiers.Control, () => _playerService?.Player?.NextFrame(),     "Next Frame");

        // ── Media Keys ──
        Register(Key.MediaNextTrack,     () => _viewModel?.NextChapter(),    "Next Chapter (Media Key)");
        Register(Key.MediaPreviousTrack, () => _viewModel?.PreviousChapter(),"Previous Chapter (Media Key)");

        // ── Subtitle (legacy cycle) ──
        Register(Key.C, () => _playerService?.Player?.CycleSubtitleTrack(), "Cycle Subtitle Track");

        // ── Subtitle Delay ──
        Register(Key.OemComma,  () => _playerService?.Player?.DecreaseSubtitleDelay(), "Decrease Subtitle Delay");
        Register(Key.OemPeriod, () => _playerService?.Player?.IncreaseSubtitleDelay(), "Increase Subtitle Delay");

        // ── Subtitle Position ──
        Register(Key.PageUp,   () => _playerService?.Player?.SetSubtitlePosition((_playerService?.Player?.SubtitlePosition ?? 50) - 1), "Subtitle Position Up");
        Register(Key.PageDown, () => _playerService?.Player?.SetSubtitlePosition((_playerService?.Player?.SubtitlePosition ?? 50) + 1), "Subtitle Position Down");

        // ── Zoom ──
        Register(Key.OemPlus,  KeyModifiers.None, () => { if (_playerService?.Player != null) _playerService.Player.Zoom += 0.05; }, "Zoom In");
        Register(Key.Add,      KeyModifiers.None, () => { if (_playerService?.Player != null) _playerService.Player.Zoom += 0.05; }, "Zoom In");
        Register(Key.OemMinus, KeyModifiers.None, () => { if (_playerService?.Player != null) _playerService.Player.Zoom -= 0.05; }, "Zoom Out");
        Register(Key.Subtract, KeyModifiers.None, () => { if (_playerService?.Player != null) _playerService.Player.Zoom -= 0.05; }, "Zoom Out");

        // ── Video Filters ──
        Register(Key.D1, () => _playerService?.Player?.DecreaseContrast(),   "Decrease Contrast");
        Register(Key.D2, () => _playerService?.Player?.IncreaseContrast(),   "Increase Contrast");
        Register(Key.D3, () => _playerService?.Player?.DecreaseBrightness(), "Decrease Brightness");
        Register(Key.D4, () => _playerService?.Player?.IncreaseBrightness(), "Increase Brightness");
        Register(Key.D5, () => _playerService?.Player?.DecreaseGamma(),      "Decrease Gamma");
        Register(Key.D6, () => _playerService?.Player?.IncreaseGamma(),      "Increase Gamma");
        Register(Key.D7, () => _playerService?.Player?.DecreaseSaturation(), "Decrease Saturation");
        Register(Key.D8, () => _playerService?.Player?.IncreaseSaturation(), "Increase Saturation");

        // ── Speed ──
        Register(Key.OemOpenBrackets,  KeyModifiers.None, () => _playerService?.Player?.DecreaseSpeed(), "Decrease Speed");
        Register(Key.OemCloseBrackets, KeyModifiers.None, () => _playerService?.Player?.IncreaseSpeed(), "Increase Speed");
        Register(Key.Back,                              () => _playerService?.Player?.ResetSpeed(),     "Reset Speed");

        // ── Screenshots ──
        Register(Key.S, KeyModifiers.None,  () => _playerService?.Player?.ScreenshotWithSubtitles(),    "Screenshot (with subtitles)");
        Register(Key.S, KeyModifiers.Shift, () => _playerService?.Player?.ScreenshotWithoutSubtitles(), "Screenshot (no subtitles)");

        // ── Equalizer ──
        Register(Key.E, KeyModifiers.Control | KeyModifiers.Shift, () => _controlsBox?.OpenEqualizerFlyout(), "Open Equalizer");

        // ── Loop ──
        Register(Key.L, KeyModifiers.Shift, () => _viewModel?.ToggleLoopFile(),                        "Toggle Loop File");
        Register(Key.I, KeyModifiers.Control,() => _viewModel?.ToggleLoopPlaylist(),                   "Toggle Loop Playlist");

        // ── Subtitle Manager Shortcuts ──
        Register(Key.V, KeyModifiers.None, () => { if (_viewModel?.Subtitles != null) _viewModel.Subtitles.IsSubtitleEnabled = !_viewModel.Subtitles.IsSubtitleEnabled; },
            "Toggle Subtitles");
        Register(Key.G, KeyModifiers.None, () => { if (_viewModel?.Subtitles != null) _viewModel.Subtitles.SubtitleFontScale = Math.Round(Math.Max(0.1, _viewModel.Subtitles.SubtitleFontScale - 0.1), 1); },
            "Decrease Subtitle Font Size");
        Register(Key.G, KeyModifiers.Shift, () => { if (_viewModel?.Subtitles != null) _viewModel.Subtitles.SubtitleFontScale = Math.Round(Math.Min(3.0, _viewModel.Subtitles.SubtitleFontScale + 0.1), 1); },
            "Increase Subtitle Font Size");
        Register(Key.R, KeyModifiers.None, () => { if (_viewModel?.Subtitles != null) _viewModel.Subtitles.SubtitlePosition = Math.Min(100, _viewModel.Subtitles.SubtitlePosition + 1); },
            "Subtitle Position Down (via Manager)");
        Register(Key.R, KeyModifiers.Shift, () => { if (_viewModel?.Subtitles != null) _viewModel.Subtitles.SubtitlePosition = Math.Max(0, _viewModel.Subtitles.SubtitlePosition - 1); },
            "Subtitle Position Up (via Manager)");
        Register(Key.J, KeyModifiers.None, () => { if (_viewModel?.Subtitles != null) _viewModel.Subtitles.CycleSubtitleTrackForward(); },
            "Next Subtitle Track");
        Register(Key.J, KeyModifiers.Shift, () => { if (_viewModel?.Subtitles != null) _viewModel.Subtitles.CycleSubtitleTrackBackward(); },
            "Previous Subtitle Track");
        Register(Key.Z, KeyModifiers.None, () => { if (_viewModel?.Subtitles != null) _viewModel.Subtitles.SubtitleDelay = (float)Math.Clamp(_viewModel.Subtitles.SubtitleDelay - 0.5, -10, 10); },
            "Decrease Subtitle Delay (via Manager)");
        Register(Key.Z, KeyModifiers.Shift, () => { if (_viewModel?.Subtitles != null) _viewModel.Subtitles.SubtitleDelay = (float)Math.Clamp(_viewModel.Subtitles.SubtitleDelay + 0.5, -10, 10); },
            "Increase Subtitle Delay (via Manager)");
        Register(Key.F, KeyModifiers.None, () => { if (_viewModel?.Subtitles != null) _viewModel.Subtitles.SubtitleFontScale = 1.0; },
            "Reset Subtitle Font Scale");
        Register(Key.D0, KeyModifiers.Control, () => { if (_viewModel?.Subtitles != null) _viewModel.Subtitles.ResetAllSubtitles(); },
            "Reset All Subtitle Settings");

        // ── File Operations ──
        Register(Key.O, KeyModifiers.Control, async () => { var files = await OpenFileDialogAsync(); if (files != null) _viewModel?.OpenFiles(files); },
            "Open File");
        Register(Key.O, KeyModifiers.Control | KeyModifiers.Shift, async () => { var folder = await OpenFolderDialogAsync(); if (folder != null) _viewModel?.OpenFiles(new[] { folder }); },
            "Open Folder");
        Register(Key.U, KeyModifiers.Control, () => { /* Placeholder for future URL streaming */ }, "Open URL");
        Register(Key.A, KeyModifiers.Control | KeyModifiers.Shift, async () => { var files = await OpenAddFilesDialogAsync(); if (files != null) _viewModel?.OpenFiles(files); },
            "Add Files to Playlist");

        // ── Playlist ──
        Register(Key.S, KeyModifiers.Control, () => _viewModel?.Stop(),              "Stop");
        Register(Key.P, KeyModifiers.Control, () => _controlsBox?.OpenPlaylistDialog(), "Open Playlist");
        Register(Key.N, KeyModifiers.None,    () => _viewModel?.NextItem(),          "Next Item");
        Register(Key.B, KeyModifiers.None,    () => _viewModel?.PreviousItem(),      "Previous Item");
        Register(Key.H, KeyModifiers.None,    () => _viewModel?.ToggleShuffle(),     "Toggle Shuffle");

        // ── PIP ──
        Register(Key.P, KeyModifiers.Control | KeyModifiers.Shift, () => OnPipToggled(null, EventArgs.Empty), "Toggle Picture-in-Picture");

        // ── Dialogs ──
        Register(Key.OemComma, KeyModifiers.Control, () => { var prefs = new PreferencesDialog { DataContext = _viewModel }; prefs.Show(this); },
            "Preferences");
        Register(Key.OemQuestion, KeyModifiers.Control, () => { var dlg = new KeyboardShortcutsDialog(); dlg.Show(this); },
            "Keyboard Shortcuts");
        Register(Key.G, KeyModifiers.Control, () => { var dlg = new GoToTimeDialog { DataContext = _viewModel }; dlg.Show(this); },
            "Go To Time");

        // ── Time Display ──
        Register(Key.T, KeyModifiers.None, () => _controlsBox?.SeekBarControl?.ToggleTimeDisplay(), "Toggle Time Display");
    }

    private void Register(Key key, Action action, string description)
        => _inputRouter?.Register(KeyModifiers.None, key, action, description);

    private void Register(Key key, KeyModifiers modifiers, Action action, string description)
        => _inputRouter?.Register(modifiers, key, action, description);

    private void SeekThrottled(Action action)
    {
        var now = DateTime.UtcNow;
        if ((now - _lastSeekRepeat).TotalMilliseconds < 90) return;
        _lastSeekRepeat = now;
        action();
    }

    // ─────────────────────────────────────────────────────────────
    //  Key Down — routed through InputRoutingService
    // ─────────────────────────────────────────────────────────────

    private void OnKeyDown(object? sender, KeyEventArgs e)
    {
        var key = e.Key;
        var shift = e.KeyModifiers.HasFlag(KeyModifiers.Shift);
        var ctrl = e.KeyModifiers.HasFlag(KeyModifiers.Control);

        // When PIP is active, only allow Escape and Ctrl+Shift+P through
        if (_pipWindowManager is { IsActive: true } &&
            key != Key.Escape && !(ctrl && shift && key == Key.P))
        {
            e.Handled = true;
            return;
        }

        // Detect if any modal dialog is open → switch scope to DialogOpen
        var scope = InputRoutingService.InputScope.Normal;
        if (OwnedWindows.Count > 0)
        {
            // Check if any owned window is modal (visible dialog)
            foreach (var owned in OwnedWindows)
            {
                if (owned.IsVisible)
                {
                    scope = InputRoutingService.InputScope.DialogOpen;
                    break;
                }
            }
        }

        // Route through InputRoutingService with detected scope
        if (_inputRouter != null && _inputRouter.TryHandle(e, scope))
        {
            e.Handled = true;
        }
    }

    private void CloseOpenFlyouts()
    {
        var flyoutsToClose = new[] { _controlsBox?.BtnVolumeMenu, _headerBar?.BtnOpenMenu, _headerBar?.BtnPrimaryMenu };
        foreach (var btn in flyoutsToClose)
            if (btn?.Flyout is Flyout f)
                f.Hide();
        _controlsBox?.SubtitleOverlayCtrl?.HideFlyout();
        _controlsBox?.AudioTrackSelectorCtrl?.HideFlyout();
        if (_controlsBox?.BtnVideoMenu?.Flyout is Flyout fv)
            fv.Hide();
    }

    // ─────────────────────────────────────────────────────────────
    //  Pointer / Click Handlers (referenced by MainWindow.axaml)
    // ─────────────────────────────────────────────────────────────

    private void OnVideoPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
        {
            // If any flyout is open, the click was just dismissing the flyout —
            // don't toggle play/pause.
            if (_controlsBox.HasActiveFlyouts ||
                _headerBar.HasActiveFlyouts ||
                _fullscreenHeader.HasActiveFlyouts)
                return;

            _viewModel?.PlayPause();
        }
    }

    private void OnVideoDoubleTapped(object? sender, TappedEventArgs e)
    {
        _viewModel?.ToggleFullscreen();
        e.Handled = true;
    }

    private void OnStartPagePointerPressed(object? sender, PointerPressedEventArgs e)
    {
        // StartPage click — handled by StartPage control internally
    }

    // ─────────────────────────────────────────────────────────────
    //  Window Drag & Drop (called from Core.cs AddHandler)
    // ─────────────────────────────────────────────────────────────

    private void OnWindowDragEnter(object? sender, DragEventArgs e)
    {
        _dropIndicator?.Show();
        e.DragEffects = DragDropEffects.Link;
        e.Handled = true;
    }

    private void OnWindowDragLeave(object? sender, DragEventArgs e)
    {
        _dropIndicator?.Hide();
    }

    private void OnWindowDrop(object? sender, DragEventArgs e)
    {
        _dropIndicator?.Hide();
        // Handled by StartOverlayHandler
    }

    // ─────────────────────────────────────────────────────────────
    //  Responsive Layout Init (called from Core.cs)
    // ─────────────────────────────────────────────────────────────

    private void InitializeResponsiveLayout()
    {
    }
}
