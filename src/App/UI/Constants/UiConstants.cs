using Avalonia;

namespace Cine.Avalonia;

public static class UiConstants
{
    // Layout breakpoints
    public const double NarrowBreakpoint = 600.0;
    public const double MediumBreakpoint = 1024.0;
    public const double NarrowControlsBreakpoint = 495.0;

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
