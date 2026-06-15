using System;
using System.Collections.ObjectModel;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
using Cine.Avalonia.ViewModels;
using Cine.Avalonia.Views.Dialogs;
using Cine.Avalonia.Managers;
using Cine.Avalonia.Builders;
using Cine.Avalonia.Models;
using Cine.Media.Models;
using AvaloniaLayout = Avalonia.Layout;
using Button = global::Avalonia.Controls.Button;
using Cursor = Avalonia.Input.Cursor;
using PointerPressedEventArgs = Avalonia.Input.PointerPressedEventArgs;
using PointerWheelEventArgs = Avalonia.Input.PointerWheelEventArgs;
using Control = Avalonia.Controls.Control;
using ToolTip = Avalonia.Controls.ToolTip;

namespace Cine.Avalonia.Controls;

public partial class ControlsBoxControl : AvaloniaUserControl
{
    private MainViewModel? _viewModel;
    private bool _replayMode;
    private int _activeFlyouts;
    private PlaylistDialog? _playlistDialog;

    private static void PauseLog(string msg)
    {
        try
        {
            var dir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Cine");
            Directory.CreateDirectory(dir);
            File.AppendAllText(Path.Combine(dir, "cine_playpause.log"), $"[{DateTime.Now:HH:mm:ss.fff}] {msg}{Environment.NewLine}");
        }
        catch { }
    }

    public SeekBarControl SeekBarControl => SeekBar;
    public SubtitleOverlayControl? SubtitleOverlayCtrl => SubOverlayCtrl;
    public AudioTrackSelectorControl? AudioTrackSelectorCtrl => AudioOverlayCtrl;
    public global::Avalonia.Controls.Border ControlsBoxElement => ControlsBox;

    public ControlsBoxControl()
    {
        InitializeComponent();
        DataContextChanged += OnDataContextChanged;

        // Volume flyout auto-dismiss: close after 1.5s of inactivity on slider
        BtnVolumeMenu.Flyout!.Opened += (_, _) =>
        {
            VolumeSlider.PointerWheelChanged += OnVolumeAutoDismiss;
            VolumeSlider.PointerReleased += OnVolumeAutoDismiss;
        };
        BtnVolumeMenu.Flyout.Closed += (_, _) =>
        {
            VolumeSlider.PointerWheelChanged -= OnVolumeAutoDismiss;
            VolumeSlider.PointerReleased -= OnVolumeAutoDismiss;
        };
    }

    private async void OnVolumeAutoDismiss(object? sender, EventArgs e)
    {
        await Task.Delay(1500);
        if (BtnVolumeMenu?.Flyout is Flyout f && f.IsOpen)
            f.Hide();
    }

    private void OnDataContextChanged(object? sender, EventArgs e)
    {
        _viewModel = DataContext as MainViewModel;
    }

    // --- Public API for MainWindow ---

    public bool HasActiveFlyouts => _activeFlyouts > 0;

    /// <summary>
    /// SINGLE authoritative method for updating the play/pause icon.
    /// All callers (MainWindow, event handlers) must go through this method.
    /// Never read from ViewModel here — always receive the isPlaying value as a parameter.
    /// </summary>
    public void SyncPlayPauseIcon(bool isPlaying)
    {
        if (_replayMode)
        {
            PlayPauseIconPath.Kind = Material.Icons.MaterialIconKind.Replay;
            PauseLog($"SyncPlayPauseIcon: replay mode -> Replay");
        }
        else
        {
            var newKind = isPlaying
                ? Material.Icons.MaterialIconKind.Pause
                : Material.Icons.MaterialIconKind.Play;
            PauseLog($"SyncPlayPauseIcon: isPlaying={isPlaying} _replayMode={_replayMode} -> {newKind}");
            PlayPauseIconPath.Kind = newKind;
        }
    }

    /// <summary>
    /// Set replay mode flag without calling SyncPlayPauseIcon.
    /// Call SyncPlayPauseIcon separately to update the icon.
    /// </summary>
    public void SetReplayMode(bool replayMode)
    {
        _replayMode = replayMode;
        PauseLog($"SetReplayMode({replayMode})");
    }

