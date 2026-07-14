using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Platform.Storage;
using AvaloniaLayout = Avalonia.Layout;
using Button = Avalonia.Controls.Button;
using Cursor = Avalonia.Input.Cursor;
using KeyEventArgs = Avalonia.Input.KeyEventArgs;
using PointerWheelEventArgs = Avalonia.Input.PointerWheelEventArgs;
using PointerPressedEventArgs = Avalonia.Input.PointerPressedEventArgs;
using RoutedEventArgs = Avalonia.Interactivity.RoutedEventArgs;
using Simba.Avalonia.Controls;
using Simba.Avalonia.Services;
using Microsoft.Extensions.DependencyInjection;
using Simba.Avalonia.ViewModels;
using Simba.Avalonia.Views.Dialogs;
using Simba.Avalonia.Views.Components;
using Material.Icons;
using MaterialIcon = global::Material.Icons.Avalonia.MaterialIcon;
using App = global::Avalonia.Application;
using SizeChangedEventArgs = Avalonia.Controls.SizeChangedEventArgs;
using DragEventArgs = Avalonia.Input.DragEventArgs;
using DragDropEffects = Avalonia.Input.DragDropEffects;

namespace Simba.Avalonia.Views.Shell;

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
            // Close any open panel — if one was open, don't also exit fullscreen
            if (AreAnyPanelsOpen())
                HideAllPanels();
            else if (_playerService?.Player?.IsFullscreen == true)
                _viewModel?.ToggleFullscreen();
        }, "Close Panel / Exit Fullscreen");

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

        // Go To Time dialog removed — not needed as a popup

        // ── Equalizer ──
        Register(Key.E, KeyModifiers.Control | KeyModifiers.Shift, () => PlayerPage.ControlsBoxControl?.TriggerEqualizer(), "Open Equalizer");

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
        Register(Key.O, KeyModifiers.Control | KeyModifiers.Shift, async () =>
        {
            var folder = await OpenFolderDialogAsync();
            if (folder != null && _viewModel != null)
                await _viewModel.OpenFolderFromPathAsync(folder);
        }, "Open Folder");
        Register(Key.U, KeyModifiers.Control, () => { /* Placeholder for future URL streaming */ }, "Open URL");
        Register(Key.A, KeyModifiers.Control | KeyModifiers.Shift, async () => { var files = await OpenAddFilesDialogAsync(); if (files != null) _viewModel?.OpenFiles(files); },
            "Add Files to Playlist");

        // ── Playlist ──
        Register(Key.S, KeyModifiers.Control, () => _viewModel?.Stop(),              "Stop");
        Register(Key.P, KeyModifiers.Control, () => PlayerPage.ControlsBoxControl?.OpenPlaylistDialog(), "Open Playlist");
        Register(Key.N, KeyModifiers.None,    () => _viewModel?.NextItem(),          "Next Item");
        Register(Key.B, KeyModifiers.None,    () => _viewModel?.PreviousItem(),      "Previous Item");
        Register(Key.H, KeyModifiers.None,    () => _viewModel?.ToggleShuffle(),     "Toggle Shuffle");

        // ── PIP ──
        Register(Key.P, KeyModifiers.Control | KeyModifiers.Shift, () => OnPipToggled(null, EventArgs.Empty), "Toggle Picture-in-Picture");

        // ── Dialogs ──
        Register(Key.OemComma, KeyModifiers.Control, () => ShowDialogWithScope(() =>
        {
            var audioManager = _serviceProvider.GetRequiredService<IAudioManager>();
            var prefs = new PreferencesWindow(null, audioManager);
            prefs.Show(this);
        }), "Preferences");
        Register(Key.OemQuestion, KeyModifiers.Control, () => ShowDialogWithScope(() =>
        {
            var dlg = new KeyboardShortcutsDialog();
            dlg.Show(this);
        }), "Keyboard Shortcuts");
        // Duplicate Go To Time shortcut removed

        // ── Time Display ──
        Register(Key.T, KeyModifiers.None, () => PlayerPage.ControlsBoxControl?.SeekBarControl?.ToggleTimeDisplay(), "Toggle Time Display");

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
        AddPalette("Toggle Time Display", () => PlayerPage.ControlsBoxControl?.SeekBarControl?.ToggleTimeDisplay());
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
        // Go to Time palette command removed
        AddPalette("Preferences", () => ShowDialogWithScope(() =>
        {
            var audioManager = _serviceProvider.GetRequiredService<IAudioManager>();
            new PreferencesWindow(null, audioManager).Show(this);
        }));
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
        if (PlayerPage.NowPlayingInfoPanel == null) return;
        PlayerPage.NowPlayingInfoPanel.IsVisible = !PlayerPage.NowPlayingInfoPanel.IsVisible;
        if (PlayerPage.NowPlayingInfoPanel.IsVisible)
        {
            PlayerPage.NowPlayingInfoPanel.SetPlayer(_playerService?.Player);
            PlayerPage.NowPlayingInfoPanel.Refresh();
            _osdService.ShowWithIcon(MaterialIconKind.InformationOutline, "Now Playing");
        }
    }

    /// <summary>Toggle Focus Mode — hides all chrome except a thin indicator line.</summary>
    private void ToggleFocusMode()
    {
        _isFocusMode = !_isFocusMode;
        if (_isFocusMode)
        {
            PlayerPage.HeaderBarControl.IsVisible = false;
            PlayerPage.FullscreenHeaderControl.IsVisible = false;
            PlayerPage.ControlsBoxControl.IsVisible = false;
            PlayerPage.FocusModeIndicator.IsVisible = true;
            _osdService.ShowWithIcon(MaterialIconKind.MoonWaxingCrescent, "Focus Mode");
        }
        else
        {
            PlayerPage.HeaderBarControl.IsVisible = WindowState != WindowState.FullScreen;
            PlayerPage.FullscreenHeaderControl.IsVisible = WindowState == WindowState.FullScreen;
            PlayerPage.ControlsBoxControl.IsVisible = true;
            ShowUiControls();  // Restore proper opacity and resume auto-hide timer
            PlayerPage.FocusModeIndicator.IsVisible = false;
            _osdService.ShowWithIcon(MaterialIconKind.MoonWaxingCrescent, "Focus Mode Off");
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

    // ─────────────────────────────────────────────────────────────
    //  Pointer / Click Handlers (referenced by MainWindow.axaml)
    // ─────────────────────────────────────────────────────────────

    private DateTime _lastVideoPressTime = DateTime.MinValue;

    private void OnVideoPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        // Left-click on video does nothing (single-click pause removed per user request).
        // Double-click handler handles fullscreen toggle independently.
        if (e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
        {
            var now = DateTime.UtcNow;
            _lastVideoPressTime = now;

            // If any panel is open, the click was just dismissing the panel.
            if (AreAnyPanelsOpen())
                return;
        }
    }

    private void OnVideoDoubleTapped(object? sender, TappedEventArgs e)
    {
        // Toggle fullscreen on double-click. Playback state is preserved
        // (single-click no longer toggles play/pause, so no undo needed).
        _viewModel?.ToggleFullscreen();
        e.Handled = true;
    }

    private void OnVideoRightTapped(object? sender, TappedEventArgs e)
    {
        try
        {
            var pos = e.GetPosition(this);
            DebugLog($"OnVideoRightTapped: pos=({pos.X:0.##},{pos.Y:0.##}) overlayIsPointerOver={(PlayerPage.VideoClickOverlay?.IsPointerOver == true)}");

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

    /// <summary>Process dropped files and folders, delegating to the ViewModel.</summary>
    private async Task OpenDroppedFiles(DragEventArgs e)
    {
        var droppedFiles = e.DataTransfer.TryGetFiles();
        if (droppedFiles == null) return;

        var paths = new List<string>();
        foreach (var item in droppedFiles)
        {
            var path = item.TryGetLocalPath();
            if (string.IsNullOrEmpty(path)) continue;
            paths.Add(path);
        }

        if (paths.Count > 0 && _viewModel != null)
            await _viewModel.OpenDroppedFilesAsync(paths.ToArray());
    }

    /// <summary>Header bar drag — enables window dragging from the custom title bar.</summary>
    private void OnHeaderPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (e.GetCurrentPoint(this).Properties.IsLeftButtonPressed && e.Source is not Button)
            BeginMoveDrag(e);
    }

    // ─────────────────────────────────────────────────────────────
    //  Window Drag & Drop (registered in Wiring.cs)
    //  These fire at all times — even when StartPage is hidden during playback.
    // ─────────────────────────────────────────────────────────────

    private int _windowDragCounter;

    private void OnWindowDragEnter(object? sender, DragEventArgs e)
    {
        _windowDragCounter++;
        if (e.DataTransfer != null && e.DataTransfer.Contains(DataFormat.File))
        {
            e.DragEffects = DragDropEffects.Copy;
            _osdService.ShowWithIcon(MaterialIconKind.FileVideo, "Drop to Play");
        }
        else
        {
            e.DragEffects = DragDropEffects.None;
        }
        e.Handled = true;
    }

    private void OnWindowDragOver(object? sender, DragEventArgs e)
    {
        e.DragEffects = e.DataTransfer != null && e.DataTransfer.Contains(DataFormat.File)
            ? DragDropEffects.Copy
            : DragDropEffects.None;
        e.Handled = true;
    }

    private void OnWindowDragLeave(object? sender, DragEventArgs e)
    {
        _windowDragCounter = Math.Max(0, _windowDragCounter - 1);
    }

    private async void OnWindowDrop(object? sender, DragEventArgs e)
    {
        _windowDragCounter = 0;
        await OpenDroppedFiles(e);
    }

    // ─────────────────────────────────────────────────────────────
    //  Responsive Layout Init (called from Core.cs)
    // ─────────────────────────────────────────────────────────────

    // Responsive layout removed — was only a stub

    // ─────────────────────────────────────────────────────
    //  Input / Interaction State
    // ─────────────────────────────────────────────────────
    private DateTime _lastSeekRepeat = DateTime.MinValue;
    private DateTime _lastTapTime = DateTime.MinValue;
    private readonly List<(string description, Action action)> _paletteCommands = new();
    private bool _isFocusMode;

    // ─────────────────────────────────────────────────────
    //  File Dialog Delegates (used by keyboard shortcuts)
    // ─────────────────────────────────────────────────────
    private Task<string[]?> OpenFileDialogAsync() =>
        _dialogHandler!.OpenFilesAsync()!;

    private Task<string?> OpenFolderDialogAsync() =>
        _dialogHandler!.OpenFolderAsync()!;

    private Task<string[]?> OpenAddFilesDialogAsync() =>
        _dialogHandler!.AddFilesAsync()!;

    private Task<string?> OpenSubtitleDialogAsync() =>
        _dialogHandler!.OpenSubtitleAsync()!;

    private Task<string?> OpenAudioDialogAsync() =>
        _dialogHandler!.OpenAudioAsync()!;
}
