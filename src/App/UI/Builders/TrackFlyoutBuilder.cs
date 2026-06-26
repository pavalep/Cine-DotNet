using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using Cine.Avalonia.Models;
using Cine.Avalonia.Services;
using Cine.Core.Services;
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
    public static Flyout Build(
        ObservableCollection<TrackMenuItem> tracks,
        string emptyMessage,
        string delayLabel,
        Func<double> getDelay,
        Action<double> setDelay,
        Action resetDelay,
        int searchThreshold = 5,
        string searchPlaceholder = "Search…",
        Action<StackPanel>? appendExtra = null,
        global::Material.Icons.MaterialIconKind emptyIcon = global::Material.Icons.MaterialIconKind.ClosedCaptionOutline)
    {
        _log.Debug("Build: {Count} tracks, threshold={Threshold}, extra={HasExtra}",
            tracks.Count, searchThreshold, appendExtra != null);
        var rootPanel = new global::Avalonia.Controls.StackPanel();

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

        // ── Separator ──────────────────────────────────────────────
        rootPanel.Children.Add(new Separator
        {
            Background = (IBrush?)global::Avalonia.Application.Current?.FindResource("PopoverBorder"),
            Margin = new Thickness(8, 4)
        });

        // ── Delay section label ─────────────────────────────────────
        rootPanel.Children.Add(new TextBlock
        {
            Text = delayLabel,
            FontSize = Token.Size("font-size-caption"),
            FontWeight = FontWeight.Bold,
            Foreground = (IBrush?)global::Avalonia.Application.Current?.FindResource("OsdForeground"),
            Opacity = 0.5,
            LetterSpacing = 0.8,
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
            Padding = new Thickness(4),
            MinWidth = 220,
            Child = scrollRoot
        };

        return new Flyout { Content = border, Placement = PlacementMode.Top };
    }

    /// <summary>Builds a single track row with selection dot + text label.</summary>
    private static Button BuildTrackRow(TrackMenuItem track)
    {
        // "Add Subtitle Track…" pseudo-entry → render as a secondary action button
        if (track.IsPseudoEntry && track.TrackIndex == -1)
        {
            var addGrid = new Grid
            {
                ColumnDefinitions = new ColumnDefinitions
                {
                    new ColumnDefinition(GridLength.Auto),
                    new ColumnDefinition(GridLength.Star)
                }
            };
            var plusIcon = new TextBlock
            {
                Text = "+",
                FontSize = Token.Size("font-size-subtitle1"),
                FontWeight = FontWeight.Bold,
                Foreground = AppColors.Accent,
                VerticalAlignment = AvaloniaLayout.VerticalAlignment.Center,
                Margin = new Thickness(0, 0, 8, 0)
            };
            var addText = new TextBlock
            {
                Text = track.DisplayName,
                FontSize = Token.Size("font-size-body2"),
                Foreground = AppColors.Accent
            };
            addGrid.Children.Add(plusIcon);
            addGrid.Children.Add(addText);
            Grid.SetColumn(addText, 1);

            // Thin separator above the add button (via parent, but we add a subtle top border here)
            var addBtn = new Button
            {
                Content = addGrid,
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
            FontSize = Token.Size("font-size-body2"),
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
            MinHeight = 36,
            HorizontalContentAlignment = AvaloniaLayout.HorizontalAlignment.Stretch,
            Cursor = new Cursor(StandardCursorType.Arrow),
            Opacity = track.DisplayOpacity,
            Command = track.SelectCommand
        };
        button.PointerEntered += (_, _) => button.Background = AppColors.HoverSubtle;
        button.PointerExited += (_, _) => button.Background = AppColors.Transparent;

        // ── Tooltip: show filename + path for external subtitles ──
        if (!track.IsPseudoEntry && track.Source != null)
        {
            var src = track.Source;
            string tip;
            if (src.IsExternal && !string.IsNullOrWhiteSpace(src.ExternalFilename))
            {
                var fileName = Path.GetFileName(src.ExternalFilename);
                var codec = string.IsNullOrWhiteSpace(src.Codec) ? "" : src.Codec;
                tip = $"{track.DisplayName}\n\n"
                    + $"File: {fileName}\n"
                    + $"Path: {src.ExternalFilename}\n"
                    + $"Format: {(string.IsNullOrWhiteSpace(codec) ? "SRT" : codec)}";
            }
            else
            {
                var codec = string.IsNullOrWhiteSpace(src.Codec) ? "" : src.Codec;
                tip = $"{track.DisplayName}\n"
                    + $"Track {track.TrackIndex}\n"
                    + (string.IsNullOrWhiteSpace(codec) ? "" : $"Codec: {codec}");
            }
            button.SetValue(global::Avalonia.Controls.ToolTip.TipProperty, tip);
        }

        return button;
    }
}
