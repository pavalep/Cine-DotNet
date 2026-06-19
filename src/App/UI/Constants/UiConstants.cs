using Avalonia;

namespace Cine.Avalonia;

/// <summary>
/// Shared UI dimension, layout, and behavior constants.
/// Mirrors values in UI/Resources/Sizes.axaml for code-behind usage.
/// </summary>
public static class UiConstants
{
    // ── UI Component Sizes ──
    public const double HeaderBarHeight = 56;
    public const double FullscreenHeaderHeight = 44;
    public const double ButtonCircular = 40;
    public const double ButtonFlat = 28;
    public const double ButtonWindowControl = 46;
    public const double ButtonWindowControlHeight = 32;
    public const double OsdMarginBottom = 110;

    // ── Responsive Breakpoints ──
    public const double BreakpointNarrow = 495;
    public const double BreakpointCompact = 600;
    public const double BreakpointTiny = 400;
    public const double BreakpointMedium = 1024.0;

    // Layout breakpoints (legacy aliases, prefer the Breakpoint* naming above)
    public const double NarrowBreakpoint = BreakpointCompact;
    public const double MediumBreakpoint = BreakpointMedium;
    public const double NarrowControlsBreakpoint = BreakpointNarrow;

    // Auto-hide
    public const double AutoHideDelaySeconds = 3.0;

    // Seek bar
    public const double SeekThumbHalf = 8.0;

    // Window
    public const int DefaultWidth = 800;
    public const int DefaultHeight = 600;
    public const int MinWidth = 332;
    public const int MinHeight = 187;

    // Animation
    public const double FadeDurationMs = 300.0;
    public const double FadeFrameIntervalMs = 16.0;

    // OSD
    public const double OsdDefaultDurationMs = 2000.0;

    // Volume
    public const double VolumeMax = 130.0;
    public const double VolumeStep = 5.0;

    // Seek
    public const double SeekStepSeconds = 10.0;
    public const double SeekFastStepSeconds = 60.0;
    public const int SeekWheelThrottleMs = 90;

    // PIP
    public const double PipDefaultWidth = 320;
    public const double PipDefaultHeight = 200;

    // Spinner
    public const double SpinnerAngleStep = 8.0;

    // Session save
    public const double SessionSaveIntervalSeconds = 15.0;
}
