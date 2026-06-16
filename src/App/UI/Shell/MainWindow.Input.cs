using System;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using AvaloniaLayout = Avalonia.Layout;
using Button = Avalonia.Controls.Button;
using Cursor = Avalonia.Input.Cursor;
using KeyEventArgs = Avalonia.Input.KeyEventArgs;
using PointerWheelEventArgs = Avalonia.Input.PointerWheelEventArgs;
using PointerPressedEventArgs = Avalonia.Input.PointerPressedEventArgs;
using RoutedEventArgs = Avalonia.Interactivity.RoutedEventArgs;
using Cine.Avalonia.Views.Dialogs;
using MaterialIcon = global::Material.Icons.Avalonia.MaterialIcon;

namespace Cine.Avalonia;

public partial class MainWindow
{
    private void OnKeyDown(object? sender, KeyEventArgs e)
    {
        var key = e.Key;
        var shift = e.KeyModifiers.HasFlag(KeyModifiers.Shift);
        var ctrl = e.KeyModifiers.HasFlag(KeyModifiers.Control);

        // When PIP is active, only block keys that would conflict with PIP controls.
        // Allow Escape and Ctrl+Shift+P so user can close or toggle PIP via keyboard.
        if (_pipService is { IsActive: true } &&
            key != Key.Escape && !(ctrl && shift && key == Key.P))
        {
            e.Handled = true;
            return;
        }

        void Handle(Action action) { action(); e.Handled = true; }

        if (key == Key.Space || key == Key.K || key == Key.P || key == Key.MediaPlayPause) 
            Handle(() => _viewModel?.PlayPause());
        else if (key == Key.MediaStop) 
            Handle(() => _viewModel?.Stop());
        else if (key == Key.Escape)
            Handle(() => {
                if (_headerBar.HasActiveFlyouts)
                    _headerBar.CloseOpenFlyouts();
                else if (_playerService?.Player?.IsFullscreen == true)
                    _viewModel?.ToggleFullscreen();
            });
        else if (key == Key.F || key == Key.F11) 
            Handle(() => _viewModel?.ToggleFullscreen());
        else if (key == Key.M || key == Key.VolumeMute) 
            Handle(() => _viewModel?.ToggleMute());
        else if (key == Key.Up || key == Key.VolumeUp) 
            Handle(() => _viewModel?.IncreaseVolume());
        else if (key == Key.Down || key == Key.VolumeDown) 
            Handle(() => _viewModel?.DecreaseVolume());
        else if (ctrl && (key == Key.OemMinus || key == Key.Subtract)) 
            Handle(() => { _playerService?.Player?.DecreaseAudioDelay(); });
        else if (ctrl && (key == Key.OemPlus || key == Key.Add)) 
            Handle(() => { _playerService?.Player?.IncreaseAudioDelay(); });
        else if (key == Key.Left) 
            Handle(() => {
                var now = DateTime.UtcNow;
                if ((now - _lastSeekRepeat).TotalMilliseconds < 90) return;
                _lastSeekRepeat = now;
                if (ctrl) _viewModel?.PreviousChapter(); else if (shift) _viewModel?.SeekLargeBackward(); else _viewModel?.SeekBackward();
            });
        else if (key == Key.Right) 
            Handle(() => {
                var now = DateTime.UtcNow;
                if ((now - _lastSeekRepeat).TotalMilliseconds < 90) return;
                _lastSeekRepeat = now;
                if (ctrl) _viewModel?.NextChapter(); else if (shift) _viewModel?.SeekLargeForward(); else _viewModel?.SeekForward();
            });
        else if (key == Key.J) 
            Handle(() => _playerService?.Player?.SeekBackward(10));
        else if (key == Key.L && !shift && !ctrl) 
            Handle(() => _playerService?.Player?.SeekForward(10));
        else if (ctrl && key == Key.OemOpenBrackets) 
            Handle(() => _playerService?.Player?.PreviousFrame());
        else if (ctrl && key == Key.OemCloseBrackets) 
            Handle(() => _playerService?.Player?.NextFrame());
        else if (key == Key.MediaNextTrack)
            Handle(() => _viewModel?.NextChapter());
        else if (key == Key.MediaPreviousTrack)
            Handle(() => _viewModel?.PreviousChapter());
        else if (key == Key.C) 
            Handle(() => _playerService?.Player?.CycleSubtitleTrack());
        else if (key == Key.OemComma) 
            Handle(() => _playerService?.Player?.DecreaseSubtitleDelay());
        else if (key == Key.OemPeriod) 
            Handle(() => _playerService?.Player?.IncreaseSubtitleDelay());
        else if (key == Key.PageUp) 
            Handle(() => _playerService?.Player?.SetSubtitlePosition((_playerService?.Player?.SubtitlePosition ?? 50) - 1));
        else if (key == Key.PageDown) 
            Handle(() => _playerService?.Player?.SetSubtitlePosition((_playerService?.Player?.SubtitlePosition ?? 50) + 1));
        else if ((key == Key.OemPlus || key == Key.Add) && !ctrl) 
            Handle(() => { if (_playerService?.Player != null) _playerService.Player.Zoom += 0.05; });
        else if ((key == Key.OemMinus || key == Key.Subtract) && !ctrl) 
            Handle(() => { if (_playerService?.Player != null) _playerService.Player.Zoom -= 0.05; });
        else if (key == Key.D1) 
            Handle(() => _playerService?.Player?.DecreaseContrast());
        else if (key == Key.D2) 
            Handle(() => _playerService?.Player?.IncreaseContrast());
        else if (key == Key.D3) 
            Handle(() => _playerService?.Player?.DecreaseBrightness());
        else if (key == Key.D4) 
            Handle(() => _playerService?.Player?.IncreaseBrightness());
        else if (key == Key.D5) 
            Handle(() => _playerService?.Player?.DecreaseGamma());
        else if (key == Key.D6) 
            Handle(() => _playerService?.Player?.IncreaseGamma());
        else if (key == Key.D7) 
            Handle(() => _playerService?.Player?.DecreaseSaturation());
        else if (key == Key.D8) 
            Handle(() => _playerService?.Player?.IncreaseSaturation());
        else if (key == Key.OemOpenBrackets && !ctrl) 
            Handle(() => _playerService?.Player?.DecreaseSpeed());
        else if (key == Key.OemCloseBrackets && !ctrl) 
            Handle(() => _playerService?.Player?.IncreaseSpeed());
        else if (key == Key.Back) 
            Handle(() => _playerService?.Player?.ResetSpeed());
        else if (key == Key.S) 
            Handle(() => { if (shift) _playerService?.Player?.ScreenshotWithoutSubtitles(); else _playerService?.Player?.ScreenshotWithSubtitles(); });
        else if (ctrl && shift && key == Key.E)
            Handle(() => { if (_viewModel != null) { var dlg = new EqualizerDialog(_viewModel); dlg.Show(this); } });
        else if (key == Key.L && shift) 
            Handle(() => _viewModel?.ToggleLoopFile());
        // ── Subtitle shortcuts (via SubtitleManager) ──
        else if (key == Key.V && !ctrl && !shift)
            Handle(() => { if (_viewModel?.Subtitles != null) _viewModel.Subtitles.IsSubtitleEnabled = !_viewModel.Subtitles.IsSubtitleEnabled; });
        else if (key == Key.G && !ctrl && !shift)
            Handle(() => { if (_viewModel?.Subtitles != null) _viewModel.Subtitles.SubtitleFontScale = Math.Round(Math.Max(0.1, _viewModel.Subtitles.SubtitleFontScale - 0.1), 1); });
        else if (key == Key.G && shift && !ctrl)
            Handle(() => { if (_viewModel?.Subtitles != null) _viewModel.Subtitles.SubtitleFontScale = Math.Round(Math.Min(3.0, _viewModel.Subtitles.SubtitleFontScale + 0.1), 1); });
        else if (key == Key.R && !ctrl && !shift)
            Handle(() => { if (_viewModel?.Subtitles != null) _viewModel.Subtitles.SubtitlePosition = Math.Min(100, _viewModel.Subtitles.SubtitlePosition + 1); });
        else if (key == Key.R && shift && !ctrl)
            Handle(() => { if (_viewModel?.Subtitles != null) _viewModel.Subtitles.SubtitlePosition = Math.Max(0, _viewModel.Subtitles.SubtitlePosition - 1); });
        // ── Extended subtitle shortcuts ──
        else if (key == Key.J && !ctrl && !shift)
            Handle(() => { if (_viewModel?.Subtitles != null) _viewModel.Subtitles.CycleSubtitleTrackForward(); });
        else if (key == Key.J && shift && !ctrl)
            Handle(() => { if (_viewModel?.Subtitles != null) _viewModel.Subtitles.CycleSubtitleTrackBackward(); });
        else if (key == Key.Z && !ctrl && !shift)
            Handle(() => { if (_viewModel?.Subtitles != null) _viewModel.Subtitles.SubtitleDelay = (float)Math.Clamp(_viewModel.Subtitles.SubtitleDelay - 0.5, -10, 10); });
        else if (key == Key.Z && shift && !ctrl)
            Handle(() => { if (_viewModel?.Subtitles != null) _viewModel.Subtitles.SubtitleDelay = (float)Math.Clamp(_viewModel.Subtitles.SubtitleDelay + 0.5, -10, 10); });
        else if (key == Key.F && !ctrl && !shift)
            Handle(() => { if (_viewModel?.Subtitles != null) _viewModel.Subtitles.SubtitleFontScale = 1.0; });
        else if (ctrl && key == Key.D0 && !shift)
            Handle(() => { if (_viewModel?.Subtitles != null) _viewModel.Subtitles.ResetAllSubtitles(); });
        // ── Phase 4: Global Keyboard Shortcuts ──
        else if (ctrl && key == Key.O && !shift)
            Handle(async () => { var files = await OpenFileDialogAsync(); if (files != null) _viewModel?.OpenFiles(files); });
        else if (ctrl && key == Key.O && shift)
            Handle(async () => { var folder = await OpenFolderDialogAsync(); if (folder != null) _viewModel?.OpenFiles(new[] { folder }); });
        else if (ctrl && key == Key.U)
            Handle(() => { /* Ctrl+U: Open URL — placeholder for future URL streaming */ });
        else if (ctrl && key == Key.I)
            Handle(() => _viewModel?.ToggleLoopPlaylist());
        else if (ctrl && key == Key.S && !shift)
            Handle(() => _viewModel?.Stop());
        else if (ctrl && key == Key.P && !shift)
            Handle(() => _controlsBox.OpenPlaylistDialog());
        else if (ctrl && shift && key == Key.P)
            Handle(() => OnPipToggled(null, EventArgs.Empty));
        else if (ctrl && key == Key.OemComma)
            Handle(() => { var prefs = new PreferencesDialog { DataContext = _viewModel }; prefs.Show(this); });
        else if (ctrl && key == Key.A && shift)
            Handle(async () => { var files = await OpenAddFilesDialogAsync(); if (files != null) _viewModel?.OpenFiles(files); });
        else if (key == Key.T && !ctrl && !shift)
            Handle(() => _controlsBox?.SeekBarControl?.ToggleTimeDisplay());
        else if (key == Key.N && !ctrl && !shift)
            Handle(() => _viewModel?.NextItem());
        else if (key == Key.B && !ctrl && !shift)
            Handle(() => _viewModel?.PreviousItem());
        else if (key == Key.H && !ctrl && !shift)
            Handle(() => _viewModel?.ToggleShuffle());
        else if (ctrl && key == Key.OemQuestion)
            Handle(() => { var dlg = new KeyboardShortcutsDialog(); dlg.Show(this); });
        else if (ctrl && key == Key.G && !shift)
            Handle(() => { var dlg = new GoToTimeDialog { DataContext = _viewModel }; dlg.Show(this); });
    }

