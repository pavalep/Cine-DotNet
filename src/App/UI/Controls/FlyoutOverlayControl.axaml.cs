using System;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;

namespace Cine.Avalonia.Controls;

/// <summary>
/// Full-window transparent overlay that hosts flyout content at the
/// window level, bypassing Avalonia's broken Popup overlay layer
/// (WindowDecorations=None + ExtendClientArea creates a visual tree
/// where PopupRoot cannot attach to the overlay layer).
///
/// Renders at ZIndex=50 in the window Grid. Content is positioned
/// via Canvas.Left/Top relative to the window.
/// </summary>
public partial class FlyoutOverlayControl : global::Avalonia.Controls.UserControl
{
    public FlyoutOverlayControl()
    {
        InitializeComponent();
        ContentContainer.Child = null;
    }

    /// <summary>
    /// Shows flyout content positioned relative to the anchor control.
    /// For Bottom menus (ControlsBox), opens above the button.
    /// For Top menus (HeaderBar), opens below the button.
    /// </summary>
    public void ShowContent(global::Avalonia.Controls.Control anchor, global::Avalonia.Controls.Control content, bool placeAbove = true)
    {
        ContentContainer.Child = content;

        content.Measure(global::Avalonia.Size.Infinity);
        var cs = content.DesiredSize;

        // Translate anchor top-left to overlay control coordinates
        var overlayPoint = anchor.TranslatePoint(new global::Avalonia.Point(0, 0), this).GetValueOrDefault();
        var anchorRect = anchor.Bounds;

        // Position: center horizontally on the anchor, place above/below with gap
        double x = overlayPoint.X + (anchorRect.Width - cs.Width) / 2;
        double y = placeAbove
            ? overlayPoint.Y - cs.Height - 8
            : overlayPoint.Y + anchorRect.Height + 8;

        var winSize = this.Bounds.Size;
        
        // Clamp to window bounds
        if (x + cs.Width > winSize.Width - 8) x = winSize.Width - cs.Width - 8;
        if (x < 8) x = 8;
        if (y + cs.Height > winSize.Height - 8) y = placeAbove ? overlayPoint.Y + anchorRect.Height + 8 : winSize.Height - cs.Height - 8;
        if (y < 8) y = 8;

        global::Avalonia.Controls.Canvas.SetLeft(ContentContainer, x);
        global::Avalonia.Controls.Canvas.SetTop(ContentContainer, y);

        IsVisible = true;
        ContentContainer.IsVisible = true;
    }

    public void HideContent()
    {
        ContentContainer.Child = null;
        IsVisible = false;
    }

    private void OnBackgroundPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        OnBackgroundDismissed?.Invoke();
        HideContent();
    }

    private void OnBackgroundKeyDown(object? sender, global::Avalonia.Input.KeyEventArgs e)
    {
        // ESC key dismisses the overlay
        if (e.Key == global::Avalonia.Input.Key.Escape)
        {
            OnBackgroundDismissed?.Invoke();
            HideContent();
            e.Handled = true;
        }
    }

    /// <summary>Fired when the flyout is dismissed (click outside content area).</summary>
    public event Action? OnBackgroundDismissed;
}