    public void RefreshVolumeIcon()
    {
        if (_viewModel == null) return;
        bool isMuted = _viewModel.IsMuted;
        VolumeIconPath.Kind = isMuted
            ? Material.Icons.MaterialIconKind.VolumeOff
            : _viewModel.VolumeValue switch
            {
                <= 0 => Material.Icons.MaterialIconKind.VolumeOff,
                <= 33 => Material.Icons.MaterialIconKind.VolumeLow,
                <= 66 => Material.Icons.MaterialIconKind.VolumeMedium,
                _ => Material.Icons.MaterialIconKind.VolumeHigh
            };
        MuteToggleIcon.Kind = isMuted
            ? Material.Icons.MaterialIconKind.VolumeOff
            : Material.Icons.MaterialIconKind.VolumeHigh;
    }

    public void SetControlsVisibility(bool visible)
    {
        ControlsBox.IsVisible = visible;
    }

    public void UpdateFullscreenIcon(bool isFullscreen)
    {
        if (isFullscreen)
        {
            FullscreenIconPath.Kind = Material.Icons.MaterialIconKind.FullscreenExit;
            ToolTip.SetTip(BtnFullscreen, "Exit Fullscreen (F)");
            BtnFullscreen.IsChecked = true;
        }
        else
        {
            FullscreenIconPath.Kind = Material.Icons.MaterialIconKind.Fullscreen;
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

    public void UpdateResponsiveLayout(double width, bool hasMultipleVideoTracks)
    {
        bool isNarrow = width < 495;
        if (isNarrow)
        {
            SetVis(BtnVideoMenu, false);
            SetFont(SeekBar.PositionTimeLabel, 11);
            SetFont(SeekBar.DurationTimeLabel, 11);
        }
        else
        {
            SetVis(BtnVideoMenu, hasMultipleVideoTracks);
            SetFont(SeekBar.PositionTimeLabel, 13);
            SetFont(SeekBar.DurationTimeLabel, 13);
        }
    }

    // --- Transport handlers ---

    private void OnPlayPause(object? sender, RoutedEventArgs e)
    {
        if (_viewModel == null) return;
        PauseLog($"OnPlayPause CLICKED. _replayMode={_replayMode}");
        // If in replay mode, clicking play restarts the video from beginning
        if (_replayMode)
        {
            _replayMode = false;
            _viewModel.PlayPause(); // ViewModel handles stop+seek+play internally
            PauseLog($"OnPlayPause replay mode: _viewModel.PlayPause() called");
            return;
        }
        // Do NOT optimistically toggle the icon — let PlaybackStateManager's StateChanged
        // event update the icon through SyncPlayPauseIcon. This avoids the double-press
        // bug where the icon shows Pause before mpv has actually started playing.
        PauseLog($"OnPlayPause: _viewModel.PlayPause() called");
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
    private void OnToggleLoopPlaylist(object? sender, RoutedEventArgs e) => _viewModel?.ToggleLoopPlaylist();
    private void OnToggleFullscreen(object? sender, RoutedEventArgs e) => _viewModel?.ToggleFullscreen();

    private void OnVideoEqualizerClick(object? sender, RoutedEventArgs e)
    {
        var parent = this.VisualRoot as Window;
        if (parent == null) return;
        var dlg = new EqualizerDialog(_viewModel!);
        dlg.Show(parent);
    }

    // --- Volume handlers ---

    private void OnToggleMute(object? sender, RoutedEventArgs e) => _viewModel?.ToggleMute();
    private void OnVolumeSliderPointerPressed(object? sender, PointerPressedEventArgs e) => e.Handled = true;

    private void OnVolumeButtonScroll(object? sender, PointerWheelEventArgs e)
    {
        if (_viewModel == null) return;
        if (e.Delta.Y > 0)
            _viewModel.IncreaseVolume();
        else if (e.Delta.Y < 0)
            _viewModel.DecreaseVolume();
        e.Handled = true;
    }

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

    private void OnEqualizerClick(object? sender, RoutedEventArgs e)
    {
        if (_viewModel == null) return;
        var dialog = new EqualizerDialog(_viewModel);
        var parent = this.VisualRoot as Window;
        if (parent != null) dialog.Show(parent);
    }

    private void OnVideoMenuClick(object? sender, RoutedEventArgs e)
    {
        if (_viewModel == null) return;
        var flyout = BuildTrackMenuFlyout(_viewModel.VideoTracks);
        TrackFlyout(flyout);
        flyout.ShowAt(BtnVideoMenu);
    }

    // --- Chapter menu handler ---

    private void OnChaptersMenuClick(object? sender, RoutedEventArgs e)
    {
        if (_viewModel == null || _viewModel.Chapters.Count == 0) return;
        var flyout = BuildChaptersFlyout(_viewModel.Chapters);
        TrackFlyout(flyout);
        flyout.ShowAt(BtnChaptersMenu);
    }

    private Flyout BuildChaptersFlyout(ObservableCollection<ChapterInfo> chapters)
    {
        var builder = new FlyoutBuilder()
            .WithMinWidth(220)
            .WithMaxHeight(300)
            .WithPlacement(PlacementMode.Top);

        foreach (var chapter in chapters)
        {
            var timeStr = TimeSpan.FromSeconds(chapter.Time).TotalHours >= 1
                ? TimeSpan.FromSeconds(chapter.Time).ToString(@"h\:mm\:ss")
                : TimeSpan.FromSeconds(chapter.Time).ToString(@"mm\:ss");

            var seekTime = chapter.Time;
            builder.AddLabeledItem(chapter.Title, timeStr, () =>
            {
                _viewModel?.SeekTo(seekTime / _viewModel.Duration.TotalSeconds);
            });
        }

        return builder.Build();
    }

    private Flyout BuildTrackMenuFlyout(ObservableCollection<TrackMenuItem> tracks)
    {
        var stackPanel = new global::Avalonia.Controls.StackPanel();

        // Safety: if no real tracks, show fallback message
        var hasRealTracks = tracks.Any(t => !t.IsPseudoEntry);
        if (!hasRealTracks && tracks.Count == 0)
        {
            var text = new TextBlock
            {
                Text = "No tracks available",
                FontSize = 12,
                Foreground = AppColors.TextTertiary,
                Padding = new Thickness(10, 7)
            };
            stackPanel.Children.Add(text);
        }

        foreach (var track in tracks)
        {
            var dot = new Border
            {
                Width = 6, Height = 6,
                CornerRadius = new CornerRadius(3),
                Background = track.IsSelected && !track.IsPseudoEntry
                    ? AppColors.Accent
                    : AppColors.IconDim,
                VerticalAlignment = AvaloniaLayout.VerticalAlignment.Center,
                Margin = new Thickness(0, 0, 8, 0)
            };

            var text = new TextBlock
            {
                Text = track.DisplayName,
                FontWeight = track.IsSelected ? FontWeight.SemiBold : FontWeight.Normal,
                FontSize = 12,
                Foreground = AppColors.TextPrimary
            };

            var grid = new Grid
            {
                ColumnDefinitions = new ColumnDefinitions
                {
                    new ColumnDefinition(GridLength.Auto),
                    new ColumnDefinition(GridLength.Star)
                }
            };
            grid.Children.Add(dot);
            grid.Children.Add(text);
            Grid.SetColumn(text, 1);

            var button = new Button
            {
                Content = grid,
                Background = AppColors.Transparent,
                BorderThickness = new Thickness(0),
                Padding = new Thickness(10, 7),
                HorizontalContentAlignment = AvaloniaLayout.HorizontalAlignment.Stretch,
                Cursor = new Cursor(StandardCursorType.Arrow),
                Opacity = track.DisplayOpacity,
                Command = track.SelectCommand
            };

            button.PointerEntered += (_, _) =>
                button.Background = AppColors.HoverSubtle;
            button.PointerExited += (_, _) =>
                button.Background = AppColors.Transparent;

            stackPanel.Children.Add(button);
        }

        var scroll = new ScrollViewer
        {
            MaxHeight = 320,
            Content = stackPanel
        };

        var border = new Border
        {
            Background = (IBrush?)global::Avalonia.Application.Current?.FindResource("PopoverBackground"),
            BorderBrush = (IBrush?)global::Avalonia.Application.Current?.FindResource("PopoverBorder"),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(8),
            Padding = new Thickness(4),
            MinWidth = 180,
            Child = scroll
        };

        return new Flyout { Content = border, Placement = PlacementMode.Top };
    }

    private void TrackFlyout(Flyout flyout)
    {
        flyout.Opened += (_, _) => _activeFlyouts++;
        flyout.Closed += (_, _) => _activeFlyouts = Math.Max(0, _activeFlyouts - 1);
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

