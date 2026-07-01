using System;
using System.Collections.ObjectModel;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
using Cine.Avalonia.Services;
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
    private PlaylistDialog? _playlistDialog;
    private FlyoutManager? _flyoutManager;
    private FlyoutOverlayControl? _flyoutOverlay; // cached reference
    private string? _activeFlyoutKey; // tracks current overlay flyout for dismiss cleanup
    private Border? _overlayContent; // cached volume overlay content



    public SeekBarControl SeekBarControl => SeekBar;
    public SubtitleOverlayControl? SubtitleOverlay => SubOverlayCtrl;
    public AudioTrackSelectorControl? AudioTrackSelector => AudioOverlay;
    public global::Avalonia.Controls.Border ControlsBoxElement => ControlsBox;

    public ControlsBoxControl()
    {
        InitializeComponent();
        DataContextChanged += OnDataContextChanged;
        // Event subscription will be set when FlyoutManager is assigned
    }

    private void OnVolumeOverlayDismissed()
    {
        _flyoutManager?.MarkClosed("volume");
    }

    private void OnDataContextChanged(object? sender, EventArgs e)
    {
        _viewModel = DataContext as MainViewModel;
    }

    private void OnVolumeMenuClick(object? sender, RoutedEventArgs e)
    {
        if (_viewModel == null) return;
        
        // Ensure overlay is cached and event is subscribed
        if (_flyoutOverlay == null)
        {
            _flyoutOverlay = MainWindow.GetOverlay(this);
            if (_flyoutOverlay != null)
                _flyoutOverlay.OnBackgroundDismissed += OnVolumeOverlayDismissed;
        }
        
        if (_flyoutOverlay == null) return;

        _flyoutManager?.DismissOthers("volume");

        var content = BuildVolumeContent();
        _overlayContent = content;

        _flyoutOverlay.ShowContent(BtnVolumeMenu, content, placeAbove: true);
    }

    private void OnFirstLoadedForPlayPause(object? sender, EventArgs e)
    {
        Loaded -= OnFirstLoadedForPlayPause;
        _hasPendingPlayPauseSync = false;
        // The icon update will be triggered by the next state change from MainWindow
    }

    // --- Public API for MainWindow ---

    public bool HasActiveFlyouts => _flyoutOverlay?.IsVisible == true;

    /// <summary>
    /// Flyout ecosystem manager. When set, all flyouts controlled by this control
    /// are registered for mutual exclusion — opening one auto-closes all others.
    /// Also forwarded to SubtitleOverlayCtrl and AudioTrackSelectorCtrl.
    /// </summary>
    public FlyoutManager? FlyoutManager
    {
        get => _flyoutManager;
        set
        {
            _flyoutManager = value;
            if (value == null) return;

            // Cache overlay reference FIRST
            _flyoutOverlay = MainWindow.GetOverlay(this);
            if (_flyoutOverlay != null)
                _flyoutOverlay.OnBackgroundDismissed += OnVolumeOverlayDismissed;

            // Register close actions — all hide the overlay instead of calling Flyout.Hide()
            Action hideOverlay = () => _flyoutOverlay?.HideContent();
            value.Register("equalizer",   hideOverlay);
            value.Register("volume",      hideOverlay);
            value.Register("video-menu",  hideOverlay);
            value.Register("chapters",    hideOverlay);

            // Pass to child controls
            if (SubOverlayCtrl != null) SubOverlayCtrl.FlyoutManager = value;
            if (AudioOverlay != null) AudioOverlay.FlyoutManager = value;
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
            Cine.Core.Log.ForContext<ControlsBoxControl>().Debug("SyncPlayPauseIcon: replay mode -> Replay");
        }
        else
        {
            Cine.Core.Log.ForContext<ControlsBoxControl>().Debug("SyncPlayPauseIcon: isPlaying={IsPlaying}", isPlaying);
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
        Cine.Core.Log.ForContext<ControlsBoxControl>().Debug("SetReplayMode({ReplayMode})", replayMode);

    }

    public void RefreshVolumeIcon()
    {
        if (_viewModel == null) return;
        var vol = _viewModel.VolumeValue;
        var muted = _viewModel.IsMuted && vol <= 0;
        if (muted)
        {
            VolumeIcon.Opacity = 0;
            VolumeMuteIcon.Opacity = 1;
        }
        else
        {
            VolumeIcon.Opacity = 1;
            VolumeMuteIcon.Opacity = 0;
        }
        // MuteToggleIcon no longer exists (removed from XAML)
    }

    public void SetControlsVisibility(bool visible)
    {
        ControlsBox.IsVisible = visible;
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

    public void UpdateResponsiveLayout(double width, bool hasMultipleVideoTracks)
    {
        bool isNarrow = width < UiConstants.BreakpointNarrow;
        if (isNarrow)
        {
            // Single track — show button but disabled
            SetVis(BtnVideoMenu, true);
            BtnVideoMenu.IsEnabled = false;
            BtnVideoMenu.Opacity = 0.4;
            ToolTip.SetTip(BtnVideoMenu, "Single video track — no switching available");
            SetFont(SeekBar.PositionTimeLabel, Token.Size("font-size-caption"));
            SetFont(SeekBar.DurationTimeLabel, Token.Size("font-size-caption"));
        }
        else
        {
            // Multiple tracks check — enable or disable appropriately
            SetVis(BtnVideoMenu, true);
            if (hasMultipleVideoTracks)
            {
                BtnVideoMenu.IsEnabled = true;
                BtnVideoMenu.Opacity = 1;
                ToolTip.SetTip(BtnVideoMenu, "Switch video track");
            }
            else
            {
                BtnVideoMenu.IsEnabled = false;
                BtnVideoMenu.Opacity = 0.4;
                ToolTip.SetTip(BtnVideoMenu, "Single video track — no switching available");
            }
            SetFont(SeekBar.PositionTimeLabel, 13);
            SetFont(SeekBar.DurationTimeLabel, 13);
        }
    }

    // --- Transport handlers ---

    private void OnPlayPause(object? sender, RoutedEventArgs e)
    {
        if (_viewModel == null) return;
        Cine.Core.Log.ForContext<ControlsBoxControl>().Debug("OnPlayPause CLICKED. _replayMode={ReplayMode}", _replayMode);
        // If in replay mode, clicking play restarts the video from beginning
        if (_replayMode)
        {
            _replayMode = false;
            _viewModel.PlayPause(); // ViewModel handles stop+seek+play internally
            Cine.Core.Log.ForContext<ControlsBoxControl>().Debug("OnPlayPause replay mode: _viewModel.PlayPause() called");
            return;
        }
        // Do NOT optimistically toggle the icon — let PlaybackStateManager's StateChanged
        // event update the icon through SyncPlayPauseIcon. This avoids the double-press
        // bug where the icon shows Pause before mpv has actually started playing.
        Cine.Core.Log.ForContext<ControlsBoxControl>().Debug("OnPlayPause: _viewModel.PlayPause() called");
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

    private void OnVolumeButtonScroll(object? sender, PointerWheelEventArgs e)
    {
        if (_viewModel == null) return;
        if (e.Delta.Y > 0)
            _viewModel.IncreaseVolume();
        else if (e.Delta.Y < 0)
            _viewModel.DecreaseVolume();
        e.Handled = true;
    }

    /// <summary>Builds the volume controls overlay content.</summary>
    private Border BuildVolumeContent()
    {
        var stack = new StackPanel
        {
            Width = 200,
            HorizontalAlignment = global::Avalonia.Layout.HorizontalAlignment.Center,
            Spacing = 12 // Matches HeaderBar flyout spacing
        };

        // Volume label row
        var labelRow = new StackPanel
        {
            Orientation = global::Avalonia.Layout.Orientation.Horizontal,
            Margin = new Thickness(12, 0),
            Spacing = 12
        };

        labelRow.Children.Add(new TextBlock
        {
            Text = "Volume",
            Classes = { "md3-subtitle1" },
            VerticalAlignment = global::Avalonia.Layout.VerticalAlignment.Center
        });

        var volumePercentLabel = new TextBlock
        {
            Name = "VolumePercentLabel",
            Text = "100%",
            Classes = { "md3-body2" },
            FontWeight = global::Avalonia.Media.FontWeight.SemiBold,
            VerticalAlignment = global::Avalonia.Layout.VerticalAlignment.Center
        };
        labelRow.Children.Add(volumePercentLabel);
        stack.Children.Add(labelRow);

        // Slider
        var slider = new Slider
        {
            Classes = { "compact" },
            Minimum = 0,
            Maximum = 150,
            Height = 36,
            VerticalAlignment = global::Avalonia.Layout.VerticalAlignment.Center,
            HorizontalAlignment = global::Avalonia.Layout.HorizontalAlignment.Stretch
        };
        stack.Children.Add(slider);

        // Presets row - matching TrackFlyoutBuilder pattern
        var presetsRow = new StackPanel
        {
            Orientation = global::Avalonia.Layout.Orientation.Horizontal,
            HorizontalAlignment = global::Avalonia.Layout.HorizontalAlignment.Center,
            Spacing = 8 // Matches TrackFlyoutBuilder spacing
        };

        foreach (var value in new[] { 25, 50, 75, 100 })
        {
            var btn = new Button
            {
                Content = $"{value}%",
                HorizontalAlignment = global::Avalonia.Layout.HorizontalAlignment.Center,
                Background = global::Avalonia.Media.Brushes.Transparent,
                Foreground = global::Avalonia.Application.Current?.FindResource("TextPrimary") as global::Avalonia.Media.IBrush,
                Padding = new Thickness(12, 9), // Matches flyout-item Padding="12,9"
                FontSize = 13, // Matches flyout-item FontSize="13"
                FontWeight = global::Avalonia.Media.FontWeight.Medium, // Matches flyout-item FontWeight="Medium"
                Classes = { "flyout-item" },
                Cursor = new global::Avalonia.Input.Cursor(global::Avalonia.Input.StandardCursorType.Arrow)
            };
            btn.Click += (_, __) => 
            {
                if (_viewModel != null)
                    _viewModel.VolumeValue = value;
            };
            presetsRow.Children.Add(btn);
        }
        stack.Children.Add(presetsRow);

        // Wrap stack in Border with flyout styling
        var border = new Border
        {
            Background = global::Avalonia.Application.Current?.FindResource("PopoverBackground") as global::Avalonia.Media.Brush,
            BorderBrush = global::Avalonia.Application.Current?.FindResource("PopoverBorder") as global::Avalonia.Media.Brush,
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(6), // radius-sm = 6
            Padding = new Thickness(0), // Let StackPanel handle spacing
            Child = stack
        };

        return border;
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

    public void OpenEqualizerFlyout()
    {
        if (_viewModel == null) return;
        _flyoutOverlay ??= MainWindow.GetOverlay(this);
        if (_flyoutOverlay == null) return;

        // Dismiss any previous flyout
        _flyoutManager?.DismissOthers("equalizer");

        // Build content
        var flyoutContent = new AudioEqualizerFlyout(_viewModel.Audio);
        flyoutContent.CloseAction = () =>
        {
            _flyoutOverlay?.HideContent();
            _flyoutManager?.MarkClosed("equalizer");
        };

        // Track current key for dismiss
        _activeFlyoutKey = "equalizer";
        _flyoutOverlay.OnBackgroundDismissed -= OnOverlayDismissed;
        _flyoutOverlay.OnBackgroundDismissed += OnOverlayDismissed;

        // Choose anchor: if BtnEqualizer exists use it, else fall back to BtnFullscreen
        var anchor = BtnEqualizer ?? BtnFullscreen;
        if (anchor != null)
            _flyoutOverlay.ShowContent(anchor, flyoutContent, placeAbove: true);
    }

    private void OnOverlayDismissed()
    {
        if (_activeFlyoutKey != null)
            _flyoutManager?.MarkClosed(_activeFlyoutKey);
        _activeFlyoutKey = null;
    }

    private void OnEqualizerClick(object? sender, RoutedEventArgs e)
    {
        OpenEqualizerFlyout();
    }

    private void OnVideoMenuClick(object? sender, RoutedEventArgs e)
    {
        if (_viewModel == null) return;
        _flyoutOverlay ??= MainWindow.GetOverlay(this);
        if (_flyoutOverlay == null) return;

        var content = BuildTrackMenuContent(_viewModel.VideoTracks);
        _flyoutManager?.DismissOthers("video-menu");
        _activeFlyoutKey = "video-menu";
        _flyoutOverlay.OnBackgroundDismissed -= OnOverlayDismissed;
        _flyoutOverlay.OnBackgroundDismissed += OnOverlayDismissed;
        _flyoutOverlay.ShowContent(BtnVideoMenu, content, placeAbove: true);
    }

    // --- Chapter menu handler ---

    private void OnChaptersMenuClick(object? sender, RoutedEventArgs e)
    {
        if (_viewModel == null || _viewModel.Chapters.Count == 0) return;
        _flyoutOverlay ??= MainWindow.GetOverlay(this);
        if (_flyoutOverlay == null) return;

        var content = BuildChaptersContent(_viewModel.Chapters);
        _flyoutManager?.DismissOthers("chapters");
        _activeFlyoutKey = "chapters";
        _flyoutOverlay.OnBackgroundDismissed -= OnOverlayDismissed;
        _flyoutOverlay.OnBackgroundDismissed += OnOverlayDismissed;
        _flyoutOverlay.ShowContent(BtnChaptersMenu, content, placeAbove: true);
    }

    private Control BuildChaptersContent(ObservableCollection<ChapterInfo> chapters)
    {
        var stack = new global::Avalonia.Controls.StackPanel();

        foreach (var chapter in chapters)
        {
            var timeStr = TimeSpan.FromSeconds(chapter.Time).TotalHours >= 1
                ? TimeSpan.FromSeconds(chapter.Time).ToString(@"h\:mm\:ss")
                : TimeSpan.FromSeconds(chapter.Time).ToString(@"mm\:ss");

            var seekTime = chapter.Time;
            var grid = new Grid
            {
                ColumnDefinitions = new ColumnDefinitions
                {
                    new ColumnDefinition(GridLength.Star),
                    new ColumnDefinition(GridLength.Auto)
                }
            };
            var left = new TextBlock
            {
                Text = chapter.Title,
                FontSize = Token.Size("font-size-body2"),
                Foreground = (IBrush?)global::Avalonia.Application.Current?.FindResource("OsdForeground"),
                VerticalAlignment = global::Avalonia.Layout.VerticalAlignment.Center,
                Padding = new Thickness(12, 8, 4, 8)
            };
            var right = new TextBlock
            {
                Text = timeStr,
                FontSize = Token.Size("font-size-caption"),
                Foreground = AppColors.TextOnDarkHint,
                VerticalAlignment = global::Avalonia.Layout.VerticalAlignment.Center,
                Padding = new Thickness(4, 8, 12, 8)
            };
            Grid.SetColumn(left, 0);
            Grid.SetColumn(right, 1);
            grid.Children.Add(left);
            grid.Children.Add(right);

            var btn = new global::Avalonia.Controls.Button
            {
                Content = grid,
                Background = AppColors.Transparent,
                BorderThickness = new Thickness(0),
                HorizontalContentAlignment = global::Avalonia.Layout.HorizontalAlignment.Stretch,
                Cursor = new Cursor(StandardCursorType.Arrow)
            };
            btn.Click += (_, _) =>
            {
                _viewModel?.SeekTo(seekTime / _viewModel.Duration.TotalSeconds);
            };
            stack.Children.Add(btn);
        }

        var border = new Border
        {
            Background = (IBrush?)global::Avalonia.Application.Current?.FindResource("PopoverBackground"),
            BorderBrush = (IBrush?)global::Avalonia.Application.Current?.FindResource("PopoverBorder"),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(8),
            Padding = new Thickness(4),
            MinWidth = 220,
            Child = new ScrollViewer
            {
                Content = stack,
                MaxHeight = 300,
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto
            }
        };

        return border;
    }

    private Control BuildTrackMenuContent(ObservableCollection<TrackMenuItem> tracks)
    {
        var stackPanel = new global::Avalonia.Controls.StackPanel();

        // Safety: if no real tracks, show fallback message
        var hasRealTracks = tracks.Any(t => !t.IsPseudoEntry);
        if (!hasRealTracks && tracks.Count == 0)
        {
            var text = new TextBlock
            {
                Text = "No tracks available",
                FontSize = Token.Size("font-size-body1"),
                Foreground = AppColors.TextTertiary,
                Padding = new Thickness(12, 8)
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
                FontSize = Token.Size("font-size-body1"),
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
                Padding = new Thickness(12, 8),
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

        return border;
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

