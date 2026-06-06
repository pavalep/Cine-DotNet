using System;
using System.Collections.Generic;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using Material.Icons.Avalonia;
using AvaloniaLayout = Avalonia.Layout;

namespace Cine.Avalonia.Helpers;

/// <summary>
/// Builds the shared primary menu structure used by both HeaderBarControl and FullscreenHeaderControl.
/// Eliminates ~200 lines of duplicated XAML between the two headers.
/// </summary>
public class PrimaryMenuBuilder
{
    private readonly StackPanel _stack = new() { Width = 240 };
    private readonly List<(MaterialIcon CheckIcon, Func<bool> IsChecked)> _toggleItems = new();

    /// <summary>Adds a section header label (e.g. "PLAYBACK").</summary>
    public PrimaryMenuBuilder AddSection(string title)
    {
        _stack.Children.Add(new TextBlock
        {
            Text = title,
            FontSize = 10,
            FontWeight = FontWeight.Bold,
            Foreground = Res("OsdForeground"),
            Opacity = 0.4,
            LetterSpacing = 0.8,
            Margin = new Thickness(8, 6, 8, 4)
        });
        return this;
    }

    /// <summary>Adds a separator between sections.</summary>
    public PrimaryMenuBuilder AddSeparator()
    {
        _stack.Children.Add(new Separator { Background = Res("PopoverBorder") });
        return this;
    }

    /// <summary>Adds a menu item with icon, label, and optional shortcut text.</summary>
    public PrimaryMenuBuilder AddItem(string iconKind, string text, string? shortcut, Action onClick)
    {
        _stack.Children.Add(MakeItem(iconKind, text, shortcut, onClick));
        return this;
    }

    /// <summary>
    /// Adds a toggle menu item with a checkmark icon. Call <see cref="SyncCheckStates"/>
    /// before showing the flyout to update checkmark visibility.
    /// </summary>
    public PrimaryMenuBuilder AddToggleItem(string iconKind, string text, string? shortcut,
        Action onClick, Func<bool> isChecked)
    {
        var checkIcon = new MaterialIcon
        {
            Kind = Material.Icons.MaterialIconKind.Check,
            Width = 12, Height = 12,
            Foreground = Res("AppAccent"),
            Opacity = 0,
            Margin = new Thickness(0, 0, 6, 0),
            VerticalAlignment = AvaloniaLayout.VerticalAlignment.Center
        };

        var grid = new Grid
        {
            ColumnDefinitions = new ColumnDefinitions
            {
                new(GridLength.Auto),
                new(GridLength.Star),
                new(GridLength.Auto),
                new(GridLength.Auto)
            }
        };

        var icon = new MaterialIcon
        {
            Kind = (Material.Icons.MaterialIconKind)Enum.Parse(
                typeof(Material.Icons.MaterialIconKind), iconKind),
            Width = 14, Height = 14,
            Foreground = Res("OsdForeground")
        };
        Grid.SetColumn(icon, 0);

        var label = new TextBlock
        {
            Text = text,
            Margin = new Thickness(10, 0, 0, 0),
            FontSize = 13,
            FontWeight = FontWeight.Medium,
            Foreground = Res("OsdForeground"),
            VerticalAlignment = AvaloniaLayout.VerticalAlignment.Center
        };
        Grid.SetColumn(label, 1);
        Grid.SetColumn(checkIcon, 2);

        grid.Children.Add(icon);
        grid.Children.Add(label);
        grid.Children.Add(checkIcon);

        if (shortcut != null)
        {
            var sc = new TextBlock
            {
                Text = shortcut,
                FontSize = 11,
                Foreground = Res("OsdForeground"),
                Opacity = 0.3,
                VerticalAlignment = AvaloniaLayout.VerticalAlignment.Center
            };
            Grid.SetColumn(sc, 3);
            grid.Children.Add(sc);
        }

        var btn = new AvaloniaButton
        {
            Content = grid,
            Background = AppColors.Transparent,
            BorderThickness = new Thickness(0),
            Padding = new Thickness(10, 7),
            HorizontalContentAlignment = AvaloniaLayout.HorizontalAlignment.Stretch,
            Cursor = new AvaloniaCursor(StandardCursorType.Arrow)
        };
        btn.Classes.Add("flyout-item");
        btn.Click += (_, _) =>
        {
            onClick();
            checkIcon.Opacity = isChecked() ? 1 : 0;
        };
        btn.PointerEntered += (_, _) => btn.Background = AppColors.HoverSubtle;
        btn.PointerExited += (_, _) => btn.Background = AppColors.Transparent;
        _stack.Children.Add(btn);
        _toggleItems.Add((checkIcon, isChecked));
        return this;
    }

