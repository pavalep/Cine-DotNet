using System;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Input;
using Avalonia.Threading;
using Cine.Avalonia.Helpers;
namespace Cine.Avalonia;

public partial class MainWindow
{
    // =========================================================================
    // Auto-hide overlay — aligned with Python Reference (PyGObject + Blueprint)
    //
    // Fields: _autoHideTimer, _uiVisible, _lastMousePosition, _isMouseOverControls
    //         declared in MainWindow.Core.cs
    //
    // Python approach (code_for_reference/src/window.blp + window.py):
    //   - All layers stacked in an Overlay widget (no Grid, no rows)
    //   - Header + Controls wrapped in a single `revealer_ui` containing a
    //     vertical Box with expandable Separator to push header←top, controls←bottom
    //   - Motion controllers on revealer_ui, headerbar, controls_box — each
    //     tracks hover independently via direct controller attachment
    //   - _hide_ui() checks header/controls hover BEFORE hiding
    //   - _show_ui() is idempotent: reveals, resets timer
    //
    // Avalonia implementation:
    //   - Panel (overlap container) ✓  (replaced Grid in .axaml)
    //   - HeaderBarControl + ControlsBoxControl float on top via alignment
    //   - Hover tracked via PointerEntered/PointerExited on each element
    //     (Avalonia's equivalent of GTK event controllers)
    //   - HideCondition: timer fires → checks _hover* flags → hides or resets
    // =========================================================================

    // Hover state — set by PointerEntered/Exited on each overlay element.
    // Mirrors Python's `motion_header.contains_pointer` / `motion_controls.contains_pointer`.
    private bool _hoverHeader;
    private bool _hoverControls;
    private bool _hoverFullscreenHeader;

