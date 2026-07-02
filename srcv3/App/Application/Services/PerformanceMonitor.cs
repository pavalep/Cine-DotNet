using System;
using System.Diagnostics;
using System.Threading;

namespace Cine.Avalonia.Services;

/// <summary>
/// Frame pacing and drop detection for the mpv OpenGL render path.
/// Counts rendered frames per second and logs warnings when the count
/// drops below 50fps (indicative of dropped frames for 60fps content).
///
/// Thread-safe: uses Interlocked for all shared state.
/// </summary>
public class PerformanceMonitor
{
    private int _frameCount;
    private long _lastCheckTimestamp;
    private int _dropsDetected;
    private int _peakFps;
    private int _minFps = int.MaxValue;

    /// <summary>Frames rendered in the current (or most recently completed) second.</summary>
    public int FramesThisSecond => Volatile.Read(ref _frameCount);

    /// <summary>Cumulative number of seconds where frame count dropped below 50.</summary>
    public int DropsDetected => Volatile.Read(ref _dropsDetected);

    /// <summary>Peak frames-per-second observed since monitor creation.</summary>
    public int PeakFps => Volatile.Read(ref _peakFps);

    /// <summary>Minimum frames-per-second observed since monitor creation.</summary>
    public int MinFps
    {
        get
        {
            var v = Volatile.Read(ref _minFps);
            return v == int.MaxValue ? 0 : v;
        }
    }

    /// <summary>
    /// Call this from the render loop after each successful frame display.
    /// Thread-safe; can be called from any thread.
    /// </summary>
    public void OnFrameRendered()
    {
        Interlocked.Increment(ref _frameCount);

        long now = Stopwatch.GetTimestamp();
        long last = Interlocked.Read(ref _lastCheckTimestamp);

        // Handle first call
        if (last == 0)
        {
            Interlocked.Exchange(ref _lastCheckTimestamp, now);
            return;
        }

        // Check if one second has elapsed
        if (now - last >= Stopwatch.Frequency)
        {
            int count = Interlocked.Exchange(ref _frameCount, 0);
            Interlocked.Exchange(ref _lastCheckTimestamp, now);

            // Track peak/min
            UpdateExtremes(count);

            // Log if drops detected (sub-15 fps = actual problem, not 24fps film content)
            if (count < 15)
            {
                Interlocked.Increment(ref _dropsDetected);
                CrashReporter.LogError(
                    $"PerformanceMonitor: Frame drop detected — {count} fps in the last second");
            }
        }
    }

    private void UpdateExtremes(int count)
    {
        // Peak
        int peak;
        do
        {
            peak = Volatile.Read(ref _peakFps);
            if (count <= peak) break;
        }
        while (Interlocked.CompareExchange(ref _peakFps, count, peak) != peak);

        // Min
        int min;
        do
        {
            min = Volatile.Read(ref _minFps);
            if (count >= min) break;
        }
        while (Interlocked.CompareExchange(ref _minFps, count, min) != min);
    }

    /// <summary>Reset all counters.</summary>
    public void Reset()
    {
        Interlocked.Exchange(ref _frameCount, 0);
        Interlocked.Exchange(ref _dropsDetected, 0);
        Interlocked.Exchange(ref _peakFps, 0);
        Interlocked.Exchange(ref _minFps, int.MaxValue);
        Interlocked.Exchange(ref _lastCheckTimestamp, 0);
    }

    /// <summary>Get a diagnostic summary string.</summary>
    public string GetSummary()
    {
        return $"PerfMonitor: frames/s={FramesThisSecond} drops={DropsDetected} peak={PeakFps} min={MinFps}";
    }
}
