using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Media;
using AvaloniaLayout = Avalonia.Layout;

namespace Cine.Avalonia.Helpers;

/// <summary>
/// Fluent builder for constructing popover/flyout menus.
/// P7.4: Replaces manual Border/StackPanel/ScrollViewer construction with named WithX() methods.
/// </summary>
public class FlyoutBuilder
{
    private readonly StackPanel _stack = new();
    private string _popoverBgKey = "PopoverBackground";
    private string _popoverBorderKey = "PopoverBorder";
    private double _cornerRadius = 8;
    private double _minWidth = 220;
    private double _maxHeight = 300;
    private Thickness _padding = new(4);
    private PlacementMode _placement = PlacementMode.Top;
    private bool _scrollable = true;

    public FlyoutBuilder WithMinWidth(double width) { _minWidth = width; return this; }
    public FlyoutBuilder WithMaxHeight(double height) { _maxHeight = height; return this; }
    public FlyoutBuilder WithPadding(Thickness padding) { _padding = padding; return this; }
    public FlyoutBuilder WithPlacement(PlacementMode placement) { _placement = placement; return this; }
    public FlyoutBuilder WithCornerRadius(double radius) { _cornerRadius = radius; return this; }
    public FlyoutBuilder WithScrollable(bool scrollable) { _scrollable = scrollable; return this; }
    public FlyoutBuilder WithResourceKeys(string background, string border)
    {
        _popoverBgKey = background;
        _popoverBorderKey = border;
        return this;
    }

    private static IBrush? Resource(string key) =>
        (IBrush?)global::Avalonia.Application.Current?.FindResource(key);

    private static global::Avalonia.Controls.Button MakeButton(global::Avalonia.Controls.Control content)
    {
        var btn = new global::Avalonia.Controls.Button
        {
            Content = content,
            Background = AppColors.Transparent,
            BorderThickness = new Thickness(0),
            HorizontalContentAlignment = AvaloniaLayout.HorizontalAlignment.Stretch,
            Cursor = new global::Avalonia.Input.Cursor(StandardCursorType.Arrow)
        };
        btn.PointerEntered += (_, _) => btn.Background = AppColors.HoverSubtle;
        btn.PointerExited += (_, _) => btn.Background = AppColors.Transparent;
        return btn;
    }

    /// <summary>
    /// Add a simple text action item.
    /// </summary>
    public FlyoutBuilder AddItem(string text, Action onClick, bool isBold = false, string? tooltip = null)
    {
        var tb = new TextBlock
        {
            Text = text,
            FontSize = 12,
            FontWeight = isBold ? FontWeight.SemiBold : FontWeight.Normal,
            Foreground = Resource("OsdForeground"),
            Padding = new Thickness(10, 6),
            VerticalAlignment = AvaloniaLayout.VerticalAlignment.Center
        };
        var btn = MakeButton(tb);
        if (tooltip != null) global::Avalonia.Controls.ToolTip.SetTip(btn, tooltip);
        btn.Click += (_, _) => onClick();
        _stack.Children.Add(btn);
        return this;
    }

    /// <summary>
    /// Add a two-column item (key on left, action on right).
    /// </summary>
    public FlyoutBuilder AddLabeledItem(string leftText, string rightText, Action onClick)
    {
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
            Text = leftText,
            FontSize = 12,
            Foreground = Resource("OsdForeground"),
            VerticalAlignment = AvaloniaLayout.VerticalAlignment.Center,
            Padding = new Thickness(10, 6, 4, 6)
        };
        var right = new TextBlock
        {
            Text = rightText,
            FontSize = 11,
            Foreground = AppColors.TextOnDarkHint,
            VerticalAlignment = AvaloniaLayout.VerticalAlignment.Center,
            Padding = new Thickness(4, 6, 10, 6)
        };
        Grid.SetColumn(left, 0);
        Grid.SetColumn(right, 1);
        grid.Children.Add(left);
        grid.Children.Add(right);

        var btn = MakeButton(grid);
        btn.Click += (_, _) => onClick();
        _stack.Children.Add(btn);
        return this;
    }

    /// <summary>
    /// Add a raw UI element to the flyout stack.
    /// </summary>
    public FlyoutBuilder AddCustom(global::Avalonia.Controls.Control control)
    {
        _stack.Children.Add(control);
        return this;
    }

    /// <summary>
    /// Add a separator line.
    /// </summary>
    public FlyoutBuilder AddSeparator()
    {
        _stack.Children.Add(new Separator
        {
            Background = Resource("PopoverBorder"),
            Margin = new Thickness(4, 2)
        });
        return this;
    }

    /// <summary>
    /// Build the Flyout.
    /// </summary>
    public Flyout Build()
    {
        var border = new Border
        {
            Background = Resource(_popoverBgKey),
            BorderBrush = Resource(_popoverBorderKey),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(_cornerRadius),
            Padding = _padding,
            MinWidth = _minWidth,
            MaxHeight = _maxHeight,
            Child = _stack
        };

        global::Avalonia.Controls.Control content = border;
        if (_scrollable)
        {
            content = new ScrollViewer
            {
                Content = border,
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto
            };
        }

        return new Flyout { Content = content, Placement = _placement };
    }
}
