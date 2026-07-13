# Performance Instrumentation

## Phase 2 — mpv OpenGL Render Path

Created: 2026-06-29

## Overview

This document describes the performance instrumentation implemented for Phase 2 of the premium product transformation. The scope is limited to the **mpv OpenGL render path** (ANGLE + pixel readback via `MpvVideoView`).

## Components

### 1. RenderThrottleService

**File:** `src/App/Application/Services/RenderThrottleService.cs`

**Purpose:** Enforces a maximum render rate of ~60fps by throttling frame submissions from the render loop. Prevents the ANGLE/mpv render pipeline from being flooded during rapid frame-ready callbacks.

**How it works:**
- Uses `Stopwatch.GetTimestamp()` for high-resolution timing.
- `ShouldRender()` returns `false` if less than ~16.666ms has elapsed since the last allowed render.
- Thread-safe: all shared state uses `Interlocked` operations.
- Tracks `AllowedRenders` and `SkippedRenders` counters for diagnostics.

**Integration:**
- Called from `MpvVideoView.RenderLoop()` on the dedicated render thread.
- If throttled, the frame-ready flag is consumed (set to `false`) to avoid infinite re-processing.

### 2. PerformanceMonitor

**File:** `src/App/Application/Services/PerformanceMonitor.cs`

**Purpose:** Counts rendered frames per second and logs warnings when the count drops below 50fps, which indicates dropped frames for 60fps content.

**How it works:**
- `OnFrameRendered()` is called after each successful render + display dispatch.
- Every second (via `Stopwatch.Frequency`), it checks the accumulated frame count.
- If below 50, logs a warning via `CrashReporter.LogError()`.
- Tracks `PeakFps`, `MinFps`, and cumulative `DropsDetected`.

**Integration:**
- Called from `MpvVideoView.RenderLoop()` on the render thread after dispatching the display update.

### 3. mpv Premium Tuning (MpvConfig)

**File:** `src/Media/Implementations/mpv/MpvConfig.cs`

**Method:** `GetPremiumTuningOptions()`

Options applied on top of the base render API options:

| Option | Value | Effect |
|--------|-------|--------|
| `audio-buffer` | `0.1` | Reduce audio buffer to 100ms for lower A/V sync latency |
| `opengl-early-flush` | `yes` | Flush OpenGL commands early to reduce frame latency |
| `cache` | `no` | Disable read-ahead cache (not needed for local files) |
| `display-resample` | `linear` | Linear resampling for smooth display rate matching |
| `video-sync` | `display-resample` | Sync video to display refresh with resampling |
| `video-sync-max-audio-change` | `0.1` | Limit audio pitch correction during sync |
| `hwdec` | `auto` | Enable hardware decoding (auto-select best decoder) |

**Integration:**
- Applied in `MpvPlayer.InitializeRenderApi()` after the base `GetRenderApiOptions()`.

## Data Flow

```
mpv frame-ready callback
    → _frameReady = true
    → RenderLoop wakes up
    → MinFrameInterval check (8ms hard cap)
    → RenderThrottleService.ShouldRender() (16.666ms cap)
    → ANGLE FBO bind
    → mpv_render_context_render()
    → Pixel readback
    → Dispatcher.UIThread.Post(UpdateDisplay)
    → PerformanceMonitor.OnFrameRendered()
```

## Diagnostics

All three components expose `GetSummary()` methods that output to the Cine debug log (`cine_mainwin_gl.log`), visible in periodic STATS lines:

```
STATS: frames=123 renders=120 displays=119 vidSize=1920x1080 fbo=1920x1080 | RenderThrottle: allowed=100 skipped=23 ratio=0.19 | PerfMonitor: frames/s=59 drops=0 peak=60 min=58
```

## Measures

- **Render call rate:** ≤ 60fps (enforced by `RenderThrottleService`)
- **Frame drop detection:** Sub-50fps events logged to `cine_errors.log`
- **mpv latency:** Low-latency audio buffer (100ms) and early OpenGL flush enabled
