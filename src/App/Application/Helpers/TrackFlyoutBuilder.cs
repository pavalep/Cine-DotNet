using System;
using System.Collections.ObjectModel;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using Cine.Avalonia.ViewModels;
using AvaloniaLayout = Avalonia.Layout;
using Button = global::Avalonia.Controls.Button;
using Cursor = Avalonia.Input.Cursor;
using TextBox = global::Avalonia.Controls.TextBox;

namespace Cine.Avalonia.Helpers;

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
        Func<float> getDelay,
        Action<float> setDelay,
        Action resetDelay,
        int searchThreshold = 5,
        string searchPlaceholder = "Search tracks\u2026")
    {
        var rootPanel = new global::Avalonia.Controls.StackPanel();

        // Count real (non-pseudo) tracks
        var realTracks = tracks.Where(t => !t.IsPseudoEntry).ToList();
        var pseudoTracks = tracks.Where(t => t.IsPseudoEntry).ToList();
        var showSearch = searchThreshold >= 0 && realTracks.Count > searchThreshold;

        // ── Search text box ─────────────────────────────────────────
        TextBox? searchBox = null;
        if (showSearch)
        {
            searchBox = new TextBox
            {
                PlaceholderText = searchPlaceholder,
                Margin = new Thickness(4, 4, 4, 0),
                Padding = new Thickness(8, 4),
                FontSize = 12,
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

        // ── Track list panel (inside scroll viewer) ─────────────────
        var trackListPanel = new global::Avalonia.Controls.StackPanel();
        var scrollViewer = new ScrollViewer { MaxHeight = 340, Content = trackListPanel };

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
                trackListPanel.Children.Add(new TextBlock
                {
                    Text = string.IsNullOrWhiteSpace(filter) ? emptyMessage : "No matching tracks",
                    FontSize = 12,
                    Foreground = AppColors.TextTertiary,
                    Padding = new Thickness(10, 7)
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
            trackListPanel.Children.Add(new TextBlock
            {
                Text = emptyMessage,
                FontSize = 12,
                Foreground = AppColors.TextTertiary,
                Padding = new Thickness(10, 7)
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

        rootPanel.Children.Add(scrollViewer);

        // ── Separator ──────────────────────────────────────────────
        rootPanel.Children.Add(new Separator
        {
            Background = (IBrush?)global::Avalonia.Application.Current?.FindResource("PopoverBorder"),
            Margin = new Thickness(4, 2)
        });

        // ── Delay section label ─────────────────────────────────────
        rootPanel.Children.Add(new TextBlock
        {
            Text = delayLabel,
            FontSize = 9,
            FontWeight = FontWeight.Bold,
            Foreground = (IBrush?)global::Avalonia.Application.Current?.FindResource("OsdForeground"),
            Opacity = 0.4,
            LetterSpacing = 0.8,
            Margin = new Thickness(8, 6, 8, 4)
        });

        // ── Delay controls ──────────────────────────────────────────
        var delayText = new TextBlock
        {
            Text = $"{getDelay():F1}s",
            FontSize = 13,
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
        }

        void ResetDelay()
        {
            resetDelay();
            delayText.Text = $"{getDelay():F1}s";
        }

        var btnMinus = new Button
        {
            Content = new TextBlock { Text = "\u2212", FontSize = 16, FontWeight = FontWeight.Bold, Foreground = AppColors.TextPrimary },
            Width = 28, Height = 28, CornerRadius = new CornerRadius(14),
            Background = AppColors.Transparent, BorderThickness = new Thickness(0), Padding = new Thickness(0),
            HorizontalContentAlignment = AvaloniaLayout.HorizontalAlignment.Center,
            VerticalContentAlignment = AvaloniaLayout.VerticalAlignment.Center,
            Cursor = new Cursor(StandardCursorType.Arrow)
        };
        btnMinus.Click += (_, _) => NudgeDelay(-0.5);
        btnMinus.PointerEntered += (_, _) => btnMinus.Background = AppColors.HoverSubtle;
        btnMinus.PointerExited += (_, _) => btnMinus.Background = AppColors.Transparent;

        var btnPlus = new Button
        {
            Content = new TextBlock { Text = "+", FontSize = 16, FontWeight = FontWeight.Bold, Foreground = AppColors.TextPrimary },
            Width = 28, Height = 28, CornerRadius = new CornerRadius(14),
            Background = AppColors.Transparent, BorderThickness = new Thickness(0), Padding = new Thickness(0),
            HorizontalContentAlignment = AvaloniaLayout.HorizontalAlignment.Center,
            VerticalContentAlignment = AvaloniaLayout.VerticalAlignment.Center,
            Cursor = new Cursor(StandardCursorType.Arrow)
        };
        btnPlus.Click += (_, _) => NudgeDelay(0.5);
        btnPlus.PointerEntered += (_, _) => btnPlus.Background = AppColors.HoverSubtle;
        btnPlus.PointerExited += (_, _) => btnPlus.Background = AppColors.Transparent;

        var btnReset = new Button
        {
            Content = new TextBlock { Text = "Reset", FontSize = 11, Foreground = AppColors.TextTertiary },
            Background = AppColors.Transparent, BorderThickness = new Thickness(0), Padding = new Thickness(6, 2),
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

        // ── Wrap in scroll + popover border ─────────────────────────
        var border = new Border
        {
            Background = (IBrush?)global::Avalonia.Application.Current?.FindResource("PopoverBackground"),
            BorderBrush = (IBrush?)global::Avalonia.Application.Current?.FindResource("PopoverBorder"),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(8),
            Padding = new Thickness(4),
            MinWidth = 220,
            Child = rootPanel
        };

        return new Flyout { Content = border, Placement = PlacementMode.Top };
    }

    /// <summary>Builds a single track row with selection dot + text label.</summary>
    private static Button BuildTrackRow(TrackMenuItem track)
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
            Padding = new Thickness(12, 8),
            MinHeight = 36,
            HorizontalContentAlignment = AvaloniaLayout.HorizontalAlignment.Stretch,
            Cursor = new Cursor(StandardCursorType.Arrow),
            Opacity = track.DisplayOpacity,
            Command = track.SelectCommand
        };
        button.PointerEntered += (_, _) => button.Background = AppColors.HoverSubtle;
        button.PointerExited += (_, _) => button.Background = AppColors.Transparent;
        return button;
    }
}
