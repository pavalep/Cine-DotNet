using System;
using System.Collections.Generic;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using MaterialIcon = global::Material.Icons.Avalonia.MaterialIcon;
using AvaloniaLayout = Avalonia.Layout;

namespace Cine.Avalonia.Components;

/// <summary>
/// Builds the shared primary (3-dot) header menu with Material icons.
/// Used by both HeaderBarControl and FullscreenHeaderControl.
/// Toggle items use a CheckCircle/CircleOutline pattern matching the context menu.
/// </summary>
public class PrimaryMenuBuilder
{
    private readonly List<global::Avalonia.Controls.Control> _items = new();
    private readonly List<(MaterialIcon Icon, Func<bool> IsChecked)> _toggleItems = new();

    private static readonly global::Avalonia.Media.Color AccentColor = AppColors.Parse("#0078D4").Color;

    private static MaterialIcon MakeIcon(global::Material.Icons.MaterialIconKind kind, double size = 16, IBrush? brush = null)
        => new() { Kind = kind, Width = size, Height = size, Foreground = brush ?? AppColors.TextOnDarkHint };

    private static global::Material.Icons.MaterialIconKind ParseIcon(string name)
    {
        try { return (global::Material.Icons.MaterialIconKind)Enum.Parse(typeof(global::Material.Icons.MaterialIconKind), name); }
        catch
        {
            System.Diagnostics.Debug.WriteLine($"[PrimaryMenuBuilder] Unknown icon: \"{name}\"");
            return global::Material.Icons.MaterialIconKind.CircleOutline;
        }
    }

    /// <summary>Adds a section header label (e.g. "PLAYBACK").</summary>
    public PrimaryMenuBuilder AddSection(string title)
    {
        var header = new global::Avalonia.Controls.MenuItem { Header = title.ToUpperInvariant() };
        header.Classes.Add("menu-section-header");
        _items.Add(header);
        return this;
    }

    /// <summary>Adds a separator between sections.</summary>
    public PrimaryMenuBuilder AddSeparator()
    {
        _items.Add(new Separator());
        return this;
    }

    /// <summary>Adds a menu item with icon, label, and optional shortcut text.</summary>
    public PrimaryMenuBuilder AddItem(string iconKind, string text, string? shortcut, Action onClick)
    {
        var icon = MakeIcon(ParseIcon(iconKind), 16);

        var item = new global::Avalonia.Controls.MenuItem
        {
            Header = text,
            InputGesture = shortcut != null ? KeyGesture.Parse(shortcut) : null,
            Icon = icon
        };
        item.Click += (_, _) => onClick();
        _items.Add(item);
        return this;
    }

    /// <summary>
    /// Adds a toggle menu item. Icon is accent-colored when checked, dimmed when unchecked.
    /// </summary>
    public PrimaryMenuBuilder AddToggleItem(string iconKind, string text, string? shortcut,
        Action onClick, Func<bool> isChecked)
    {
        var icon = MakeIcon(ParseIcon(iconKind), 16);
        var accent = new SolidColorBrush(AppColors.Accent?.Color ?? AccentColor);

        // Tint the icon accent when active, dim when inactive
        icon.Foreground = isChecked() ? accent : AppColors.TextOnDarkHint;

        var item = new global::Avalonia.Controls.MenuItem
        {
            Header = text,
            InputGesture = shortcut != null ? KeyGesture.Parse(shortcut) : null,
            Icon = icon
        };

        item.Click += (_, _) =>
        {
            onClick();
            icon.Foreground = isChecked() ? accent : AppColors.TextOnDarkHint;
        };

        _items.Add(item);
        _toggleItems.Add((icon, isChecked));
        return this;
    }

    /// <summary>Updates all toggle icons to reflect current state. Call before showing flyout.</summary>
    public void SyncCheckStates()
    {
        var accent = new SolidColorBrush(AppColors.Accent?.Color ?? AccentColor);
        foreach (var (icon, check) in _toggleItems)
        {
            icon.Foreground = check() ? accent : AppColors.TextOnDarkHint;
        }
    }

    /// <summary>Builds the MenuFlyout.</summary>
    public MenuFlyout Build()
    {
        var menu = new MenuFlyout
        {
            Placement = PlacementMode.BottomEdgeAlignedRight
        };
        foreach (var item in _items)
            menu.Items.Add(item);
        return menu;
    }
}