    private void CloseOpenFlyouts()
    {
        var flyoutsToClose = new[] { _controlsBox.BtnVolumeMenu, _headerBar.BtnOpenMenu, _headerBar.BtnPrimaryMenu };
        foreach (var btn in flyoutsToClose)
            if (btn?.Flyout is Flyout f)
                f.Hide();
        // Close subtitle & audio flyouts from standalone overlay controls
        _controlsBox?.SubtitleOverlayCtrl?.HideFlyout();
        _controlsBox?.AudioTrackSelectorCtrl?.HideFlyout();
        if (_controlsBox?.BtnVideoMenu?.Flyout is Flyout fv)
            fv.Hide();
    }

    // Guard against duplicate PointerPressed
    private DateTime _lastClickTime = DateTime.MinValue;

    private void OnVideoPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        var clickNow = DateTime.UtcNow;
        if ((clickNow - _lastClickTime).TotalMilliseconds < 100)
        {
            e.Handled = true;
            return;
        }
        _lastClickTime = clickNow;

        var props = e.GetCurrentPoint(this).Properties;

        if (props.IsRightButtonPressed)
        {
            ShowVideoContextMenu();
            e.Handled = true;
            return;
        }

        if (props.IsMiddleButtonPressed)
        {
            _viewModel?.ToggleMute();
            e.Handled = true;
            return;
        }

        if (props.IsLeftButtonPressed)
        {
            var now = DateTime.UtcNow;
            if ((now - _lastTapTime).TotalMilliseconds < 300)
            {
                _lastTapTime = DateTime.MinValue;
                _viewModel?.ToggleFullscreen();
                e.Handled = true;
                return;
            }

            _lastTapTime = now;
            _viewModel?.PlayPause();
            // Icon updates via PlaybackStateManager.StateChanged — no optimistic toggle
            e.Handled = true;
        }
    }

    // ─── Right-click context menu — extracted to VideoContextMenuBuilder ──

    /// <summary>Show the right-click context menu at pointer position.</summary>
    private void ShowVideoContextMenu()
    {
        try
        {
            var builder = new Builders.VideoContextMenuBuilder(
                this, _viewModel, _playerService?.Player);
            builder.Build().ShowAt(this);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Right-click menu error: {ex.Message}");
        }
    }

    /// <summary>Handles right-click on StartPage (which is on top of VideoClickOverlay when visible).</summary>
    private void OnStartPagePointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (e.GetCurrentPoint(this).Properties.IsRightButtonPressed)
        {
            ShowVideoContextMenu();
            e.Handled = true;
        }
    }
}
