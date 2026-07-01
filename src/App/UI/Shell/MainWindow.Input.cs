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
using Cine.Avalonia.ViewModels;
using Cine.Avalonia.Views.Dialogs;
using Cine.Avalonia.Builders;
using Material.Icons;
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
        DebugLog("[DBG] RegisterKeyboardShortcuts: enter, _inputRouter=" + (_inputRouter == null ? "null" : "not-null"));
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
            // Close any open flyout first
            _flyoutManager.CloseAll();
            // If no flyout was open, exit fullscreen
            if (_playerService?.Player?.IsFullscreen == true)
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

        // ── Go To Time ──
        Register(Key.G, KeyModifiers.Control, () =>
        {
            var dlg = new GoToTimeDialog { DataContext = _viewModel };
            dlg.Show(this);
        }, "Go To Time");

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
        Register(Key.OemComma, KeyModifiers.Control, () => ShowDialogWithScope(() =>
        {
            var prefs = new PreferencesDialog { DataContext = _viewModel };
            prefs.Show(this);
        }), "Preferences");
        Register(Key.OemQuestion, KeyModifiers.Control, () => ShowDialogWithScope(() =>
        {
            var dlg = new KeyboardShortcutsDialog();
            dlg.Show(this);
        }), "Keyboard Shortcuts");
        Register(Key.G, KeyModifiers.Control, () => ShowDialogWithScope(() =>
        {
            var dlg = new GoToTimeDialog { DataContext = _viewModel };
            dlg.Show(this);
        }), "Go To Time");

        // ── Time Display ──
        Register(Key.T, KeyModifiers.None, () => _controlsBox?.SeekBarControl?.ToggleTimeDisplay(), "Toggle Time Display");

        // Phase 3: Validate all shortcut bindings at startup
        var validation = KeyboardConflictValidator.Validate(_inputRouter!);
        DebugLog($"[DBG] RegisterKeyboardShortcuts: {validation.TotalBindings} bindings registered, {validation.ConflictCount} conflicts");
        if (!validation.IsClean)
        {
            DebugLog($"KeyboardConflictValidator: {validation.ConflictCount} conflicts found in {validation.TotalBindings} bindings.");
            foreach (var c in validation.Conflicts)
                DebugLog($"  Conflict: {c}");
        }
        else
        {
            DebugLog($"KeyboardConflictValidator: Clean — {validation.TotalBindings} bindings, no conflicts.");
        }

        // ── Phase 11: Signature Commands ──
        Register(Key.K, KeyModifiers.Control, ShowCommandPalette, "Command Palette (Ctrl+K)");
        Register(Key.D, KeyModifiers.Control, ToggleNowPlayingInfo, "Now Playing Info (Ctrl+J)");
        Register(Key.OemPeriod, KeyModifiers.Control, ToggleFocusMode, "Focus Mode (Ctrl+.)");

        PopulatePaletteCommands();
    }

    /// <summary>Collects all signal commands for the command palette.</summary>
    private void PopulatePaletteCommands()
    {
        _paletteCommands.Clear();
        // Playback
        AddPalette("Play / Pause", () => _viewModel?.PlayPause());
        AddPalette("Stop", () => _viewModel?.Stop());
        AddPalette("Seek Backward 5s", () => _viewModel?.SeekBackward());
        AddPalette("Seek Forward 5s", () => _viewModel?.SeekForward());
        AddPalette("Seek Backward 30s", () => _viewModel?.SeekLargeBackward());
        AddPalette("Seek Forward 30s", () => _viewModel?.SeekLargeForward());
        AddPalette("Seek Backward 10s", () => _playerService?.Player?.SeekBackward(10));
        AddPalette("Seek Forward 10s", () => _playerService?.Player?.SeekForward(10));
        // Navigation
        AddPalette("Next Chapter", () => _viewModel?.NextChapter());
        AddPalette("Previous Chapter", () => _viewModel?.PreviousChapter());
        AddPalette("Next Frame", () => _playerService?.Player?.NextFrame());
        AddPalette("Previous Frame", () => _playerService?.Player?.PreviousFrame());
        // View
        AddPalette("Toggle Fullscreen", () => _viewModel?.ToggleFullscreen());
        AddPalette("Toggle Focus Mode", ToggleFocusMode);
        AddPalette("Toggle Picture-in-Picture", () => OnPipToggled(null, EventArgs.Empty));
        AddPalette("Toggle Time Display", () => _controlsBox?.SeekBarControl?.ToggleTimeDisplay());
        AddPalette("Now Playing Info", ToggleNowPlayingInfo);
        // Speed
        AddPalette("Increase Speed", () => _playerService?.Player?.IncreaseSpeed());
        AddPalette("Decrease Speed", () => _playerService?.Player?.DecreaseSpeed());
        AddPalette("Reset Speed", () => _playerService?.Player?.ResetSpeed());
        // Audio
        AddPalette("Mute / Unmute", () => _viewModel?.ToggleMute());
        AddPalette("Volume Up", () => _viewModel?.IncreaseVolume());
        AddPalette("Volume Down", () => _viewModel?.DecreaseVolume());
        AddPalette("Toggle Shuffle", () => _viewModel?.ToggleShuffle());
        AddPalette("Toggle Loop File", () => _viewModel?.ToggleLoopFile());
        AddPalette("Toggle Loop Playlist", () => _viewModel?.ToggleLoopPlaylist());
        // Subtitles
        AddPalette("Toggle Subtitles", () => { if (_viewModel?.Subtitles != null) _viewModel.Subtitles.IsSubtitleEnabled = !_viewModel.Subtitles.IsSubtitleEnabled; });
        AddPalette("Next Subtitle Track", () => { if (_viewModel?.Subtitles != null) _viewModel.Subtitles.CycleSubtitleTrackForward(); });
        // Screenshots
        AddPalette("Screenshot (with subs)", () => _playerService?.Player?.ScreenshotWithSubtitles());
        AddPalette("Screenshot (no subs)", () => _playerService?.Player?.ScreenshotWithoutSubtitles());
        // Dialogs
        AddPalette("Go to Time…", () => ShowDialogWithScope(() => new GoToTimeDialog { DataContext = _viewModel }.Show(this)));
        AddPalette("Preferences", () => ShowDialogWithScope(() => new PreferencesDialog { DataContext = _viewModel }.Show(this)));
        AddPalette("Keyboard Shortcuts", () => ShowDialogWithScope(() => new KeyboardShortcutsDialog().Show(this)));
        // Zoom
        AddPalette("Zoom In", () => { if (_playerService?.Player != null) _playerService.Player.Zoom += 0.05; });
        AddPalette("Zoom Out", () => { if (_playerService?.Player != null) _playerService.Player.Zoom -= 0.05; });
        // Loop / Playlist
        AddPalette("Next Item", () => _viewModel?.NextItem());
        AddPalette("Previous Item", () => _viewModel?.PreviousItem());
    }

    private void AddPalette(string description, Action action)
        => _paletteCommands.Add((description, action));

    /// <summary>Show the command palette dialog.</summary>
    private void ShowCommandPalette()
    {
        var dlg = new CommandPaletteDialog(_paletteCommands)
        {
            DataContext = _viewModel
        };
        dlg.Show(this);
    }

    /// <summary>Toggle Now Playing info panel overlay.</summary>
    private void ToggleNowPlayingInfo()
    {
        if (NowPlayingInfoPanel == null) return;
        NowPlayingInfoPanel.IsVisible = !NowPlayingInfoPanel.IsVisible;
        if (NowPlayingInfoPanel.IsVisible)
        {
            NowPlayingInfoPanel.SetPlayer(_playerService?.Player);
            NowPlayingInfoPanel.Refresh();
            ShowOsdNotification(MaterialIconKind.InformationOutline, "Now Playing");
        }
    }

    /// <summary>Toggle Focus Mode — hides all chrome except a thin indicator line.</summary>
    private void ToggleFocusMode()
    {
        _isFocusMode = !_isFocusMode;
        if (_isFocusMode)
        {
            _headerBar.IsVisible = false;
            _fullscreenHeader.IsVisible = false;
            _controlsBox.IsVisible = false;
            FocusModeIndicator.IsVisible = true;
            ShowOsdNotification(MaterialIconKind.MoonWaxingCrescent, "Focus Mode");
        }
        else
        {
            _headerBar.IsVisible = WindowState != WindowState.FullScreen;
            _fullscreenHeader.IsVisible = WindowState == WindowState.FullScreen;
            _controlsBox.IsVisible = true;
            FocusModeIndicator.IsVisible = false;
            ShowOsdNotification(MaterialIconKind.MoonWaxingCrescent, "Focus Mode Off");
        }
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
    //  Key Down — global Tunnel handler catches all keys before children
    // ─────────────────────────────────────────────────────────────

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        if (e.Handled) return;

        var key = e.Key;
        var ctrl = e.KeyModifiers.HasFlag(KeyModifiers.Control);
        var shift = e.KeyModifiers.HasFlag(KeyModifiers.Shift);
        DebugLog($"[DBG] OnKeyDown: key={key} ctrl={ctrl} shift={shift}");

        // ── Text-edit scope: skip routing when a text control has focus ──
        var focused = TopLevel.GetTopLevel(this)?.FocusManager?.GetFocusedElement();
        if (focused is global::Avalonia.Controls.TextBox
            or AutoCompleteBox
            or global::Avalonia.Controls.NumericUpDown)
        {
            // Push TextEdit scope so only TextEdit-registered shortcuts fire
            _inputRouter?.PushScope(InputRoutingService.InputScope.TextEdit);
            try
            {
                if (_inputRouter != null && _inputRouter.TryHandle(e))
                    e.Handled = true;
            }
            finally
            {
                _inputRouter?.PopScope();
            }
            return;
        }

        // ── Dialog scope: detect if any owned modal dialog is visible ──
        var hasVisibleDialog = false;
        if (OwnedWindows.Count > 0)
        {
            foreach (var owned in OwnedWindows)
            {
                if (owned.IsVisible)
                {
                    hasVisibleDialog = true;
                    break;
                }
            }
        }

        if (hasVisibleDialog)
        {
            _inputRouter?.PushScope(InputRoutingService.InputScope.DialogOpen);
            try
            {
                if (_inputRouter != null && _inputRouter.TryHandle(e))
                    e.Handled = true;
            }
            finally
            {
                _inputRouter?.PopScope();
            }
            return;
        }

        // ── PIP scope ──
        if (_pipWindowManager is { IsActive: true })
        {
            // Only allow Escape and Ctrl+Shift+P through
            if (key == Key.Escape || (ctrl && shift && key == Key.P))
            {
                _inputRouter?.PushScope(InputRoutingService.InputScope.PipActive);
                try
                {
                    if (_inputRouter != null && _inputRouter.TryHandle(e))
                        e.Handled = true;
                }
                finally
                {
                    _inputRouter?.PopScope();
                }
            }
            else
            {
                e.Handled = true;
            }
            return;
        }

        // ── Normal scope ──
        if (_inputRouter != null && _inputRouter.TryHandle(e))
        {
            DebugLog($"[DBG] OnKeyDown: handled key={key} ctrl={ctrl} shift={shift}");
            e.Handled = true;
        }
        else
        {
            var routerState = _inputRouter == null ? "null" : $"{_inputRouter.ScopeDepth} scopes, cur={_inputRouter.CurrentScope}";
            DebugLog($"[DBG] OnKeyDown: unhandled key={key} ctrl={ctrl} shift={shift} router={routerState}");
        }
    }

    /// <summary>
    /// Show a dialog. The dialog opens as an owned window; OnKeyDown detects it
    /// automatically via OwnedWindows and switches to DialogOpen scope.
    /// </summary>
    private void ShowDialogWithScope(Action showDialog)
    {
        showDialog();
    }

    private void CloseOpenFlyouts()
    {
        // Close all via FlyoutManager — handles all registered flyouts
        _flyoutManager.CloseAll();
        // Also close any inline flyouts not managed by FlyoutManager
        _controlsBox?.SubtitleOverlay?.HideFlyout();
        _controlsBox?.AudioTrackSelector?.HideFlyout();
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

    private void OnVideoRightTapped(object? sender, TappedEventArgs e)
    {
        try
        {
            var pos = e.GetPosition(this);
            DebugLog($"OnVideoRightTapped: pos=({pos.X:0.##},{pos.Y:0.##}) overlayIsPointerOver={(VideoClickOverlay?.IsPointerOver == true)}");

            var menu = new VideoContextMenuBuilder(this, _viewModel!, _playerService?.Player).Build();
            DebugLog($"OnVideoRightTapped: built menu with {menu.Items.Count} top-level items");

            try
            {
                menu.ShowAt(this);
                DebugLog("OnVideoRightTapped: ShowAt succeeded");
            }
            catch (Exception ex)
            {
                DebugLog($"OnVideoRightTapped: ShowAt threw: {ex}");
            }
        }
        catch (Exception ex)
        {
            DebugLog($"OnVideoRightTapped: handler failed: {ex}");
        }
        finally
        {
            e.Handled = true;
        }
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
