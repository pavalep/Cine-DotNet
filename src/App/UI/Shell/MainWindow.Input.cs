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

namespace Cine.Avalonia;

public partial class MainWindow
{
    private void OnKeyDown(object? sender, KeyEventArgs e)
    {
        var key = e.Key;
        var shift = e.KeyModifiers.HasFlag(KeyModifiers.Shift);
        var ctrl = e.KeyModifiers.HasFlag(KeyModifiers.Control);

        if (_pipService is { IsActive: true })
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
            var flyout = new Flyout
            {
                Placement = PlacementMode.Pointer
            };
            BuildVideoContextMenu(flyout);
            flyout.ShowAt(this);
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

    private void BuildVideoContextMenu(Flyout flyout)
    {
        // ── Shared helpers ──
        Border MakeBorder(StackPanel child) => new()
        {
            Background = AppColors.DialogSurface,
            BorderBrush = AppColors.DividerStrong,
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(8),
            Padding = new Thickness(4),
            MinWidth = 200,
            Child = child
        };

        // ── Flat menu item ──
        void AddFlat(StackPanel s, string text, string? shortcut, Action action, bool selected = false)
        {
            var grid = new Grid
            {
                ColumnDefinitions = new ColumnDefinitions
                {
                    new ColumnDefinition(new GridLength(16)),
                    new ColumnDefinition(GridLength.Star),
                    new ColumnDefinition(GridLength.Auto)
                }
            };

            // Checkmark column
            if (selected)
            {
                var check = new TextBlock
                {
                    Text = "✓",
                    FontSize = 12,
                    Foreground = AppColors.Accent,
                    VerticalAlignment = AvaloniaLayout.VerticalAlignment.Center,
                    HorizontalAlignment = AvaloniaLayout.HorizontalAlignment.Center
                };
                grid.Children.Add(check);
            }

            var textBlock = new TextBlock
            {
                Text = text,
                FontSize = 13,
                Foreground = AppColors.TextPrimary,
                VerticalAlignment = AvaloniaLayout.VerticalAlignment.Center
            };
            Grid.SetColumn(textBlock, 1);
            grid.Children.Add(textBlock);

            if (shortcut != null)
            {
                var shortcutBlock = new TextBlock
                {
                    Text = shortcut,
                    FontSize = 11,
                    Foreground = AppColors.TextOnDarkDisabled,
                    VerticalAlignment = AvaloniaLayout.VerticalAlignment.Center
                };
                Grid.SetColumn(shortcutBlock, 2);
                grid.Children.Add(shortcutBlock);
            }

            var btn = new Button
            {
                Content = grid,
                Background = AppColors.Transparent,
                BorderThickness = new Thickness(0),
                Padding = new Thickness(10, 7),
                HorizontalContentAlignment = AvaloniaLayout.HorizontalAlignment.Stretch,
                Cursor = new Cursor(StandardCursorType.Arrow)
            };
            btn.PointerEntered += (_, _) => btn.Background = AppColors.HoverSubtle;
            btn.PointerExited += (_, _) => btn.Background = AppColors.Transparent;
            btn.Click += (_, _) => { action(); flyout.Hide(); };
            s.Children.Add(btn);
        }

        // ── Section header label ──
        void AddHeader(StackPanel s, string text) => s.Children.Add(new TextBlock
        {
            Text = text,
            FontSize = 10,
            FontWeight = FontWeight.Bold,
            Foreground = AppColors.TextPrimary,
            Opacity = 0.4,
            LetterSpacing = 0.8,
            Margin = new Thickness(10, 6, 8, 4),
            VerticalAlignment = AvaloniaLayout.VerticalAlignment.Center
        });

        // ── Submenu item with arrow and nested flyout ──
        void AddSubMenu(StackPanel s, string text, Action<StackPanel> buildSub)
        {
            var subStack = new StackPanel();
            buildSub(subStack);

            var subFlyout = new Flyout
            {
                Placement = PlacementMode.Right,
                ShowMode = FlyoutShowMode.Standard,
                Content = MakeBorder(subStack)
            };

            var grid = new Grid
            {
                ColumnDefinitions = new ColumnDefinitions
                {
                    new ColumnDefinition(GridLength.Star),
                    new ColumnDefinition(new GridLength(16))
                }
            };

            grid.Children.Add(new TextBlock
            {
                Text = text,
                FontSize = 13,
                Foreground = AppColors.TextPrimary,
                VerticalAlignment = AvaloniaLayout.VerticalAlignment.Center
            });
            Grid.SetColumn(grid.Children[^1], 0);

            // Arrow indicator
            grid.Children.Add(new TextBlock
            {
                Text = "▶",
                FontSize = 9,
                Foreground = AppColors.TextOnDarkDisabled,
                VerticalAlignment = AvaloniaLayout.VerticalAlignment.Center,
                HorizontalAlignment = AvaloniaLayout.HorizontalAlignment.Right
            });
            Grid.SetColumn(grid.Children[^1], 1);

            var btn = new Button
            {
                Content = grid,
                Background = AppColors.Transparent,
                BorderThickness = new Thickness(0),
                Padding = new Thickness(10, 7),
                HorizontalContentAlignment = AvaloniaLayout.HorizontalAlignment.Stretch,
                Cursor = new Cursor(StandardCursorType.Arrow)
            };
            btn.PointerEntered += (_, _) => btn.Background = AppColors.HoverSubtle;
            btn.PointerExited += (_, _) => btn.Background = AppColors.Transparent;
            btn.Click += (_, _) =>
            {
                if (subFlyout.IsOpen)
                    subFlyout.Hide();
                else
                    subFlyout.ShowAt(btn);
            };
            s.Children.Add(btn);
        }

        // ── Separator ──
        void AddSep(StackPanel s) => s.Children.Add(new Separator
        {
            Background = AppColors.DividerStrong,
            Margin = new Thickness(4, 2)
        });

        // ═══════════════════════════════════════════════════
        //  Build Menu
        // ═══════════════════════════════════════════════════

        var root = new StackPanel();

        AddFlat(root, "Play / Pause", "Space", () => _viewModel?.PlayPause());
        AddFlat(root, "Stop", "Ctrl+S", () => _viewModel?.Stop());
        AddSep(root);

        // ── Navigate ──
        AddSubMenu(root, "Navigate", s =>
        {
            AddFlat(s, "Seek Backward", "←", () => _viewModel?.SeekBackward());
            AddFlat(s, "Seek Forward", "→", () => _viewModel?.SeekForward());
        });

        // ── Video ──
        AddSubMenu(root, "Video", s =>
        {
            AddFlat(s, "Fullscreen", "F", () => _viewModel?.ToggleFullscreen());
            AddFlat(s, "Always on Top", "", () => Topmost = !Topmost);
            AddSep(s);

            // Aspect Ratio
            AddHeader(s, "ASPECT RATIO");
            AddFlat(s, "Original", null, () => _viewModel?.SetAspectRatio(-1),
                _viewModel?.AspectRatioValue < 0);
            AddFlat(s, "16:9", null, () => _viewModel?.SetAspectRatio(1.7778),
                Math.Abs((_viewModel?.AspectRatioValue ?? 0) - 1.7778) < 0.01);
            AddFlat(s, "16:10", null, () => _viewModel?.SetAspectRatio(1.6),
                Math.Abs((_viewModel?.AspectRatioValue ?? 0) - 1.6) < 0.01);
            AddFlat(s, "4:3", null, () => _viewModel?.SetAspectRatio(1.3333),
                Math.Abs((_viewModel?.AspectRatioValue ?? 0) - 1.3333) < 0.01);
            AddFlat(s, "2.35:1", null, () => _viewModel?.SetAspectRatio(2.35),
                Math.Abs((_viewModel?.AspectRatioValue ?? 0) - 2.35) < 0.01);
            AddSep(s);

            // Crop
            AddHeader(s, "CROP");
            AddFlat(s, "Off", null, () => _viewModel?.ResetCrop());
            AddFlat(s, "16:9", null, () => _viewModel?.SetCrop(1.7778));
            AddFlat(s, "16:10", null, () => _viewModel?.SetCrop(1.6));
            AddFlat(s, "4:3", null, () => _viewModel?.SetCrop(1.3333));
            AddFlat(s, "2.35:1", null, () => _viewModel?.SetCrop(2.35));
        });

        // ── Subtitle ──
        AddSubMenu(root, "Subtitle", s =>
        {
            AddFlat(s, "Cycle Subtitles", "C", () => _playerService?.Player?.CycleSubtitleTrack());
        });

        // ── Speed ──
        AddSubMenu(root, "Speed", s =>
        {
            var currentSpeed = _viewModel?.SpeedValue ?? 1.0;
            AddFlat(s, "0.5×", null, () => _viewModel?.SetSpeed(0.5), Math.Abs(currentSpeed - 0.5) < 0.01);
            AddFlat(s, "1.0×", null, () => _viewModel?.SetSpeed(1.0), Math.Abs(currentSpeed - 1.0) < 0.01);
            AddFlat(s, "1.5×", null, () => _viewModel?.SetSpeed(1.5), Math.Abs(currentSpeed - 1.5) < 0.01);
            AddFlat(s, "2.0×", null, () => _viewModel?.SetSpeed(2.0), Math.Abs(currentSpeed - 2.0) < 0.01);
        });

        AddSep(root);
        AddFlat(root, "Preferences", null, () => new PreferencesDialog { DataContext = _viewModel }.Show(this));
        AddFlat(root, "About Cine", null, () => new AboutDialog { DataContext = _viewModel }.Show(this));

        flyout.Content = MakeBorder(root);
    }
}
