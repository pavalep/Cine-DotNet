using System;
using Avalonia;
using Avalonia.Input;
using Avalonia.Threading;
using Avalonia.VisualTree;
using global::Avalonia.Controls;

namespace Cine.Avalonia.Components;

public partial class FlyoutOverlay : UserControl
{
    // Incremented each time we hide — lets us cancel stale deferred cleanups.
    private int _hideStamp = 0;

    // A8: Remember anchor/content for repositioning on window resize
    private global::Avalonia.Controls.Control? _lastAnchor;
    private global::Avalonia.Controls.Control? _lastContent;
    private bool _placeAbove;

    public FlyoutOverlay()
    {
        InitializeComponent();
        ContentContainer.Child = null;
        Opacity = 0;
        IsHitTestVisible = false;
    }

    public void ShowContent(global::Avalonia.Controls.Control anchor, global::Avalonia.Controls.Control content, bool placeAbove = true)
    {
        // Cancel any pending deferred hide from a previous HideContent call
        _hideStamp++;

        // A8: Store for repositioning on resize
        _lastAnchor = anchor;
        _lastContent = content;
        _placeAbove = placeAbove;
        // Subscribe once to SizeChanged for repositioning
        SizeChanged -= OnOverlaySizeChanged;
        SizeChanged += OnOverlaySizeChanged;

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

        // A7: Defer IsHitTestVisible to after animation frame so clicks don't land on stale content
        IsHitTestVisible = false;

        // Start at 0, then trigger animation to 1 on next render frame
        // so the XAML DoubleTransition on ContentContainer.Opacity kicks in.
        ContentContainer.Opacity = 0;
        Opacity = 1;

        var stamp = _hideStamp;
        Dispatcher.UIThread.Post(() =>
        {
            if (stamp == _hideStamp)
            {
                ContentContainer.Opacity = 1;
                // A7: Enable hit-test only after content is fully visible
                IsHitTestVisible = true;
                // A4: Focus content so keyboard events (Escape) reach the overlay
                content.Focus();
            }
        }, DispatcherPriority.Render);
    }

    public void HideContent()
    {
        _hideStamp++; // Invalidate any pending show/post from a previous ShowContent
        ContentContainer.Opacity = 0;
        // A2: Clear child content to prevent stale content and memory leaks
        ContentContainer.Child = null;
        // A3: Restore root opacity so background dim disappears
        Opacity = 0;
        IsHitTestVisible = false;
        // A8: Clean up last anchor/content references
        _lastAnchor = null;
        _lastContent = null;
        SizeChanged -= OnOverlaySizeChanged;
    }

    // A8: Reposition flyout content when window is resized
    private void OnOverlaySizeChanged(object? sender, global::Avalonia.Controls.SizeChangedEventArgs e)
    {
        var anchor = _lastAnchor;
        var content = _lastContent;
        if (anchor == null || content == null || ContentContainer.Child == null)
            return;

        var cs = content.DesiredSize;
        var overlayPoint = ((Visual)anchor).TranslatePoint(new global::Avalonia.Point(0, 0), this).GetValueOrDefault();
        var anchorRect = anchor.Bounds;

        double x = overlayPoint.X + (anchorRect.Width - cs.Width) / 2;
        double y = _placeAbove
            ? overlayPoint.Y - cs.Height - 8
            : overlayPoint.Y + anchorRect.Height + 8;

        var winSize = e.NewSize;

        if (x + cs.Width > winSize.Width - 8) x = winSize.Width - cs.Width - 8;
        if (x < 8) x = 8;
        if (y + cs.Height > winSize.Height - 8) y = _placeAbove ? overlayPoint.Y + anchorRect.Height + 8 : winSize.Height - cs.Height - 8;
        if (y < 8) y = 8;

        global::Avalonia.Controls.Canvas.SetLeft(ContentContainer, x);
        global::Avalonia.Controls.Canvas.SetTop(ContentContainer, y);
    }

    private void OnBackgroundPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        // A1: Ignore clicks that originated inside ContentContainer (flyout content).
        // Without this guard, any click on a button/list-item inside the flyout
        // bubbles up through the Canvas/Border and dismisses the overlay.
        if (ContentContainer.Child != null &&
            e.Source is Visual sourceVisual &&
            ContentContainer.IsVisualAncestorOf(sourceVisual))
        {
            return;
        }

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
