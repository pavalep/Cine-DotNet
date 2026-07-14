using System;
using System.Diagnostics;
using System.Threading;

namespace Simba.Avalonia.Services;

/// <summary>
/// Throttles render submissions to a maximum rate (~60fps).
/// Prevents the render loop from flooding the ANGLE/mpv render pipeline
/// during rapid frame-ready callbacks or window resize events.
///
/// Thread-safe: uses Interlocked for all shared state.
/// </summary>
public class RenderThrottleService
{
    private long _lastRenderTicks;

    // Minimum interval between renders: ~16.666ms = 60fps
    private const long MinIntervalTicks = 166660; // ~16.666ms in TimeSpan ticks (1 ms = 10000 ticks)

    /// <summary>
    /// The minimum interval between renders in ticks (default ~16.666ms).
    /// </summary>
    public long MinInterval
    {
        get => Volatile.Read(ref _minIntervalField);
        set => Interlocked.Exchange(ref _minIntervalField, value);
    }
    private long _minIntervalField = MinIntervalTicks;

    /// <summary>Total number of renders that were skipped due to throttling.</summary>
    public long SkippedRenders => Volatile.Read(ref _skippedRenders);
    private long _skippedRenders;

    /// <summary>Total number of renders that were allowed through.</summary>
    public long AllowedRenders => Volatile.Read(ref _allowedRenders);
    private long _allowedRenders;

    /// <summary>
    /// Returns true if the render should proceed (enough time has elapsed
    /// since the last allowed render). Thread-safe.
    /// </summary>
    public bool ShouldRender()
    {
        long now = Stopwatch.GetTimestamp();
        long last = Volatile.Read(ref _lastRenderTicks);
        long interval = Volatile.Read(ref _minIntervalField);

        // First call: allow immediately (last == 0 means never rendered)
        if (last == 0)
        {
            Interlocked.Exchange(ref _lastRenderTicks, now);
            Interlocked.Increment(ref _allowedRenders);
            return true;
        }

        // Convert Stopwatch ticks to TimeSpan ticks
        long elapsed = (now - last) * TimeSpan.TicksPerSecond / Stopwatch.Frequency;

        if (elapsed < interval)
        {
            Interlocked.Increment(ref _skippedRenders);
            return false;
        }

        Interlocked.Exchange(ref _lastRenderTicks, now);
        Interlocked.Increment(ref _allowedRenders);
        return true;
    }

    /// <summary>Reset throttle state (clears timing and counters).</summary>
    public void Reset()
    {
        Interlocked.Exchange(ref _lastRenderTicks, 0);
        Interlocked.Exchange(ref _skippedRenders, 0);
        Interlocked.Exchange(ref _allowedRenders, 0);
    }

    /// <summary>Get a diagnostic summary string.</summary>
    public string GetSummary()
    {
        return $"RenderThrottle: allowed={AllowedRenders} skipped={SkippedRenders} ratio={SkippedRenders / Math.Max(1.0, AllowedRenders + SkippedRenders):F2}";
    }
}
