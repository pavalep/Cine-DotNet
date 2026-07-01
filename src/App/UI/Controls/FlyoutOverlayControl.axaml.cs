using System;
using Avalonia;
using Avalonia.Input;
using Avalonia.Threading;
using Avalonia.VisualTree;
using global::Avalonia.Controls;

namespace Cine.Avalonia.Controls;

public partial class FlyoutOverlayControl : UserControl
{
    // Incremented each time we hide — lets us cancel stale deferred cleanups.
    private int _hideStamp = 0;

    public FlyoutOverlayControl()
    {
        InitializeComponent();
        ContentContainer.Child = null;
        Opacity = 0;
        IsHitTestVisible = false;
    }

    /// <summary>True when the overlay content is currently visible (opacity > 0).</summary>
    public bool IsOpen => ContentContainer.Opacity > 0.5;

    public void ShowContent(global::Avalonia.Controls.Control anchor, global::Avalonia.Controls.Control content, bool placeAbove = true)
    {
        // Cancel any pending deferred hide from a previous HideContent call
        _hideStamp++;

        ContentContainer.Child = content;
        content.Measure(global::Avalonia.Size.Infinity);
        var cs = content.DesiredSize;

        var overlayPoint = ((Visual)anchor).TranslatePoint(new global::Avalonia.Point(0, 0), this).GetValueOrDefault();
        var anchorRect = anchor.Bounds;

        double x = overlayPoint.X + (anchorRect.Width - cs.Width) / 2;
        double y = placeAbove
            ? overlayPoint.Y - cs.Height - 8
            : overlayPoint.Y + anchorRect.Height + 8;

        var winSize = Bounds.Size;

        if (x + cs.Width > winSize.Width - 8) x = winSize.Width - cs.Width - 8;
        if (x < 8) x = 8;
        if (y + cs.Height > winSize.Height - 8) y = placeAbove ? overlayPoint.Y + anchorRect.Height + 8 : winSize.Height - cs.Height - 8;
        if (y < 8) y = 8;

        global::Avalonia.Controls.Canvas.SetLeft(ContentContainer, x);
        global::Avalonia.Controls.Canvas.SetTop(ContentContainer, y);

        IsHitTestVisible = true;

        // Start at 0, then trigger animation to 1 on next render frame
        // so the XAML DoubleTransition on ContentContainer.Opacity kicks in.
        ContentContainer.Opacity = 0;
        Opacity = 1;

        var stamp = _hideStamp;
        Dispatcher.UIThread.Post(() =>
        {
            if (stamp == _hideStamp)
                ContentContainer.Opacity = 1;
        }, DispatcherPriority.Render);
    }

    public void HideContent()
    {
        _hideStamp++; // Invalidate any pending show/post from a previous ShowContent
        ContentContainer.Opacity = 0;
        IsHitTestVisible = false;
    }

    private void OnBackgroundPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        OnBackgroundDismissed?.Invoke();
        HideContent();
    }

    private void OnBackgroundKeyDown(object? sender, global::Avalonia.Input.KeyEventArgs e)
    {
        if (e.Key == global::Avalonia.Input.Key.Escape)
        {
            OnBackgroundDismissed?.Invoke();
            HideContent();
            e.Handled = true;
        }
    }

    public event Action? OnBackgroundDismissed;
}