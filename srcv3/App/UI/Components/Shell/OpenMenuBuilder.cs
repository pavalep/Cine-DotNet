using System;
using System.Collections.Generic;
using Avalonia.Controls;
using Avalonia.Input;
using MaterialIcon = global::Material.Icons.Avalonia.MaterialIcon;
using AvaloniaLayout = Avalonia.Layout;

namespace Cine.Avalonia.Components;

/// <summary>
/// Builds the header "Open" menu flyout with File / Folder items.
/// Mirrors the PrimaryMenuBuilder pattern exactly.
/// </summary>
public class OpenMenuBuilder
{
    private readonly List<global::Avalonia.Controls.Control> _items = new();
    private MenuFlyout? _flyout;

    private static MaterialIcon MakeIcon(global::Material.Icons.MaterialIconKind kind, double size = 16)
        => new() { Kind = kind, Width = size, Height = size, Foreground = AppColors.TextOnDarkHint };

    private static global::Material.Icons.MaterialIconKind ParseIcon(string name)
    {
        try { return (global::Material.Icons.MaterialIconKind)Enum.Parse(typeof(global::Material.Icons.MaterialIconKind), name); }
        catch
        {
            System.Diagnostics.Debug.WriteLine($"[OpenMenuBuilder] Unknown icon: \"{name}\"");
            return global::Material.Icons.MaterialIconKind.CircleOutline;
        }
    }

    /// <summary>Adds a section header label.</summary>
    public OpenMenuBuilder AddSection(string title)
    {
        var header = new MenuItem { Header = title.ToUpperInvariant() };
        header.Classes.Add("menu-section-header");
        _items.Add(header);
        return this;
    }

    /// <summary>Adds a separator between sections.</summary>
    public OpenMenuBuilder AddSeparator()
    {
        _items.Add(new Separator());
        return this;
    }

    /// <summary>Adds a menu item with icon and label.</summary>
    public OpenMenuBuilder AddItem(string iconKind, string text, string? shortcut, Action onClick)
    {
        var icon = MakeIcon(ParseIcon(iconKind));

        var item = new MenuItem
        {
            Header = text,
            InputGesture = shortcut != null ? KeyGesture.Parse(shortcut) : null,
            Icon = icon
        };
        item.Click += (_, _) => onClick();
        _items.Add(item);
        return this;
    }

    /// <summary>Builds the MenuFlyout.</summary>
    public MenuFlyout Build()
    {
        var menu = new MenuFlyout
        {
            Placement = PlacementMode.Bottom
        };
        foreach (var item in _items)
            menu.Items.Add(item);
        _flyout = menu;
        return menu;
    }

    /// <summary>Hides the flyout if it's open.</summary>
    public void Hide()
    {
        _flyout?.Hide();
    }
}
