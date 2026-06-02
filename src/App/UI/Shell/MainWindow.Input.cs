using System;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using AvaloniaLayout = Avalonia.Layout;
using Button = Avalonia.Controls.Button;
using Color = Avalonia.Media.Color;
using Brushes = Avalonia.Media.Brushes;
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
        else if (key == Key.I) 
            Handle(() => { });
        else if (key == Key.L && shift) 
            Handle(() => _viewModel?.ToggleLoopFile());
    }

    private void CloseOpenFlyouts()
    {
        var flyoutsToClose = new[] { _controlsBox.BtnVolumeMenu, _headerBar.BtnOpenMenu, _headerBar.BtnPrimaryMenu };
        foreach (var btn in flyoutsToClose)
            if (btn?.Flyout is Flyout f)
                f.Hide();
        // BtnOptionsMenu handles its own flyout internally
        var trackMenus = new[] { _controlsBox.BtnSubtitlesMenu, _controlsBox.BtnAudioMenu, _controlsBox.BtnVideoMenu };
        foreach (var btn in trackMenus)
            if (btn?.Flyout is Flyout f)
                f.Hide();
    }

    // Guard against duplicate PointerPressed from both VideoClickOverlay and _videoHost
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
            _controlsBox?.UpdatePlayPauseIcon();
            e.Handled = true;
        }
    }

    private void OnVolumeButtonScroll(object? sender, PointerWheelEventArgs e)
    {
        if (_viewModel == null) return;
        if (e.Delta.Y > 0)
            _viewModel.IncreaseVolume();
        else if (e.Delta.Y < 0)
            _viewModel.DecreaseVolume();
        e.Handled = true;
    }

    private void BuildVideoContextMenu(Flyout flyout)
    {
        var stack = new global::Avalonia.Controls.StackPanel();

        void AddItem(string iconKey, string text, string? shortcut, Action action)
        {
            var grid = new Grid
            {
                ColumnDefinitions = new ColumnDefinitions
                {
                    new ColumnDefinition(GridLength.Auto),
                    new ColumnDefinition(GridLength.Star),
                    new ColumnDefinition(GridLength.Auto)
                }
            };

            var iconData = AvaloniaApp.Current?.FindResource(iconKey) as Geometry;
            if (iconData != null)
            {
                var iconPath = new global::Avalonia.Controls.Shapes.Path
                {
                    Data = iconData,
                    Fill = new SolidColorBrush(Color.FromArgb(0xFF, 0xE5, 0xE5, 0xE5)),
                    Width = 14, Height = 14,
                    Stretch = Stretch.Uniform,
                    VerticalAlignment = global::Avalonia.Layout.VerticalAlignment.Center
                };
                Grid.SetColumn(iconPath, 0);
                grid.Children.Add(iconPath);
            }

            var textBlock = new TextBlock
            {
                Text = text,
                FontSize = 13,
                Foreground = new SolidColorBrush(Color.FromArgb(0xFF, 0xE5, 0xE5, 0xE5)),
                Margin = new Thickness(10, 0, 0, 0),
                VerticalAlignment = global::Avalonia.Layout.VerticalAlignment.Center
            };
            Grid.SetColumn(textBlock, 1);
            grid.Children.Add(textBlock);

            if (shortcut != null)
            {
                var shortcutBlock = new TextBlock
                {
                    Text = shortcut,
                    FontSize = 11,
                    Foreground = new SolidColorBrush(Color.FromArgb(0x50, 0xFF, 0xFF, 0xFF)),
                    VerticalAlignment = global::Avalonia.Layout.VerticalAlignment.Center
                };
                Grid.SetColumn(shortcutBlock, 2);
                grid.Children.Add(shortcutBlock);
            }

            var btn = new Button
            {
                Content = grid,
                Background = Brushes.Transparent,
                BorderThickness = new Thickness(0),
                Padding = new Thickness(10, 7),
                HorizontalContentAlignment = global::Avalonia.Layout.HorizontalAlignment.Stretch,
                Cursor = new Cursor(StandardCursorType.Arrow)
            };

            btn.PointerEntered += (_, _) =>
                btn.Background = new SolidColorBrush(Color.FromArgb(0x14, 0xFF, 0xFF, 0xFF));
            btn.PointerExited += (_, _) =>
                btn.Background = Brushes.Transparent;
            btn.Click += (_, _) => { action(); flyout.Hide(); };

            stack.Children.Add(btn);
        }

        AddItem("PlayIcon", "Play / Pause", "Space", () => _viewModel?.PlayPause());
        AddItem("StopIcon", "Stop", "Ctrl+S", () => _viewModel?.Stop());

        stack.Children.Add(new Separator
        {
            Background = new SolidColorBrush(Color.FromArgb(0x33, 0xFF, 0xFF, 0xFF)),
            Margin = new Thickness(4, 2)
        });

        AddItem("SkipBackwardIcon", "Seek Backward", "←", () => _viewModel?.SeekBackward());
        AddItem("SkipForwardIcon", "Seek Forward", "→", () => _viewModel?.SeekForward());
        AddItem("FullscreenEnterIcon", "Fullscreen", "F", () => _viewModel?.ToggleFullscreen());

        stack.Children.Add(new Separator
        {
            Background = new SolidColorBrush(Color.FromArgb(0x33, 0xFF, 0xFF, 0xFF)),
            Margin = new Thickness(4, 2)
        });

        AddItem("SubtitlesIcon", "Cycle Subtitles", "C", () => _playerService?.Player?.CycleSubtitleTrack());
        AddItem("OptionsIcon", "Preferences", "", () => new PreferencesDialog { DataContext = _viewModel }.Show(this));
        AddItem("InfoIcon", "About Cine", "", () => new AboutDialog { DataContext = _viewModel }.Show(this));

        flyout.Content = new Border
        {
            Background = new SolidColorBrush(Color.FromArgb(0xF0, 0x1E, 0x1E, 0x2E)),
            BorderBrush = new SolidColorBrush(Color.FromArgb(0x33, 0xFF, 0xFF, 0xFF)),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(8),
            Padding = new Thickness(4),
            MinWidth = 200,
            Child = stack
        };
    }
}
