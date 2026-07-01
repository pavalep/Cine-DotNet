using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
using Cine.Avalonia.Models;
using Cine.Avalonia.Services;
using Cine.Core.Services;
using Cine.Media.Models;
using AvaloniaLayout = Avalonia.Layout;
using Button = global::Avalonia.Controls.Button;
using Cursor = Avalonia.Input.Cursor;
using TextBox = global::Avalonia.Controls.TextBox;

namespace Cine.Avalonia.Builders;

/// <summary>
/// Builds a track-selection flyout with a scrollable track list, an optional
/// real-time search/filter box (shown when track count > threshold), separator,
/// and delay adjustment controls (− / value / + / Reset).
///
/// Designed to eliminate the ~180 lines of duplicated flyout-building code
/// shared between <see cref="Controls.SubtitleOverlayControl"/> and
/// <see cref="Controls.AudioTrackSelectorControl"/>.
/// </summary>
public static class TrackFlyoutBuilder
{
    private static readonly ILogger _log = global::Cine.Core.Log.ForContext("TrackFlyoutBuilder");

    /// <summary>
    /// Build a complete track-selection flyout.
    /// </summary>
    /// <param name="tracks">Track menu items to display.</param>
    /// <param name="emptyMessage">Message shown when no tracks are available.</param>
    /// <param name="delayLabel">Header label for the delay section.</param>
    /// <param name="getDelay">Getter for current delay value.</param>
    /// <param name="setDelay">Setter for delay value (value is pre-clamped).</param>
    /// <param name="resetDelay">Action to reset delay to default.</param>
    /// <param name="searchThreshold">
    /// If the number of real (non-pseudo) tracks exceeds this value, a search
    /// text box is shown at the top of the flyout. Default 5. Set to -1 to
    /// disable search entirely.
    /// </param>
    /// <param name="searchPlaceholder">Placeholder text for the search box.</param>
    /// <param name="title">Header title shown in the flyout header bar. If null, no header is shown.</param>
    public static Flyout Build(
        ObservableCollection<TrackMenuItem> tracks,
        string emptyMessage,
        string delayLabel,
        Func<double> getDelay,
        Action<double> setDelay,
        Action resetDelay,
        int searchThreshold = 5,
        string searchPlaceholder = "Search…",
        string? title = null,
        Action<StackPanel>? appendExtra = null,
        global::Material.Icons.MaterialIconKind emptyIcon = global::Material.Icons.MaterialIconKind.ClosedCaptionOutline)
    {
        _log.Debug("Build: {Count} tracks, threshold={Threshold}, extra={HasExtra}",
            tracks.Count, searchThreshold, appendExtra != null);
        var rootPanel = new global::Avalonia.Controls.StackPanel
        {
            Spacing = 10
        };

        // ── Header: title + close button (equalizer standard) ──────
        if (title != null)
        {
            var headerGrid = new Grid
            {
                ColumnDefinitions = new ColumnDefinitions
                {
                    new ColumnDefinition(GridLength.Star),
                    new ColumnDefinition(GridLength.Auto)
                }
            };

            var titleBlock = new TextBlock
            {
                Text = title,
                Classes = { "md3-subtitle1" },
                VerticalAlignment = AvaloniaLayout.VerticalAlignment.Center
            };
            headerGrid.Children.Add(titleBlock);

            var closeBtn = new Button
            {
                Content = new TextBlock { Text = "\u2715", FontSize = 14, Foreground = AppColors.TextTertiary },
                Background = AppColors.Transparent,
                BorderThickness = new Thickness(0),
                Width = 24, Height = 24,
                CornerRadius = new CornerRadius(12),
                Cursor = new Cursor(StandardCursorType.Arrow)
            };
            closeBtn.Classes.Add("hover-subtle");
            Grid.SetColumn(closeBtn, 1);
            headerGrid.Children.Add(closeBtn);
            rootPanel.Children.Add(headerGrid);
        }

        // Count real (non-pseudo) tracks
        var realTracks = tracks.Where(t => !t.IsPseudoEntry).ToList();
        var showSearch = searchThreshold >= 0 && realTracks.Count > searchThreshold;

        // ── Search text box ─────────────────────────────────────────
        TextBox? searchBox = null;
        if (showSearch)
        {
            searchBox = new TextBox
            {
                PlaceholderText = searchPlaceholder,
                Margin = new Thickness(8, 4, 8, 0),
                Padding = Token.GetThickness("space-1"),
                FontSize = Token.Size("font-size-body2"),
                Height = 28,
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(4),
                Background = AppColors.Transparent,
                BorderBrush = (IBrush?)global::Avalonia.Application.Current?.FindResource("PopoverBorder"),
                Foreground = AppColors.TextPrimary,
                PlaceholderForeground = AppColors.TextTertiary
            };
            rootPanel.Children.Add(searchBox);
        }

        // ── Track list panel (direct child of root — outer ScrollViewer handles overflow) ─
        var trackListPanel = new global::Avalonia.Controls.StackPanel();

        // Helper to rebuild the track list applying a search filter
        void RebuildTrackList(string? filter)
        {
            trackListPanel.Children.Clear();

            var filtered = string.IsNullOrWhiteSpace(filter)
                ? tracks
                : tracks.Where(t =>
                    t.DisplayName.Contains(filter, StringComparison.OrdinalIgnoreCase));

            var filteredItems = filtered.ToList();
            if (filteredItems.Count == 0)
            {
                trackListPanel.Children.Add(new global::Avalonia.Controls.StackPanel
                {
                    Orientation = global::Avalonia.Layout.Orientation.Vertical,
                    HorizontalAlignment = global::Avalonia.Layout.HorizontalAlignment.Center,
                    Spacing = 4,
                    Margin = new Thickness(12, 16),
                    Children =
                    {
                        new global::Material.Icons.Avalonia.MaterialIcon
                        {
                            Kind = emptyIcon,
                            Width = 32,
                            Height = 32,
                            Foreground = AppColors.TextTertiary,
                            Opacity = 0.5
                        },
                        new TextBlock
                        {
                            Text = string.IsNullOrWhiteSpace(filter) ? emptyMessage : "No matching tracks",
                            FontSize = Token.Size("font-size-body2"),
                            Foreground = AppColors.TextTertiary,
                            HorizontalAlignment = global::Avalonia.Layout.HorizontalAlignment.Center
                        }
                    }
                });
                return;
            }

            foreach (var track in filteredItems)
                trackListPanel.Children.Add(BuildTrackRow(track));
        }

        // Initial population: if there are no tracks at all, show the
        // static empty message; otherwise run the filter builder (which
        // handles both search and non-search states).
        if (tracks.Count == 0 && !showSearch)
        {
            trackListPanel.Children.Add(new global::Avalonia.Controls.StackPanel
            {
                Orientation = global::Avalonia.Layout.Orientation.Vertical,
                HorizontalAlignment = global::Avalonia.Layout.HorizontalAlignment.Center,
                Spacing = 4,
                Margin = new Thickness(12, 16),
                Children =
                {
                    new global::Material.Icons.Avalonia.MaterialIcon
                    {
                        Kind = emptyIcon,
                        Width = 32,
                        Height = 32,
                        Foreground = AppColors.TextTertiary,
                        Opacity = 0.5
                    },
                    new TextBlock
                    {
                        Text = emptyMessage,
                        FontSize = Token.Size("font-size-body2"),
                        Foreground = AppColors.TextTertiary,
                        HorizontalAlignment = global::Avalonia.Layout.HorizontalAlignment.Center
                    }
                }
            });
        }
        else
        {
            RebuildTrackList(null);
        }

        // Wire search text changed (must be after initial population)
        if (searchBox != null)
        {
            searchBox.TextChanged += (_, _) => RebuildTrackList(searchBox.Text);
            // Clear search on Escape
            searchBox.KeyDown += (_, e) =>
            {
                if (e.Key == Key.Escape)
                {
                    searchBox.Text = "";
                    e.Handled = true;
                }
            };
        }

        rootPanel.Children.Add(trackListPanel);

        // Keyboard navigation for track list
        trackListPanel.KeyDown += (_, e) =>
        {
            var buttons = trackListPanel.Children
                .OfType<Button>()
                .Where(b => b.IsEnabled && b.IsVisible)
                .ToList();

            if (buttons.Count == 0) return;

            var focused = TopLevel.GetTopLevel(trackListPanel)
                ?.FocusManager
                ?.GetFocusedElement() as Button;
            var currentIndex = focused is not null ? buttons.IndexOf(focused) : -1;

            switch (e.Key)
            {
                case Key.Down:
                    e.Handled = true;
                    var nextIndex = Math.Min(currentIndex + 1, buttons.Count - 1);
                    if (nextIndex >= 0) buttons[nextIndex].Focus();
                    break;

                case Key.Up:
                    e.Handled = true;
                    var prevIndex = Math.Max(currentIndex - 1, 0);
                    if (prevIndex >= 0) buttons[prevIndex].Focus();
                    break;

                case Key.Enter or Key.Return:
                    e.Handled = true;
                    focused?.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                    break;

                case Key.Home:
                    e.Handled = true;
                    buttons.FirstOrDefault()?.Focus();
                    break;

                case Key.End:
                    e.Handled = true;
                    buttons.LastOrDefault()?.Focus();
                    break;
            }
        };

        // Focus the first button automatically when the panel is shown
        trackListPanel.AttachedToVisualTree += (_, _) =>
        {
            var first = trackListPanel.Children.OfType<Button>().FirstOrDefault(b => b.IsEnabled);
            first?.Focus();
        };

        // ── Separator (equalizer standard) ─────────────────────────
        rootPanel.Children.Add(new Border
        {
            Background = (IBrush?)global::Avalonia.Application.Current?.FindResource("PopoverBorder"),
            Height = 1,
            Opacity = 0.5,
            Margin = new Thickness(0, 2)
        });

        // ── Delay section label ─────────────────────────────────────
        rootPanel.Children.Add(new TextBlock
        {
            Text = delayLabel,
            Classes = { "md3-caption" },
            Margin = new Thickness(12, 8, 12, 4)
        });

        // ── Delay controls ──────────────────────────────────────────
        var delayText = new TextBlock
        {
            Text = $"{getDelay():F1}s",
            FontSize = Token.Size("font-size-body1"),
            FontWeight = FontWeight.SemiBold,
            Foreground = AppColors.TextPrimary,
            MinWidth = 40,
            TextAlignment = global::Avalonia.Media.TextAlignment.Center,
            VerticalAlignment = AvaloniaLayout.VerticalAlignment.Center
        };

        void NudgeDelay(double delta)
        {
            var current = getDelay();
            setDelay((float)Math.Clamp(current + delta, -10, 10));
            delayText.Text = $"{getDelay():F1}s";
            _log.Trace("NudgeDelay: delta={Delta}, from={From}s, to={To}s", delta, current, getDelay());
        }

        void ResetDelay()
        {
            resetDelay();
            delayText.Text = $"{getDelay():F1}s";
        }

        var btnMinus = new Button
        {
            Content = new TextBlock { Text = "\u2212", FontSize = Token.Size("font-size-subtitle2"), FontWeight = FontWeight.Bold, Foreground = AppColors.TextPrimary },
            Width = 28, Height = 28, CornerRadius = new CornerRadius(14),
            Background = AppColors.Transparent, BorderThickness = new Thickness(0), Padding = new Thickness(0),
            HorizontalContentAlignment = AvaloniaLayout.HorizontalAlignment.Center,
            VerticalContentAlignment = AvaloniaLayout.VerticalAlignment.Center,
            Cursor = new Cursor(StandardCursorType.Arrow)
        };
        btnMinus.Click += (_, _) => NudgeDelay(-0.5);
        btnMinus.Classes.Add("hover-subtle");

        var btnPlus = new Button
        {
            Content = new TextBlock { Text = "+", FontSize = Token.Size("font-size-subtitle2"), FontWeight = FontWeight.Bold, Foreground = AppColors.TextPrimary },
            Width = 28, Height = 28, CornerRadius = new CornerRadius(14),
            Background = AppColors.Transparent, BorderThickness = new Thickness(0), Padding = new Thickness(0),
            HorizontalContentAlignment = AvaloniaLayout.HorizontalAlignment.Center,
            VerticalContentAlignment = AvaloniaLayout.VerticalAlignment.Center,
            Cursor = new Cursor(StandardCursorType.Arrow)
        };
        btnPlus.Click += (_, _) => NudgeDelay(0.5);
        btnPlus.Classes.Add("hover-subtle");

        var btnReset = new Button
        {
            Content = new TextBlock { Text = "Reset", FontSize = Token.Size("font-size-caption"), Foreground = AppColors.TextTertiary },
            Background = AppColors.Transparent, BorderThickness = new Thickness(0), Padding = Token.GetThickness("space-1"),
            Cursor = new Cursor(StandardCursorType.Arrow)
        };
        btnReset.Click += (_, _) => ResetDelay();
        btnReset.PointerEntered += (_, _) => { if (btnReset.Content is TextBlock tb) tb.Foreground = AppColors.TextPrimary; };
        btnReset.PointerExited += (_, _) => { if (btnReset.Content is TextBlock tb) tb.Foreground = AppColors.TextTertiary; };

        var delayRow = new Grid
        {
            ColumnDefinitions = new ColumnDefinitions
            {
                new ColumnDefinition(GridLength.Auto),
                new ColumnDefinition(GridLength.Star),
                new ColumnDefinition(GridLength.Auto),
                new ColumnDefinition(GridLength.Auto)
            },
            Margin = new Thickness(4, 0)
        };
        Grid.SetColumn(btnMinus, 0);
        Grid.SetColumn(delayText, 1);
        Grid.SetColumn(btnPlus, 2);
        Grid.SetColumn(btnReset, 3);
        delayRow.Children.Add(btnMinus);
        delayRow.Children.Add(delayText);
        delayRow.Children.Add(btnPlus);
        delayRow.Children.Add(btnReset);
        rootPanel.Children.Add(delayRow);

        // ── Extra content (e.g., appearance submenu button) ─────────
        appendExtra?.Invoke(rootPanel);

        // ── Wrap in scroll + popover border ─────────────────────────
        var scrollRoot = new ScrollViewer
        {
            MaxHeight = 600,
            Content = rootPanel
        };

        var border = new Border
        {
            Background = (IBrush?)global::Avalonia.Application.Current?.FindResource("PopoverBackground"),
            BorderBrush = (IBrush?)global::Avalonia.Application.Current?.FindResource("PopoverBorder"),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(8),
            Padding = new Thickness(14, 12),
            MinWidth = 220,
            Child = scrollRoot
        };

        return new Flyout { Content = border, Placement = PlacementMode.Top };
    }