    /// <summary>Updates all toggle checkmarks to reflect current state. Call before showing flyout.</summary>
    public void SyncCheckStates()
    {
        foreach (var (icon, check) in _toggleItems)
            icon.Opacity = check() ? 1 : 0;
    }

    /// <summary>Builds the content Border (without Flyout wrapper).</summary>
    public Border BuildContent()
    {
        return new Border
        {
            Background = Res("PopoverBackground"),
            BorderBrush = Res("PopoverBorder"),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(8),
            Padding = new Thickness(6),
            MinWidth = 220,
            Child = _stack
        };
    }

    /// <summary>Builds the flyout with standard primary-menu styling.</summary>
    public Flyout Build()
    {
        return new Flyout { Content = BuildContent(), Placement = PlacementMode.Bottom };
    }

    // --- Private helpers ---

    private static IBrush? Res(string key) =>
        (IBrush?)global::Avalonia.Application.Current?.FindResource(key);

    private static AvaloniaButton MakeItem(string iconKind, string text, string? shortcut, Action onClick)
    {
        var grid = new Grid
        {
            ColumnDefinitions = new ColumnDefinitions
            {
                new(GridLength.Auto),
                new(GridLength.Star),
                new(GridLength.Auto)
            }
        };

        var icon = new MaterialIcon
        {
            Kind = (Material.Icons.MaterialIconKind)Enum.Parse(
                typeof(Material.Icons.MaterialIconKind), iconKind),
            Width = 14, Height = 14,
            Foreground = Res("OsdForeground")
        };
        Grid.SetColumn(icon, 0);

        var label = new TextBlock
        {
            Text = text,
            Margin = new Thickness(10, 0, 0, 0),
            FontSize = 13,
            FontWeight = FontWeight.Medium,
            Foreground = Res("OsdForeground"),
            VerticalAlignment = AvaloniaLayout.VerticalAlignment.Center
        };
        Grid.SetColumn(label, 1);

        grid.Children.Add(icon);
        grid.Children.Add(label);

        if (shortcut != null)
        {
            var sc = new TextBlock
            {
                Text = shortcut,
                FontSize = 11,
                Foreground = Res("OsdForeground"),
                Opacity = 0.3,
                VerticalAlignment = AvaloniaLayout.VerticalAlignment.Center
            };
            Grid.SetColumn(sc, 2);
            grid.Children.Add(sc);
        }

        var btn = new AvaloniaButton
        {
            Content = grid,
            Background = AppColors.Transparent,
            BorderThickness = new Thickness(0),
            Padding = new Thickness(10, 7),
            HorizontalContentAlignment = AvaloniaLayout.HorizontalAlignment.Stretch,
            Cursor = new AvaloniaCursor(StandardCursorType.Arrow)
        };
        btn.Classes.Add("flyout-item");
        btn.Click += (_, _) => onClick();
        btn.PointerEntered += (_, _) => btn.Background = AppColors.HoverSubtle;
        btn.PointerExited += (_, _) => btn.Background = AppColors.Transparent;
        return btn;
    }
}