    private void InitializeAutoHide()
    {
        _autoHideTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(3000)
        };
        _autoHideTimer.Tick += OnAutoHideTimerTick;
        _autoHideTimer?.Start();
    }

    // ── Auto-hide timer logic (aligned with Python _on_hide_ui) ──

    private void OnAutoHideTimerTick(object? sender, EventArgs e)
    {
        // Python: _hide_ui() checks `motion_header.contains_pointer`
        //        and `motion_controls.contains_pointer` before hiding.
        if (_hoverHeader || _hoverControls || _hoverFullscreenHeader)
        {
            _autoHideTimer?.Start();
            return;
        }

        bool hasMedia = !string.IsNullOrEmpty(_viewModel?.FilePath);
        if (!hasMedia) return;

        bool isFlyoutOpen = _controlsBox.HasActiveFlyouts ||
                            _fullscreenHeader.HasActiveFlyouts ||
                            _headerBar.HasActiveFlyouts ||
                            _dropIndicator.IsShowing;
        if (isFlyoutOpen)
        {
            _autoHideTimer?.Start();
            return;
        }

        HideUiControls();
    }

    // ── Window-level pointer (catches all mouse movement) ──
    // Mirrors Python's motion_controller on revealer_ui

    private void OnWindowPointerMoved(object? sender, PointerEventArgs e)
    {
        var pos = e.GetCurrentPoint(this).Position;

        if (!_uiVisible)
            ShowUiControls();

        if (Math.Abs(pos.X - _lastMousePosition.X) > 3 ||
            Math.Abs(pos.Y - _lastMousePosition.Y) > 3)
        {
            _lastMousePosition = pos;
            _autoHideTimer?.Stop();
            _autoHideTimer?.Start();
        }
    }

    // ── Hover tracking — direct PointerEntered/Exited on each overlay element ──
    // Mirrors Python's EventController + contains_pointer checks

    private void OnHeaderPointerEntered(object? sender, PointerEventArgs e) => _hoverHeader = true;
    private void OnHeaderPointerExited(object? sender, PointerEventArgs e) => _hoverHeader = false;
    private void OnControlsPointerEntered(object? sender, PointerEventArgs e) => _hoverControls = true;
    private void OnControlsPointerExited(object? sender, PointerEventArgs e) => _hoverControls = false;
    private void OnFullscreenHeaderPointerEntered(object? sender, PointerEventArgs e) => _hoverFullscreenHeader = true;
    private void OnFullscreenHeaderPointerExited(object? sender, PointerEventArgs e) => _hoverFullscreenHeader = false;

    // ── Show / Hide — aligned with Python idempotent _show_ui / _hide_ui ──

    private void ShowUiControls()
    {
        _uiVisible = true;
        _autoHideTimer?.Stop();

        bool isFullscreen = WindowState == global::Avalonia.Controls.WindowState.FullScreen;

        // Header bar: always show unless in fullscreen
        if (_headerBar.HeaderBar != null)
        {
            _headerBar.HeaderBar.IsVisible = !isFullscreen;
            _headerBar.HeaderBar.Opacity = isFullscreen ? 0 : 1;
            _headerBar.HeaderBar.IsHitTestVisible = !isFullscreen;
        }
        // Fullscreen header: only visible when in fullscreen mode
        if (_fullscreenHeader.FullscreenHeader != null)
        {
            _fullscreenHeader.FullscreenHeader.IsVisible = isFullscreen;
            _fullscreenHeader.FullscreenHeader.Opacity = isFullscreen ? 1 : 0;
            _fullscreenHeader.FullscreenHeader.IsHitTestVisible = isFullscreen;
        }
        // Controls box: always show
        if (_controlsBox.ControlsBox != null)
        {
            _controlsBox.ControlsBox.IsVisible = true;
            _controlsBox.ControlsBox.Opacity = 1;
            _controlsBox.ControlsBox.IsHitTestVisible = true;
        }

        _autoHideTimer?.Start();
    }

    private void HideUiControls()
    {
        _uiVisible = false;
        _autoHideTimer?.Stop();

        bool isFullscreen = WindowState == global::Avalonia.Controls.WindowState.FullScreen;

        if (isFullscreen)
        {
            _headerBar.HeaderBar.IsVisible = false;
            _headerBar.HeaderBar.Opacity = 0;
            _headerBar.HeaderBar.IsHitTestVisible = false;
            _fullscreenHeader.FullscreenHeader.IsVisible = false;
            _fullscreenHeader.FullscreenHeader.Opacity = 0;
            _fullscreenHeader.FullscreenHeader.IsHitTestVisible = false;
        }
        else
        {
            _headerBar.HeaderBar.Opacity = 0;
            _headerBar.HeaderBar.IsHitTestVisible = false;
            _fullscreenHeader.FullscreenHeader.IsVisible = false;
            _fullscreenHeader.FullscreenHeader.Opacity = 0;
            _fullscreenHeader.FullscreenHeader.IsHitTestVisible = false;
        }
        _controlsBox.ControlsBox.IsVisible = false;
        _controlsBox.ControlsBox.Opacity = 0;
        _controlsBox.ControlsBox.IsHitTestVisible = false;
    }

    // ── Fade animation (Python: revealer transition) ──

    private async void FadeHeaderAndControls(double targetOpacity)
    {
        ErrorBoundary.Run(async () =>
        {
            var headerBar = _headerBar.HeaderBar;
            var controlsBox = _controlsBox.ControlsBox;
            if (headerBar == null && controlsBox == null) return;

            double startHeader = headerBar?.Opacity ?? 0;
            double startControls = controlsBox?.Opacity ?? 0;
            int steps = 6;

            for (int i = 1; i <= steps; i++)
            {
                double t = i / (double)steps;
                await Dispatcher.UIThread.OnUiThreadAsync(() =>
                {
                    if (headerBar != null)
                        headerBar.Opacity = startHeader + (targetOpacity - startHeader) * t;
                    if (controlsBox != null)
                        controlsBox.Opacity = startControls + (targetOpacity - startControls) * t;
                });
                await Task.Delay(16);
            }

            await Dispatcher.UIThread.OnUiThreadAsync(() =>
            {
                if (headerBar != null) headerBar.Opacity = targetOpacity;
                if (controlsBox != null) controlsBox.Opacity = targetOpacity;
            });
        });
    }

}
