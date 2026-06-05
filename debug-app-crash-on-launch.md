# Debug Session: app-crash-on-launch

**Status:** [OPEN — Fix applied, ready for verification]
**Session ID:** app-crash-on-launch
**Start:** 2026-06-05
**Symptom:** App crashes immediately on launch (exit code -2146232797 = COR_E_EXCEPTION) — no window appears.
**Last changes:** MainWindow.axaml layer restructuring, auto-hide hit-test changes, FilePath watcher changes.

## Hypotheses

1. ~~**H1: XAML parse failure**~~ — REJECTED. Was the `ZIndex` attached property syntax, now fixed.
2. ~~**H2: NullReferenceException in startup path**~~ — REJECTED. InitializeComponent runs before property watchers.
3. ~~**H3: Type cast error**~~ — REJECTED. MainOverlay is `FindControl<Control>`, no cast to Panel.
4. ~~**H4: Missing resource key**~~ — REJECTED. Build succeeds, XAML resources resolve fine.
5. **H5: GC collected WndProc delegate** — **CONFIRMED**. Classic .NET interop bug in D3D11VideoHost.

## Crash 1: GC collected delegate (FIXED)

**Evidence:**
```
A callback was made on a garbage collected delegate of type
'App!Cine.Avalonia.Controls.D3D11VideoHost+WndProcDelegate::Invoke'.
```

**Root cause:** `Marshal.GetFunctionPointerForDelegate(new WndProcDelegate(StaticWndProc))` creates a delegate inline and never stores it. GC collects it, native code calls back to freed memory.

**Fix:** Added static `_wndProcDelegate` field to keep the delegate alive.

## Crash 2: Cross-thread UI access (FIXED)

**Evidence:**
```
System.InvalidOperationException: The calling thread cannot access this object because a different thread owns it.
   at Avalonia.Visual.set_Opacity(Double value)
   at MainWindow.FadeVisual(Control, Double, Double, Int32, Boolean)
   at MainWindow.OnMediaOpened(Object, EventArgs)
```

**Root cause:** `OnMediaOpened` fires from player event loop (background thread). `FadeVisual` + `_dropIndicator.Hide()` access UI properties after `await` continuation (which runs on ThreadPool).

**Fix:** Consolidated all UI access into a single `Dispatcher.UIThread.OnUiThreadAsync` block. Replaced `FadeVisual` with direct property sets.

## Crash 3: Cross-thread fade animation (FIXED)

**Evidence (static analysis):** `FadeHeaderAndControls` uses `await Task.Delay(16)` in a loop, then directly sets `headerBar.Opacity` and `controlsBox.Opacity` after continuation (ThreadPool).

**Root cause:** Same pattern as Crash 2 — `await` continuation runs on ThreadPool, then accesses Avalonia UI properties.

**Fix:** Wrapped all `Opacity` assignments in `Dispatcher.UIThread.OnUiThreadAsync` blocks after each `Task.Delay`.

## Crash 4: PropertyWatcher callbacks on background thread (FIXED)

**Evidence (static analysis):** `MainViewModel.OpenFile` calls `RefreshState()` in `finally` block. If called from non-UI thread (drag-drop, CLI args), `PropertyChanged` fires on that thread, and `SetupPropertyWatchers` callbacks directly set UI control properties.

**Root cause:** `PropertyWatcher.OnPropertyChanged` runs callbacks on whatever thread raised `PropertyChanged`. The watcher callbacks in `MainWindow.Core.cs` manipulate Avalonia controls directly.

**Fix:** Added `Dispatcher.UIThread.CheckAccess()` guard in `PropertyWatcher.OnPropertyChanged` — dispatches to UI thread if called from background thread.

## Bug 5: OnOsdNotificationClicked event handler leak (FIXED)

**Evidence (static analysis):** Handler lambda captures `_playerService` dynamically, subscribes to `_playerService.Player.Opened`, but unsubscribes from whatever `_playerService.Player` returns at handler time — which may be a different instance.

**Fix:** Capture `_playerService.Player` reference at subscribe time in a local, bail early if null.

## Fixes Applied

| Fix | File | Change |
|-----|------|--------|
| GC delegate | `D3D11VideoHost.cs` | Added `static _wndProcDelegate` field, stored delegate in `RegisterWindowClass()` |
| Thread safety | `MainWindow.Media.cs` | Moved all `OnMediaOpened` UI work into single `OnUiThreadAsync` block |
| Cross-thread fade | `MainWindow.AutoHide.cs` | `FadeHeaderAndControls`: moved `Opacity` sets into `OnUiThreadAsync` after each `Task.Delay` |
| PropertyWatcher dispatch | `PropertyWatcher.cs` | `OnPropertyChanged` now checks `CheckAccess()` and dispatches to UI thread if needed |
| Event leak | `MainWindow.Media.cs` | `OnOsdNotificationClicked`: capture player ref at subscribe time, bail early if null |

## Evidence Log

| Step | Action | Result |
|------|--------|--------|
| 1 | Build + Run | Crash 1: GC delegate error |
| 2 | Fix GC delegate | Build clean, but Crash 2: cross-thread |
| 3 | Fix thread safety | Build clean, 0 errors |
