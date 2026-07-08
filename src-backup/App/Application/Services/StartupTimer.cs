using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;

namespace Cine.Avalonia.Services;

/// <summary>
/// Lightweight per-phase startup timer.
/// Records timestamps at named milestones during startup and outputs a
/// summary report at the end. Each phase duration is relative to the
/// previous phase (delta), not cumulative.
///
/// Thread-safe: only called from the UI thread during startup.
/// </summary>
public sealed class StartupTimer
{
    private readonly Stopwatch _sw = Stopwatch.StartNew();
    private readonly List<PhaseEntry> _phases = new();
    private string _lastPhase = "begin";
    private long _lastTimestamp;
    private bool _finalized;

    /// <summary>
    /// Mark the end of the current phase and start a new one.
    /// </summary>
    /// <param name="phaseName">Short name for the phase (e.g., "ctor", "init-services", "wire-ui").</param>
    public void Mark(string phaseName)
    {
        if (_finalized) return;
        var now = _sw.ElapsedMilliseconds;
        var delta = now - _lastTimestamp;
        _phases.Add(new PhaseEntry(_lastPhase, delta, now));
        _lastPhase = phaseName;
        _lastTimestamp = now;
    }

    /// <summary>
    /// Finalize and return the summary string.
    /// Call once at the end of startup to get a loggable report.
    /// </summary>
    public string Finalize()
    {
        if (_finalized) return _summary;
        _finalized = true;

        // Record the final phase
        Mark("done");

        var sb = new StringBuilder();
        sb.AppendLine($"StartupTimer: {_phases[^1].CumulativeMs}ms total");
        foreach (var p in _phases)
        {
            var pct = _phases[^1].CumulativeMs > 0
                ? (p.DeltaMs * 100.0 / _phases[^1].CumulativeMs)
                : 0.0;
            sb.AppendLine($"  {p.Name,-30} {p.DeltaMs,5}ms ({pct,4:F1}%)");
        }
        _summary = sb.ToString().TrimEnd();
        return _summary;
    }

    /// <summary>
    /// Get the current elapsed milliseconds since timer creation.
    /// </summary>
    public long ElapsedMs => _sw.ElapsedMilliseconds;

    /// <summary>Serialized summary (only non-null after Finalize).</summary>
    public string? Summary => _summary;
    private string _summary = "";

    private readonly record struct PhaseEntry(string Name, long DeltaMs, long CumulativeMs);
}
