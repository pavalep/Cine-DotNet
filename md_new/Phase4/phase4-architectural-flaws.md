# Phase 4 — Architectural Flaws (AF1–AF5)

> **Status**: Plan document — ready for implementation  
> **Scope**: 5 structural refactors beyond surface bugs  
> **Depends on**: All Category A–H fixes applied

---

## AF1. State ownership split across 4 systems

**Problem**: Flyout open/close state is tracked in 4 places — `FlyoutOverlayControl.IsOpen`, `FlyoutManager._openKey`, `ControlsBoxControl._activeFlyoutKey`, `HeaderBarControl._trackedFlyouts` — all drift out of sync.

**Current state**:
- `FlyoutManager.HasActiveFlyouts` → `_openKey != null` — **single source of truth** ✓
- `HeaderBarControl.HasActiveFlyouts` → delegates to `FlyoutManager` ✓
- `FullscreenHeaderControl.HasActiveFlyouts` → delegates to `FlyoutManager` ✓
- `ControlsBoxControl._activeFlyoutKey` — **still tracked independently**
- `FlyoutOverlayControl.IsOpen` → heuristic via `ContentContainer.Opacity > 0.5` — **still exists**

### Steps

1. **Remove `_activeFlyoutKey` from ControlsBoxControl**
   - File: [ControlsBoxControl.axaml.cs](file:///x:/Development/Cine_CSharp_DotNet/src/App/UI/Screens/Shell/ControlsBoxControl.axaml.cs)
   - Delete the `_activeFlyoutKey` field (line 34)
   - Replace all `_activeFlyoutKey = "key"` assignments with `_flyoutManager?.DismissOthers("key")` or just remove if redundant
   - Replace `_activeFlyoutKey != null` guard (line 503) with `_flyoutManager?.HasActiveFlyouts == true`

2. **Remove or delegate `FlyoutOverlayControl.IsOpen`**
   - File: [FlyoutOverlayControl.axaml.cs](file:///x:/Development/Cine_CSharp_DotNet/src/App/UI/Controls/FlyoutOverlayControl.axaml.cs)
   - Remove the heuristic `IsOpen` property (line 29)
   - Add `IsOpen` that accepts a `Func<bool>` callback or queries a shared state
   - Or: just remove it if nothing consumes it

---

## AF2. Flyout content is static snapshot, not reactive

**Problem**: `TrackFlyoutBuilder.BuildTrackRow` creates `TrackMenuItem` instances with `INotifyPropertyChanged` support (`IsChecked`, `IsEnabled`, `Text`), but the actual `MenuItem` controls in the flyout read these values **once at build time** and never update. Selection visual state is stale until flyout is reopened.

**Current state**:
- B8 added `CollectionChanged` handler to rebuild track list when tracks change — helps with add/remove
- But **selection state** (IsChecked checkmark, badge icon) still only renders at build time

### Steps

1. **Subscribe TrackMenuItem property changes to UI** in `BuildTrackRow`
   - File: [TrackFlyoutBuilder.cs](file:///x:/Development/Cine_CSharp_DotNet/src/App/UI/Builders/TrackFlyoutBuilder.cs)
   - After creating `menuItem` and binding `IsChecked`, add:
     ```csharp
     track.PropertyChanged += (_, e) =>
     {
         if (e.PropertyName == nameof(TrackMenuItem.IsChecked))
             menuItem.IsChecked = track.IsChecked;
         else if (e.PropertyName == nameof(TrackMenuItem.IsEnabled))
             menuItem.IsEnabled = track.IsEnabled;
         else if (e.PropertyName == nameof(TrackMenuItem.Text))
             menuItem.Header = track.Text;
     };
     ```
   - This ensures selection state updates live without reopening the flyout

2. **Verify TrackMenuItem.Text includes codec badge**
   - `TrackMenuItem.Text` setter already appends codec badge via `TextWithCodec` property
   - Confirm `PropertyChanged` fires correctly for `Text`

---

## AF3. No single overlay ownership

**Problem**: 5+ sources call `_flyoutOverlay.ShowContent()` / `HideContent()` directly. Close-action cleanups conflict. Race between `DismissOthers()` and `ShowContent()`.

### Steps

1. **Add `ShowFlyout` / `HideFlyout` to FlyoutManager**
   - File: [FlyoutManager.cs](file:///x:/Development/Cine_CSharp_DotNet/src/App/Application/Services/FlyoutManager.cs)
   - Add methods that combine `DismissOthers()` + content show/hide in one atomic operation:

     ```csharp
     public void ShowFlyout(string key, Control anchor, Control content, bool placeAbove = true, 
         Action<Control, Control, bool>? showContent = null)
     {
         DismissOthers(key);
         showContent?.Invoke(anchor, content, placeAbove);
     }

     public void HideFlyout(string key, Action? hideContent = null)
     {
         MarkClosed(key);
         hideContent?.Invoke();
     }
     ```

2. **Update all callers** to use `FlyoutManager.ShowFlyout` / `HideFlyout`
   - SubtitleOverlayControl.axaml.cs — gear button, overlay dismiss
   - AudioTrackSelectorControl.axaml.cs — overlay dismiss
   - ControlsBoxControl.axaml.cs — volume, equalizer, video menu, chapters
   - FullscreenHeaderControl.axaml.cs — menu flyout

3. **Remove direct `_overlay.ShowContent/HideContent` calls** from individual controls where possible

---

## AF4. ControlsBoxControl combines too many concerns

**Problem**: ControlsBoxControl (~730 lines) is simultaneously:
- Flyout source (opens equalizer, video menu, chapters, volume)
- FlyoutManager owner (holds the `FlyoutManager` instance)
- Event router (wires keyboard, click, and lifecycle events)
- Cannot test flyout sources independently

### Steps

1. **Extract volume flyout** into `VolumeFlyoutControl`
   - Move volume slider, label, mute toggle and their event wiring
   - File: new `UI/Controls/VolumeFlyoutControl.axaml` + `.cs`

2. **Extract chapters flyout** into `ChaptersFlyoutControl`
   - Move chapters button + popover wiring
   - File: new `UI/Controls/ChaptersFlyoutControl.axaml` + `.cs`

3. **Reduce ControlsBoxControl** to pure composition
   - Replace inline XAML with `<local:VolumeFlyoutControl />` etc.
   - Keep only the FlyoutManager reference and layout

---

## AF5. AudioManager collection mutation not on UI thread

**Problem**: `AudioManager.RefreshAudioTracks()` clears and repopulates `AudioTracks` (ObservableCollection) from a background thread.

**Status**: ✅ **Already fixed** by B4 — `CheckAccess()` + `Dispatcher.UIThread.Post()` guard added at the top of `RefreshAudioTracks()`. Verify:

- File: [AudioManager.cs](file:///x:/Development/Cine_CSharp_DotNet/src/App/Application/Managers/AudioManager.cs#L348-L350)
- Lines 348–350 check `Dispatcher.UIThread.CheckAccess()` and post to UI thread if needed

No additional work needed.

---

## Summary

| AF | Description | Effort | Dependencies | Status |
|----|-------------|--------|-------------|--------|
| **AF1** | Unify state ownership | Small | None | Ready |
| **AF2** | Reactive flyout content | Medium | None | Ready |
| **AF3** | Single overlay ownership | Medium | AF1 | Blocked by AF1 |
| **AF4** | Extract ControlsBoxControl | Large | AF3 | Blocked by AF3 |
| **AF5** | UI thread AudioManager | Small | B4 | ✅ Done |
