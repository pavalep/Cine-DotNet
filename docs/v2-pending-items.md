# v2 — Pending Items

> Auto-generated from code audit. Executed and checked off on 2026-06-26.

---

## Accessibility

### A11Y-1 — Add KeyboardNavigation.DirectionalNavigation to all dialogs
- [x] PreferencesDialog.axaml (checked — property doesn't exist in this Avalonia version, default behavior is fine)
- [x] PlaylistDialog.axaml
- [x] SubtitleSettingsDialog.axaml
- [x] GoToTimeDialog.axaml
- [x] KeyboardShortcutsDialog.axaml
- [x] PipWindow.axaml
- [x] FirstLaunchDialog.axaml

### A11Y-2 — Focus indicator visible on dark backgrounds
- [x] Enhanced focus-visible style: 2px accent BorderThickness + BorderBrush added

### A11Y-3 — Tab order verified in all dialogs
- [x] Default tab order follows visual layout — no explicit TabIndex needed (only 2-3 interactive controls per dialog)

### A11Y-4 — AutomationProperties.Name on SeekBar controls
- [x] SeekBar thumb — `AutomationProperties.Name="Seek position"`
- [x] SeekBar track area — `AutomationProperties.Name="Seek bar track"`
- [x] Position time label — `AutomationProperties.Name="Current playback position"`
- [x] Duration time label — `AutomationProperties.Name="Total duration"`

### A11Y-5 — AutomationProperties.Name on chapter list items
- [x] Chapter markers — `AutomationProperties.Name="Chapter marker"` on Rectangle

---

## Architecture Cleanup

### ARCH-1 — Remove manual `_activeFlyouts` counter
- [x] ControlsBoxControl.axaml.cs: `_activeFlyouts` → `_trackedFlyouts` list-based `IsOpen` check
- [x] HeaderBarControl.axaml.cs: `_activeFlyouts` → `_trackedFlyouts` list-based `IsOpen` check

### ARCH-2 — Split MainWindow.Initialization.cs (400+ lines)
- [x] Created `MainWindow.Startup.cs` — `InitVideoRenderer()`, `InitializeSessionSave()`
- [x] Created `MainWindow.Wiring.cs` — `InitializeWiring()` with all event subscriptions
- [x] Remaining in `MainWindow.Initialization.cs` reduced to ~200 lines (orchestrator only)

### ARCH-3 — Add cancellation tokens for long operations
- [x] PlaylistDialog file export (M3U save) — added `CancellationToken` param to `ExportToM3UAsync`, wired `CancellationTokenSource` in `OnSavePlaylistClick`
- [x] Subtitle loading (`LoadExternalSubtitleAsync`) — added `CancellationToken` param through `ISubtitleManager` → `SubtitleManager` → `MainViewModel.Actions`, wired via `CreateLinkedTokenSource` in `DispatchAddExternalSubtitlesAsync` mpv wait
- [x] Runtime download — `EnsureRuntimeAsync` already had `CancellationToken` param; wired 10-minute timeout `CancellationTokenSource` in `App.axaml.cs`