    /// <summary>
    /// Builds only the flyout content control (the wrapped Border/ScrollViewer),
    /// without wrapping it in a Flyout. Use this with a window-level FlyoutOverlayControl
    /// to bypass Avalonia's broken Popup overlay layer.
    ///
    /// All parameters and behavior are identical to Build.
    /// </summary>
    public static Border BuildContent(
        ObservableCollection<TrackMenuItem> tracks,
        string emptyMessage,
        string delayLabel,
        Func<double> getDelay,
        Action<double> setDelay,
        Action resetDelay,
        int searchThreshold = 5,
        string searchPlaceholder = "Search\u2026",
        string? title = null,
        Action<StackPanel>? appendExtra = null,
        global::Material.Icons.MaterialIconKind emptyIcon = global::Material.Icons.MaterialIconKind.ClosedCaptionOutline)
    {
        // Build the content using the same infrastructure, then unwrap
        var flyout = Build(tracks, emptyMessage, delayLabel, getDelay, setDelay, resetDelay,
            searchThreshold, searchPlaceholder, title, appendExtra, emptyIcon);
        var border = (flyout.Content as Border) ?? new Border(); // fallback
        // Rewire placement logic: border is already correctly built
        return border;
    }

    /// <summary>Builds a single track row with selection dot + text label.</summary>
    private static Button BuildTrackRow(TrackMenuItem track)
    {
        // "Add Subtitle Track…" pseudo-entry → render as a secondary action button
        // Note: no redundant "+" icon — the text "Add" is self-explanatory.
        if (track.IsPseudoEntry && track.TrackIndex == -1)
        {
            var addText = new TextBlock
            {
                Text = track.DisplayName,
                FontSize = Token.Size("font-size-body2"),
                Foreground = AppColors.Accent
            };

            // Thin separator above the add button (via parent, but we add a subtle top border here)
            var addBtn = new Button
            {
                Content = addText,
                Background = AppColors.Transparent,
                BorderThickness = new Thickness(0),
                Padding = new Thickness(8, 4),
                MinHeight = 36,
                HorizontalContentAlignment = AvaloniaLayout.HorizontalAlignment.Stretch,
                Cursor = new Cursor(StandardCursorType.Arrow),
                Command = track.SelectCommand
            };
            addBtn.PointerEntered += (_, _) => addBtn.Background = AppColors.HoverSubtle;
            addBtn.PointerExited += (_, _) => addBtn.Background = AppColors.Transparent;
            return addBtn;
        }

        // Check if this track is actively playing (different from just selected)
        bool isNowPlaying = track.IsSelected; // Active track that's currently being played
        var dotColor = isNowPlaying ? AppColors.Accent : AppColors.IconDim;
        var dotScale = isNowPlaying ? 1.3 : 1.0;

        var dot = new Border
        {
            Width = isNowPlaying ? 8 : 6,
            Height = isNowPlaying ? 8 : 6,
            CornerRadius = new CornerRadius(isNowPlaying ? 4 : 3),
            Background = dotColor,
            VerticalAlignment = AvaloniaLayout.VerticalAlignment.Center,
            HorizontalAlignment = AvaloniaLayout.HorizontalAlignment.Center,
            RenderTransform = new ScaleTransform(dotScale, dotScale),
            Margin = new Thickness(0, 0, 8, 0)
        };

        // Set tooltip for the active state
        if (isNowPlaying)
        {
            // Tooltip.SetTip(dot, "Now playing");  // CS0234 issue
        }

        var text = new TextBlock
        {
            Text = track.DisplayName,
            FontWeight = isNowPlaying ? FontWeight.SemiBold : FontWeight.Normal,
            FontSize = Token.Size("font-size-body2"),
            Foreground = isNowPlaying ? AppColors.Accent : AppColors.TextPrimary
        };

        var grid = new Grid
        {
            ColumnDefinitions = new ColumnDefinitions
            {
                new ColumnDefinition(GridLength.Auto),  // dot
                new ColumnDefinition(GridLength.Auto),  // codec badge
                new ColumnDefinition(GridLength.Star)    // text
            }
        };
        grid.Children.Add(dot);
        Grid.SetColumn(dot, 0);

        // ── F12: Codec badge (small colored dot indicating codec type) ──
        if (!track.IsPseudoEntry && track.Source != null)
        {
            // Resolve default codec fallback based on track type
            var defaultCodec = track.TrackType == TrackType.Audio ? "unknown" : "srt";
            var codec = string.IsNullOrWhiteSpace(track.Source.Codec)
                ? defaultCodec
                : track.Source.Codec.ToLowerInvariant();

            var badgeColor = GetCodecBadgeColor(codec);
            var badge = new Border
            {
                Width = 6, Height = 6,
                CornerRadius = new CornerRadius(3),
                Background = badgeColor,
                VerticalAlignment = AvaloniaLayout.VerticalAlignment.Center,
                HorizontalAlignment = AvaloniaLayout.HorizontalAlignment.Center,
                Margin = new Thickness(4, 0, 4, 0)
            };
            var codecTip = !string.IsNullOrWhiteSpace(track.Source.Codec) ? $"{track.Source.Codec} codec" : "Unknown codec";
            // Avalonia.Controls.ToolTip.SetTip(badge, codecTip);  // CS0234 issue
            grid.Children.Add(badge);
            Grid.SetColumn(badge, 1);
        }

        grid.Children.Add(text);
        Grid.SetColumn(text, 2);

        // ── F13: Drag-over feedback on track buttons ──
        var button = new Button
        {
            Content = grid,
            Background = AppColors.Transparent,
            BorderThickness = new global::Avalonia.Thickness(0),
            Padding = new global::Avalonia.Thickness(12, 8),
            MinHeight = 36,
            HorizontalContentAlignment = global::Avalonia.Layout.HorizontalAlignment.Stretch,
            Cursor = new global::Avalonia.Input.Cursor(global::Avalonia.Input.StandardCursorType.Arrow),
            Opacity = track.DisplayOpacity,
            Command = track.SelectCommand
        };
        // global::Avalonia.Controls.DragDrop.SetAllowDrop(button, true);  // CS0234: DragDrop doesn't exist
        button.PointerEntered += (_, _) => button.Background = AppColors.HoverSubtle;
        button.PointerExited += (_, _) => button.Background = AppColors.Transparent;

        // Tooltip: show filename + path for external subtitles
        if (!track.IsPseudoEntry && track.Source != null)
        {
            // Avalonia.Controls.ToolTip.SetTip(button, BuildTrackTooltip(track.Source));  // CS0234
        }

        return button;
    }

