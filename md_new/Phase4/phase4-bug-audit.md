# Phase 4 — Complete Bug Audit & Remediation Plan

> **Date**: 2026-07-01 | **Audit Scope**: 22 source files, 3 service classes, all managers, XAML resources
> **Total Bugs Found**: **53** | **Architectural Flaws**: **5**

---

## Category Summary

| Cat | Count | Severity |
|-----|-------|----------|
| **A** Flyout Core | 9 | CRITICAL |
| **B** Track Selection | 11 | CRITICAL |
| **C** Modal Dismissal | 7 | HIGH |
| **D** Event Wiring | 6 | HIGH |
| **E** Data Binding | 5 | HIGH |
| **F** Initialization | 4 | MEDIUM |
| **G** Keyboard/Access | 4 | MEDIUM |
| **H** Styles/Visuals | 7 | LOW |

---

## A. Flyout Core (9 bugs)

### A1. BUBBLE KILLER — clicks inside flyout content dismiss the flyout
**File**: [FlyoutOverlayControl.axaml](file:///x:/Development/Cine_CSharp_DotNet/src/App/UI/Controls/FlyoutOverlayControl.axaml#L6-L11), [FlyoutOverlayControl.axaml.cs](file:///x:/Development/Cine_CSharp_DotNet/src/App/UI/Controls/FlyoutOverlayControl.axaml.cs#L75-L79)
**Root cause**: `PointerPressed` is on the full-screen `OverlayBackground`. Pointer events from all descendants (buttons, sliders inside `ContentContainer`) bubble up to this handler. No `e.Handled` guard or origin check. **Clicking a track button → dismisses the flyout before SelectCommand runs.**
**Fix**: Check `e.Source` — if it descends from `ContentContainer`, return without dismissing.

### A2. ContentContainer.Child never cleared on hide
**File**: [FlyoutOverlayControl.axaml.cs](file:///x:/Development/Cine_CSharp_DotNet/src/App/UI/Controls/FlyoutOverlayControl.axaml.cs#L68-L73)
**Root cause**: `HideContent()` sets `Opacity = 0` but never `ContentContainer.Child = null`. Stale content accumulates.
**Fix**: Add `ContentContainer.Child = null` in `HideContent()`.

### A3. Overlay background dim never restored after hide
**File**: [FlyoutOverlayControl.axaml.cs](file:///x:/Development/Cine_CSharp_DotNet/src/App/UI/Controls/FlyoutOverlayControl.axaml.cs#L68-L73)
**Root cause**: `ShowContent()` sets root `Opacity = 1` (dim visible), but `HideContent()` only sets `ContentContainer.Opacity = 0` — root `Opacity` stays 1. Dim never goes away.
**Fix**: Set root `Opacity = 0` in `HideContent()`.

### A4. Escape handler is dead code — overlay not focusable
**File**: [FlyoutOverlayControl.axaml](file:///x:/Development/Cine_CSharp_DotNet/src/App/UI/Controls/FlyoutOverlayControl.axaml#L10-L11), [FlyoutOverlayControl.axaml.cs](file:///x:/Development/Cine_CSharp_DotNet/src/App/UI/Controls/FlyoutOverlayControl.axaml.cs#L81-L89)
**Root cause**: `OverlayBackground` is a `Border` with `KeyDown` handler, but is never focusable — keyboard events don't reach it.
**Fix**: Make overlay focusable and `Focus()` it in `ShowContent()`.

### A5. Overlay background only 8% opacity — barely visible
**File**: [FlyoutOverlayControl.axaml](file:///x:/Development/Cine_CSharp_DotNet/src/App/UI/Controls/FlyoutOverlayControl.axaml#L7)
**Root cause**: `Background="#14000000"` = alpha 0x14 = ~8%. User cannot tell flyout is modal.
**Fix**: Change to `#80000000` (50%) or use theme token.

### A6. ContentContainer transitions only on ContentContainer, not on root
**File**: [FlyoutOverlayControl.axaml](file:///x:/Development/Cine_CSharp_DotNet/src/App/UI/Controls/FlyoutOverlayControl.axaml#L18-L22)
**Root cause**: `DoubleTransition` only on `ContentContainer.Opacity` — root overlay dim opacity has no transition.
**Fix**: Add `Transitions` on root border too, or set root opacity simultaneously.

### A7. IsHitTestVisible set synchronously — animation then non-interactive
**File**: [FlyoutOverlayControl.axaml.cs](file:///x:/Development/Cine_CSharp_DotNet/src/App/UI/Controls/FlyoutOverlayControl.axaml.cs#L72)
**Root cause**: `IsHitTestVisible = false` is set immediately, but opacity animation takes 180ms. Content becomes non-interactive while still visually present.
**Fix**: Delay `IsHitTestVisible = false` after animation completes.

### A8. Overlay positioned via Canvas but no size re-layout on window resize
**File**: [FlyoutOverlayControl.axaml.cs](file:///x:/Development/Cine_CSharp_DotNet/src/App/UI/Controls/FlyoutOverlayControl.axaml.cs#L26-L66)
**Root cause**: `ShowContent` measures anchor position once. If window resizes, flyout stays at old position.
**Fix**: Subscribe to `BoundsProperty` change, or use popup with proper placement.

### A9. Volume slider in flyout has no binding → non-functional
**File**: [ControlsBoxControl.axaml.cs](file:///x:/Development/Cine_CSharp_DotNet/src/App/UI/Screens/Shell/ControlsBoxControl.axaml.cs#L393-L401)
**Root cause**: `Slider` created with no `ValueChanged` handler and no `Binding` to `_viewModel.VolumeValue`. Moving slider does nothing; slider doesn't reflect current volume on open.
**Fix**: Add `ValueChanged` handler that calls `_viewModel.VolumeValue = ...` and set initial value from ViewModel.

---

## B. Track Selection (11 bugs)

### B1. Selection dot/text doesn't update — static snapshot
**File**: [TrackFlyoutBuilder.cs](file:///x:/Development/Cine_CSharp_DotNet/src/App/UI/Builders/TrackFlyoutBuilder.cs#L465-L548)
**Root cause**: `BuildTrackRow` reads `track.IsSelected` once at build time into local `isNowPlaying`, then never updates. `TrackMenuItem` fires `PropertyChanged` but no one subscribes.
**Fix**: Either close flyout after selection (simple) or use proper binding (complex).

### B2. "None" selection doesn't show as selected
**File**: [SubtitleManager.cs](file:///x:/Development/Cine_CSharp_DotNet/src/App/Application/Managers/SubtitleManager.cs#L719-L729), [TrackFlyoutBuilder.cs](file:///x:/Development/Cine_CSharp_DotNet/src/App/UI/Builders/TrackFlyoutBuilder.cs#L465-L548)
**Root cause**: "None" is `IsPseudoEntry = true`, `TrackIndex = -2`. `BuildTrackRow` checks `track.IsSelected` but "None" works via `item.RefreshSelection(true)` — however the dot was built at open time and never updates.
**Fix**: Close flyout after any selection, including "None".

### B3. Flyout never closes after track selection
**File**: All flyout openers (`OnSubtitlesClick`, `OnAudioClick`, `OnVideoMenuClick`, `OnEqualizerClick`)
**Root cause**: `SelectCommand` fires, manager updates state, but no code dismisses the flyout after selection.
**Fix**: Add close action to each `SelectCommand` or use routed event pattern.

### B4. Audio track thread safety — ObservableCollection mutated from background thread
**File**: [AudioManager.cs](file:///x:/Development/Cine_CSharp_DotNet/src/App/Application/Managers/AudioManager.cs#L531-L535)
**Root cause**: `OnPlayerTrackListChanged` fires on mpv callback thread. `RefreshAudioTracks` clears/adds to `AudioTracks` (ObservableCollection) without `Dispatcher.UIThread.Post`. Subtitle path does this correctly.
**Fix**: Wrap RefreshAudioTracks in `Dispatcher.UIThread.Post`.

### B5. VideoManager "None" entry is unreachable dead code
**File**: [VideoManager.cs](file:///x:/Development/Cine_CSharp_DotNet/src/App/Application/Managers/VideoManager.cs#L121-L139)
**Root cause**: `OnSelectVideo` returns immediately for `IsPseudoEntry` items. "None" and "Add Video Track…" are unreachable.
**Fix**: Add `!item.IsPseudoEntry` guard only for non-functional pseudo-entries, or implement actual None/Add behavior.

### B6. Duplicate video track source of truth (ViewModel vs Manager)
**File**: [MainViewModel.Tracks.cs](file:///x:/Development/Cine_CSharp_DotNet/src/App/Application/ViewModels/MainViewModel.Tracks.cs#L145-L210), [VideoManager.cs](file:///x:/Development/Cine_CSharp_DotNet/src/App/Application/Managers/VideoManager.cs#L116-L175)
**Root cause**: `MainViewModel.VideoTracks` is separate from `VideoManager.VideoTracks`. Selection logic in manager doesn't affect ViewModel's collection.
**Fix**: Consolidate to one source of truth.

### B7. Audio "Add Audio Track…" doesn't close flyout before file dialog
**File**: [AudioManager.cs](file:///x:/Development/Cine_CSharp_DotNet/src/App/Application/Managers/AudioManager.cs#L317-L327)
**Root cause**: `OnAddAudioAsync` doesn't call `DismissFlyoutAsync`. Subtitle path does. Causes window deadlock on some platforms.
**Fix**: Await `DismissFlyoutAsync?.Invoke()` like subtitle path does.

### B8. Open flyout doesn't refresh when track list changes
**File**: [TrackFlyoutBuilder.cs](file:///x:/Development/Cine_CSharp_DotNet/src/App/UI/Builders/TrackFlyoutBuilder.cs#L214-L229)
**Root cause**: `trackListPanel` built once. No subscription to `tracks.CollectionChanged`. New subtitles/audio tracks added externally are invisible until reopen.
**Fix**: Subscribe to `CollectionChanged` and call `RebuildTrackList`.

### B9. Codec badges/tooltips disabled (CS0234 comments)
**File**: [TrackFlyoutBuilder.cs](file:///x:/Development/Cine_CSharp_DotNet/src/App/UI/Builders/TrackFlyoutBuilder.cs#L491-L494, L502-L504, L508-L510)
**Root cause**: Codec badge creation, tooltip on dot, and tooltip on button are all commented out with `// CS0234` notes. Badges and tooltips don't appear.
**Fix**: Resolve the missing `Avalonia.Controls.ToolTip` reference and uncomment.

### B10. No visual "now playing" indicator update across managers
**File**: [SubtitleManager.cs](file:///x:/Development/Cine_CSharp_DotNet/src/App/Application/Managers/SubtitleManager.cs#L698-L706), [AudioManager.cs](file:///x:/Development/Cine_CSharp_DotNet/src/App/Application/Managers/AudioManager.cs#L295-L311), [VideoManager.cs](file:///x:/Development/Cine_CSharp_DotNet/src/App/Application/Managers/VideoManager.cs#L109-L120)
**Root cause**: `RefreshSelection(true)` is called on the selected track, updating `IsSelected`. But no UI code subscribes to `PropertyChanged` on `TrackMenuItem`. The flyout was built with a static snapshot.
**Fix**: Same as B1.

### B11. Subtitle icon not refreshed after "None" selection
**File**: [SubtitleOverlayControl.cs](file:///x:/Development/Cine_CSharp_DotNet/src/App/UI/Controls/Subtitle/SubtitleOverlayControl.axaml.cs#L67-L74), [SubtitleManager.cs](file:///x:/Development/Cine_CSharp_DotNet/src/App/Application/Managers/SubtitleManager.cs#L719-L729)
**Root cause**: `OnSelectSubtitle` calls `IsSubtitleEnabled = false` but doesn't call `RefreshIcon()`. `RefreshIcon` is only called from `MainWindow` on initial load.
**Fix**: Call `RefreshIcon()` in `OnSelectSubtitle` after updating state.

---

## C. Modal Dismissal (7 bugs)

### C1. Multiple OnBackgroundDismissed handlers accumulate → wrong MarkClosed calls
**File**: [ControlsBoxControl.axaml.cs](file:///x:/Development/Cine_CSharp_DotNet/src/App/UI/Screens/Shell/ControlsBoxControl.axaml.cs#L70, L109-L110), [SubtitleOverlayControl.axaml.cs](file:///x:/Development/Cine_CSharp_DotNet/src/App/UI/Controls/Subtitle/SubtitleOverlayControl.axaml.cs#L140-L141), [AudioTrackSelectorControl.axaml.cs](file:///x:/Development/Cine_CSharp_DotNet/src/App/UI/Controls/Audio/AudioTrackSelectorControl.axaml.cs#L107-L108)
**Root cause**: Volume handler subscribed once in `FlyoutManager` setter (always active). Subtitle/audio/video/e-quality handlers subscribe in click handlers. Volume handler fires on ALL dismissals.
**Fix**: Single dispatcher delegate instead of multi-subscription.

### C2. Escape closes flyout AND exits fullscreen in one press
**File**: [MainWindow.Input.cs](file:///x:/Development/Cine_CSharp_DotNet/src/App/UI/Shell/MainWindow.Input.cs#L50-L57)
**Root cause**: `CloseAll()` return value (reopen action) ignored. If flyout was open, still checks fullscreen and toggles it. User in fullscreen with flyout open → pressing Escape closes flyout AND exits fullscreen.
**Fix**: Check `CloseAll()` return value — if non-null (flyout was closed), skip fullscreen toggle.

### C3. ReopenFlyout silently fails after dialog
**File**: [SubtitleOverlayControl.axaml.cs](file:///x:/Development/Cine_CSharp_DotNet/src/App/UI/Controls/Subtitle/SubtitleOverlayControl.axaml.cs#L90-L99), [AudioTrackSelectorControl.axaml.cs](file:///x:/Development/Cine_CSharp_DotNet/src/App/UI/Controls/Audio/AudioTrackSelectorControl.axaml.cs#L75-L83)
**Root cause**: `ReopenFlyout` checks `_currentFlyoutContent != null`, but close action in `FlyoutManager` setter sets `_currentFlyoutContent = null`. After dialog closes, reopen hits null check and silently returns.
**Fix**: Don't null `_currentFlyoutContent` in close action — only null on explicit hide.

### C4. Fullscreen header not tracked as active flyout
**File**: [FullscreenHeaderControl.axaml.cs](file:///x:/Development/Cine_CSharp_DotNet/src/App/UI/Screens/Shell/FullscreenHeaderControl.axaml.cs#L89)
**Root cause**: `HasActiveFlyouts => _btnFlyout != null`. This is only set on menu open and nulled on close. But other track selectors in fullscreen not tracked.
**Fix**: Use same `FlyoutManager` pattern as ControlsBoxControl.

### C5. Primary menu (header) not tracked in _trackedFlyouts
**File**: [HeaderBarControl.axaml.cs](file:///x:/Development/Cine_CSharp_DotNet/src/App/UI/Screens/Shell/HeaderBarControl.axaml.cs#L37-L43)
**Root cause**: `BtnPrimaryMenu.Flyout.Opened` calls `TrackFlyoutOpened(null, EventArgs.Empty)` instead of passing sender. `_trackedFlyouts` never records the primary menu instance.
**Fix**: Pass correct sender or use FlyoutManager pattern.

### C6. HasActiveFlyouts unreliable across components
**File**: [ControlsBoxControl.axaml.cs](file:///x:/Development/Cine_CSharp_DotNet/src/App/UI/Screens/Shell/ControlsBoxControl.axaml.cs#L92), [FullscreenHeaderControl.axaml.cs](file:///x:/Development/Cine_CSharp_DotNet/src/App/UI/Screens/Shell/FullscreenHeaderControl.axaml.cs#L89), [HeaderBarControl.axaml.cs](file:///x:/Development/Cine_CSharp_DotNet/src/App/UI/Screens/Shell/HeaderBarControl.axaml.cs#L256-L266)
**Root cause**: Three different implementations. ControlsBox uses overlay opacity, FullscreenHeader uses `_btnFlyout != null`, HeaderBar uses `_trackedFlyouts.Any(...)`.
**Fix**: Single source of truth — query `FlyoutManager._openKey`.

### C7. FlyoutManager race condition — lock on dictionary but not on execution
**File**: [FlyoutManager.cs](file:///x:/Development/Cine_CSharp_DotNet/src/App/Application/Services/FlyoutManager.cs#L21-L22, L29-L31, L39-L50)
**Root cause**: `_entries` dictionary is locked, but `_openKey` read/write is inside the lock. The `TryClose()` action (touches overlay) runs **inside** the lock — potential deadlock if close action triggers event that re-enters FlyoutManager.
**Fix**: Move `entry.TryClose()` outside the lock.

---

## D. Event Wiring (6 bugs)

### D1. OnBackgroundDismissed += accumulates across opens
**File**: [SubtitleOverlayControl.axaml.cs](file:///x:/Development/Cine_CSharp_DotNet/src/App/UI/Controls/Subtitle/SubtitleOverlayControl.axaml.cs#L140-L141), [AudioTrackSelectorControl.axaml.cs](file:///x:/Development/Cine_CSharp_DotNet/src/App/UI/Controls/Audio/AudioTrackSelectorControl.axaml.cs#L107-L108), [ControlsBoxControl.axaml.cs](file:///x:/Development/Cine_CSharp_DotNet/src/App/UI/Screens/Shell/ControlsBoxControl.axaml.cs#L487-L488, L517-L518, L533-L534)
**Root cause**: Each click does `-=; +=` which should work. But multiple click handlers exist in different controls. `OnVolumeOverlayDismissed` is subscribed once and never removed.
**Fix**: Use a single per-overlay dispatcher or manage subscriptions with weak events.

### D2. ExternalFileDropped events tied to overlay but overlay may be null
**File**: [MainWindow.Wiring.cs](file:///x:/Development/Cine_CSharp_DotNet/src/App/UI/Shell/MainWindow.Wiring.cs#L86-L94)
**Root cause**: `_controlsBox.SubtitleOverlay?.ExternalFileDropped += (_, path) => ...`. If `SubtitleOverlay` is null, handler never fires. No error logging.
**Fix**: Add null guard with warning log, or make required.

### D3. DismissFlyoutAsync wired to subtitle but not audio path
**File**: [MainWindow.Initialization.cs](file:///x:/Development/Cine_CSharp_DotNet/src/App/UI/Shell/MainWindow.Initialization.cs#L291-L303)
**Root cause**: `subMgr.DismissFlyoutAsync` is set. `audMgr.DismissFlyoutAsync` is set. But audio path in `OnAddAudioAsync` doesn't call it (B7).
**Fix**: Audio `OnAddAudioAsync` should invoke `DismissFlyoutAsync?.Invoke()` before opening dialog.

### D4. No PropertyChanged subscription on TrackMenuItem in flyout rows
**File**: [TrackFlyoutBuilder.cs](file:///x:/Development/Cine_CSharp_DotNet/src/App/UI/Builders/TrackFlyoutBuilder.cs#L465-L548), [TrackMenuItem.cs](file:///x:/Development/Cine_CSharp_DotNet/src/App/Application/Models/TrackMenuItem.cs#L73-L76)
**Root cause**: `TrackMenuItem` fires `PropertyChanged` for `IsSelected`/`DisplayOpacity`. `BuildTrackRow` builds a static `Button` with no binding or subscription. The `INotifyPropertyChanged` infrastructure is fully wired but completely unused.
**Fix**: Either use bindings or close/reopen flyout on selection.

### D5. Multiple ReplayRequested subscriptions accumulate
**File**: [MainWindow.Wiring.cs](file:///x:/Development/Cine_CSharp_DotNet/src/App/UI/Shell/MainWindow.Wiring.cs#L74)
**Root cause**: `_replayOverlay.ReplayRequested +=` in `InitializeWiring` (called once). But if re-initialized, would accumulate.
**Fix**: Use `-=` before `+=`.

### D6. PreviewPopover offset hardcoded in XAML
**File**: [SeekBarControl.axaml](file:///x:/Development/Cine_CSharp_DotNet/src/App/UI/Controls/SeekBar/SeekBarControl.axaml#L77-L92)
**Root cause**: `Margin="0,-34,0,0"` magic number. Breaks on different DPI/font sizes.
**Fix**: Compute offset in code-behind or use themed resource.

---

## E. Data Binding (5 bugs)

### E1. Volume slider not bound to ViewModel
**File**: [ControlsBoxControl.axaml.cs](file:///x:/Development/Cine_CSharp_DotNet/src/App/UI/Screens/Shell/ControlsBoxControl.axaml.cs#L393-L401)
**Fix**: Add `ValueChanged` handler + initial value set.

### E2. Volume percent label never updates
**File**: [ControlsBoxControl.axaml.cs](file:///x:/Development/Cine_CSharp_DotNet/src/App/UI/Screens/Shell/ControlsBoxControl.axaml.cs#L381-L389)
**Root cause**: `volumePercentLabel.Text = "100%"` hardcoded at build time. Never updated on slider change or from ViewModel.
**Fix**: Update in slider ValueChanged.

### E3. Equalizer delay slider not bound to manager value
**File**: [AudioEqualizerFlyout.axaml.cs](file:///x:/Development/Cine_CSharp_DotNet/src/App/UI/Controls/Audio/AudioEqualizerFlyout.axaml.cs#L140-L146)
**Root cause**: DelaySlider `PropertyChanged` updates `DelayLabel.Text` but not `_manager.AudioDelay`. Only `LoadedFromManager` syncs once.
**Fix**: Add `_manager.AudioDelay = DelaySlider.Value` in the PropertyChanged handler.

### E4. Equalizer preset "selected" class lost on reopen
**File**: [AudioEqualizerFlyout.axaml.cs](file:///x:/Development/Cine_CSharp_DotNet/src/App/UI/Controls/Audio/AudioEqualizerFlyout.axaml.cs#L176-L205)
**Root cause**: Flyout is a new `UserControl` instance each open. `OnPresetClick` adds CSS class to button, but next open creates new buttons with no `selected` class. `LoadFromManager` doesn't check which preset matches.
**Fix**: In `LoadFromManager`, compare current bands against preset definitions and mark matching button.

### E5. Equalizer preset button traversal broken
**File**: [AudioEqualizerFlyout.axaml.cs](file:///x:/Development/Cine_CSharp_DotNet/src/App/UI/Controls/Audio/AudioEqualizerFlyout.axaml.cs#L183-L198)
**Root cause**: `SlidersPanel.Parent` is a `ScrollViewer`, not the `StackPanel` containing preset `WrapPanel`. The `foreach` over `parent.Children` iterates wrong children. Preset deselection doesn't work.
**Fix**: Store preset buttons in a `List<Button>` field.

---

## F. Initialization (4 bugs)

### F1. Overlay cached before window is ready
**File**: [ControlsBoxControl.axaml.cs](file:///x:/Development/Cine_CSharp_DotNet/src/App/UI/Screens/Shell/ControlsBoxControl.axaml.cs#L108)
**Root cause**: `MainWindow.GetOverlay(this)` called in `FlyoutManager` setter, which may run before visual tree attachment. Returns null.
**Fix**: Defer overlay lookup to first `ShowContent` call.

### F2. SubtitleOverlayControl/FullscreenHeader.axaml data context may not propagate
**File**: [SubtitleOverlayControl.axaml.cs](file:///x:/Development/Cine_CSharp_DotNet/src/App/UI/Controls/Subtitle/SubtitleOverlayControl.axaml.cs#L60-L64), [FullscreenHeaderControl.axaml.cs](file:///x:/Development/Cine_CSharp_DotNet/src/App/UI/Screens/Shell/FullscreenHeaderControl.axaml.cs#L47-L50)
**Root cause**: Both use `DataContextChanged` event to set `_viewModel`. If DataContext is set before handler is attached, `_viewModel` stays null.
**Fix**: Also check `DataContext` in constructor if already set.

### F3. FlyoutManager set in FlyoutManager setter but reopened flyout content null
**File**: [AudioTrackSelectorControl.axaml.cs](file:///x:/Development/Cine_CSharp_DotNet/src/App/UI/Controls/Audio/AudioTrackSelectorControl.axaml.cs#L88-L96), [SubtitleOverlayControl.axaml.cs](file:///x:/Development/Cine_CSharp_DotNet/src/App/UI/Controls/Subtitle/SubtitleOverlayControl.axaml.cs#L105-L113)
**Root cause**: `value?.Register()` registers close action that sets `_currentFlyoutContent = null`. `ReopenFlyout` checks `_currentFlyoutContent != null` → fails.
**Fix**: Close action should only hide, not null the content.

### F4. Fullscreen header track selectors not connected to overlay
**File**: [FullscreenHeaderControl.axaml.cs](file:///x:/Development/Cine_CSharp_DotNet/src/App/UI/Screens/Shell/FullscreenHeaderControl.axaml.cs#L98-L100)
**Root cause**: `FullscreenSubOverlay.FlyoutManager = value; FullscreenAudioOverlay.FlyoutManager = value;` — but no `MainWindow.GetOverlay()` call for these child controls. Their `_overlay` stays null.
**Fix**: Ensure overlay propagation to child controls.

---

## G. Keyboard & Accessibility (4 bugs)

### G1. Keyboard navigation in flyout — no focus management
**File**: [FlyoutOverlayControl.axaml.cs](file:///x:/Development/Cine_CSharp_DotNet/src/App/UI/Controls/FlyoutOverlayControl.axaml.cs#L53-L65)
**Root cause**: `ShowContent` doesn't set focus into the flyout content. Tab/arrow navigation doesn't target the flyout.
**Fix**: Set focus to first focusable element after showing.

### G2. Close button in TrackFlyoutBuilder header has no close action
**File**: [TrackFlyoutBuilder.cs](file:///x:/Development/Cine_CSharp_DotNet/src/App/UI/Builders/TrackFlyoutBuilder.cs#L72-L93)
**Root cause**: Close button is created but never wired — no `Click` handler that dismisses the parent flyout.
**Fix**: Accept `Action? closeAction` param and wire to header close button.

### G3. Search box in TrackFlyoutBuilder not wired to dismiss parent
**File**: [TrackFlyoutBuilder.cs](file:///x:/Development/Cine_CSharp_DotNet/src/App/UI/Builders/TrackFlyoutBuilder.cs#L157-L166)
**Root cause**: Search box `KeyDown` handler checks `Escape` but only clears text. Doesn't close the flyout.
**Fix**: Also trigger dismiss action on Escape when search box is empty.

### G4. FlyoutOverlayControl not setting AutomationProperties
**Root cause**: No `AutomationProperties.Name` or `HelpText` on the overlay. Screen readers can't identify the modal state.
**Fix**: Add automation properties to both overlay background and content container.

---

## H. Styles & Visuals (7 bugs)

### H1. MenuStyles.axaml partially overrides popover chrome
**File**: [MenuStyles.axaml](file:///x:/Development/Cine_CSharp_DotNet/src/App/UI/Resources/MenuStyles.axaml#L1-L9)
**Root cause**: Loaded after App.axaml. Only sets `Background`, so shared popover chrome loses border, radius, padding, shadow.
**Fix**: Delete or merge into main resource dictionary.

### H2. HeaderBar still uses native Flyout (two popover systems)
**File**: [HeaderBarControl.axaml](file:///x:/Development/Cine_CSharp_DotNet/src/App/UI/Screens/Shell/HeaderBarControl.axaml#L27-L97, L129-L141)
**Root cause**: BtnOpenMenu and BtnPrimaryMenu use Avalonia `Flyout` while all other flyouts use `FlyoutOverlayControl`. Two popover systems with different placement/animation/dismissal behavior.
**Fix**: Migrate to FlyoutOverlayControl.

### H3. Duplicate Button focus-visible styles
**File**: [App.axaml](file:///x:/Development/Cine_CSharp_DotNet/src/App/UI/Resources/App.axaml#L489-L493, L606-L610)
**Root cause**: `Button:focus-visible /template/ ContentPresenter` defined twice with different colors.
**Fix**: Consolidate to one definition.

### H4. Duplicate Border.OsdNotificationStyle
**File**: [App.axaml](file:///x:/Development/Cine_CSharp_DotNet/src/App/UI/Resources/App.axaml#L461-L479, L954-L965)
**Fix**: Merge into one definition block.

### H5. IsVisible="False" overlays fighting opacity animations
**File**: [PauseOverlayControl.axaml](file:///x:/Development/Cine_CSharp_DotNet/src/App/UI/Controls/Indicators/PauseOverlayControl.axaml#L5-L21), [OsdNotificationControl.axaml](file:///x:/Development/Cine_CSharp_DotNet/src/App/UI/Controls/Indicators/OsdNotificationControl.axaml#L8-L26)
**Root cause**: Several overlays start with `IsVisible="False"`. In this project, IsVisible destroys the render tree, so opacity transitions don't work on first show.
**Fix**: Keep visible, toggle with Opacity + IsHitTestVisible.

### H6. DragDropOverlayControl hardcodes accent color #4aa3ff
**File**: [DragDropOverlayControl.axaml](file:///x:/Development/Cine_CSharp_DotNet/src/App/UI/Controls/Indicators/DragDropOverlayControl.axaml#L22-L27)
**Fix**: Use `AppAccent` brush from Colors.axaml.

### H7. ChapterPreviewPopover magic offset
**File**: [SeekBarControl.axaml](file:///x:/Development/Cine_CSharp_DotNet/src/App/UI/Controls/SeekBar/SeekBarControl.axaml#L77-L92)
**Root cause**: `Margin="0,-34,0,0"` hardcoded negative offset.
**Fix**: Compute from font size or use themed resource.

---

## Architectural Flaws (5)

### AF1. State ownership split across 4 systems
**Files**: `FlyoutOverlayControl._isOpen` / `FlyoutManager._openKey` / `ControlsBoxControl._activeFlyoutKey` / `HeaderBarControl._trackedFlyouts`
**Impact**: Open/close state tracked in 4 places, all drift out of sync.

### AF2. Flyout content is static snapshot, not reactive
**Files**: `TrackFlyoutBuilder.BuildTrackRow` → local `isNowPlaying` / `TrackMenuItem` `INotifyPropertyChanged` never consumed
**Impact**: All selection visual state is stale until flyout is reopened.

### AF3. No single overlay ownership — any control can show/hide shared overlay
**Files**: 5+ sources call `_flyoutOverlay.ShowContent()` / `HideContent()` independently
**Impact**: Close action cleanups conflict. Race between DismissOthers and ShowContent.

### AF4. ControlsBoxControl combines flyout source + FlyoutManager owner + event router
**Impact**: Tight coupling. Can't test flyout sources independently. Volume/subtitle/audio/video/chapters all in one ~730-line file.

### AF5. AudioManager collection mutation not on UI thread
**Impact**: Production crash on track change events. Subtitle path already has the fix.

---

## T-shirt Sizing

**Critical (blocks core UX)** — 16 bugs:

A1, A9, B1, B2, B3, B4, B5, B7, B8, B10, C1, C2, C7, D4, E1, AF5

**Medium (confusing UX, partial breakage)** — 22 bugs:

A2, A3, A5, A7, B11, C3, C4, C5, C6, D1, D3, D6, E2, E3, E4, E5, F1, F2, F3, G1, G2, H5

**Low (polish, consistency)** — 15 bugs:

A4, A6, A8, B6, B9, D2, D5, F4, G3, G4, H1, H2, H3, H4, H6, H7

---

## Next-Step Recommendation

Fix **Critical** tier first — especially **A1** (bubbling dismiss) + **B1/B2/B3** (close flyout on selection) which together make track selection actually work. See repair plan in `phase4-repair-plan.md`.

---

## Architectural Flaws — Implementation Plan

The 5 architectural flaws identified in this audit have a separate detailed plan with step-by-step instructions:

📄 [phase4-architectural-flaws.md](phase4-architectural-flaws.md)

| AF | Summary | Effort | Status |
|----|---------|--------|--------|
| **AF1** | Unify state ownership across 4 systems | Small | Ready |
| **AF2** | Make flyout content reactive via PropertyChanged | Medium | Ready |
| **AF3** | Route all Show/HideContent through FlyoutManager | Medium | Blocked by AF1 |
| **AF4** | Extract ControlsBoxControl into focused controls | Large | Blocked by AF3 |
| **AF5** | AudioManager collection on UI thread | Small | ✅ Done (B4)