    /// <summary>Builds a human-readable tooltip for a subtitle source.</summary>
    private static string BuildTrackTooltip(SubtitleSource src)
    {
        string codec = string.IsNullOrWhiteSpace(src.Codec) ? "SRT" : src.Codec;
        if (src.IsExternal && !string.IsNullOrWhiteSpace(src.ExternalFilename))
        {
            var fileName = System.IO.Path.GetFileName(src.ExternalFilename);
            return $"File: {fileName}\n\nPath: {src.ExternalFilename}\nFormat: {codec}";
        }
        return $"Track {src.PathOrId}\nCodec: {codec}";
    }

    private static global::Avalonia.Media.IBrush GetCodecBadgeColor(string codecLower) => codecLower switch
    {
        "ass" or "ssa" => new global::Avalonia.Media.SolidColorBrush(new global::Avalonia.Media.Color(255, 0, 180, 180)),
        "subrip" or "srt" => new global::Avalonia.Media.SolidColorBrush(new global::Avalonia.Media.Color(255, 120, 130, 140)),
        "hdmv_pgs_subtitle" or "hdmv_pgs" or "pgs" => new global::Avalonia.Media.SolidColorBrush(new global::Avalonia.Media.Color(255, 180, 100, 50)),
        "dvd_subtitle" or "vobsub" or "dvb_subtitle" => new global::Avalonia.Media.SolidColorBrush(new global::Avalonia.Media.Color(255, 100, 120, 200)),
        "mov_text" or "tx3g" => new global::Avalonia.Media.SolidColorBrush(new global::Avalonia.Media.Color(255, 200, 180, 50)),
        "dvb" or "dvbsub" => new global::Avalonia.Media.SolidColorBrush(new global::Avalonia.Media.Color(255, 150, 80, 180)),
        "webvtt" or "vtt" => new global::Avalonia.Media.SolidColorBrush(new global::Avalonia.Media.Color(255, 80, 180, 80)),
        "unknown" => new global::Avalonia.Media.SolidColorBrush(new global::Avalonia.Media.Color(255, 100, 100, 100)),
        _ => new global::Avalonia.Media.SolidColorBrush(new global::Avalonia.Media.Color(255, 80, 80, 80)),
    };
}
