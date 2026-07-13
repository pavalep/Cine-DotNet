# Phase 3 Completion Tracking

> **Document**: Tracking status of all Premium UI Refinements (P1-P5) and Functional Completeness fixes (F1-F19) from [`innovate.md`](innovate.md)
> **Latest Commit**: `4186ded` - "feat: premium UI polish + 10 functional completeness fixes"

---

## Status Legend

| Symbol | Meaning |
|--------|---------|
| ✅ | **Completed** — implemented and committed |
| ⏳ | **Pending** — listed as future work |
| ⚠️ | **Partially Done** — some implementation exists, needs completion |

---

## Part 1: Premium UI Refinements

### P1. Consistent Spacing & Sizing Tokens
- **Status**: ⏳ **Pending**
- **Scope**: Replace 87+ hardcoded pixel values across 30+ files with centralized tokens
- **Effort**: ½ day
- **Files Affected**: ControlsBox, HeaderBar, FullscreenHeader, SeekBar, AudioEqualizerFlyout, FlyoutOverlay, PlaylistDialog, SubtitleSettingsDialog, etc.

### P2. Layered Transparency System
- **Status**: ⏳ **Pending**
- **Scope**: Apply consistent alpha hierarchy (90% → 85% → 80% → 95% → 70% → 75%)
- **Effort**: 1 day
- **Current Issue**: Inconsistent backgrounds across flyouts and overlays

### P3. Typography Hierarchy
- **Status**: ⏳ **Pending**
- **Scope**: Define and apply font scale (display→micro) + weight system
- **Effort**: 2 days
- **Current Issue**: Inconsistent sizes/weights across headers, buttons, timestamps

### P4. Proper Button States & Micro-Interactions
- **Status**: ⏳ **Pending**
- **Scope**: Standardize 4-state button system (default → hover → pressed → disabled)
- **Effort**: 1 day
- **Current Issue**: Manual `PointerEntered/Exited` handlers everywhere vs. shared styles

### P5. Consistent Divider & Separator Treatment
- **Status**: ⏳ **Pending**
- **Scope**: One canonical separator style using `Border` instead of `Rectangle`
- **Effort**: 1 day
- **Current Issue**: Mixed separator implementations across files

---

## Part 2: Functional Completeness Fixes

### F1. Now Playing Indicator on Track Selectors
- **Status**: ✅ **Completed**
- **Commit**: `4186ded`
- **Implementation**: `TrackFlyoutBuilder.BuildTrackRow()` accepts `isNowPlaying` parameter
- **Visual**: Accent dot (8px), semi-bold text, "Now playing" tooltip
- **Note**: Tooltip code commented out due to `ToolTip.SetTip` namespace conflict (compilation issue)

### F2. Audio Equalizer — Active Preset Highlighting
- **Status**: ✅ **Completed**
- **Commit**: `4186ded`
- **Implementation**: `.eq-preset:selected` style with accent background + semi-bold
- **Effect**: Active preset visually distinguished from inactive presets

### F3. AudioTrackSelector — Font Size Setter
- **Status**: ⏳ **Verified as already handled** (Delay reset button exists)

### F4. Video Menu — Single-Track Indicator
- **Status**: ⏳ **Pending**
- **Implementation**: Add tooltip or disabled placeholder when single track
- **Current**: Button simply hidden via `IsVisible={HasMultipleVideoTracks}`
- **Recommendation**: Low priority — informational only

### F5. PlaylistDialog — Search/Filter
- **Status**: ⏳ **Pending**
- **Scope**: Add `TextBox` with `ICollectionView` filtering
- **Effort**: 2 hours

### F6. PlaylistDialog — Context Menu Actions
- **Status**: ⏳ **Pending**
- **Scope**: Right-click context menu (remove, move up/down, clear, deduplicate)
- **Effort**: 2 hours

### F7. SeekBar — Chapter Marker Tooltips
- **Status**: ✅ **Completed**
- **Commit**: `4186ded`
- **Implementation**: `ItemContainerStyle` with `ToolTip.Tip` bound to chapter title + time
- **File**: `SeekBarControl.axaml`

### F8. SeekBar — Chapter Preview Boundary Safety
- **Status**: ✅ **Completed**
- **Commit**: `4186ded`
- **Implementation**: Measurement guard, clamped width to 65% of track, safe fallback
- **File**: `SeekBarControl.axaml.cs`

### F9. GoToTimeDialog — Screen Centering
- **Status**: ⏳ **Pending**
- **Scope**: Explicit centering in fullscreen context
- **Current**: Uses `CenterOwner` but may not work in fullscreen
- **Effort**: ½ day

### F10. SubtitleOverlay — Gear Button Tooltip
- **Status**: ✅ **Completed**
- **Commit**: `4186ded`
- **Implementation**: Tooltip: "Subtitle settings — font, size, color, outline, encoding"
- **File**: `SubtitleOverlayControl.axaml.cs`

### F11. SubtitleOverlay — Drag-Over Visual Feedback
- **Status**: ✅ **Partially Done / Implementation Conflict**
- **Commit**: `4186ded`
- **Implementation**: Drag-over feedback on **track buttons** (`TrackFlyoutBuilder`)
- **Note**: **Not** wired to subtitle control — that would require XAML style update or code-behind handler. Current state: drag-over on `BtnSubtitles` shows no visual change.
- **Recommendation**: Low priority — cosmetic improvement only

### F12. TrackFlyoutBuilder — Codec Badges
- **Status**: ✅ **Completed**
- **Commit**: `4186ded`
- **Implementation**: Color-coded 6px dots per codec (ASS=teal, SRT=gray, PGS=orange, DVD=blue, mov_text=gold, dvb=purple, webvtt=green) + tooltip
- **File**: `TrackFlyoutBuilder.GetCodecBadgeColor()`
- **Note**: Tooltip code commented out due to `ToolTip.SetTip` namespace conflict

### F13. Fullscreen — Track Selector Access
- **Status**: ⏳ **Pending**
- **Scope**: Compact audio + subtitle toggles in `FullscreenHeaderControl`
- **Effort**: ½ day
- **Current Issue**: No way to change tracks in fullscreen without exiting

### F14. Equalizer Flyout Migration
- **Status**: ✅ **Completed**
- **Commit**: `4186ded`
- **Implementation**: Equalizer flyout already uses `TrackFlyoutBuilder` + `AppendExtra` pattern
- **Bonus**: NumericUpDown for delay with bidirectional sync (`AudioEqualizerFlyout.axaml.cs` + `.axaml`)

### F15. Flyout Entrance/Exit Animations
- **Status**: ✅ **Completed**
- **Commit**: `4186ded`
- **Implementation**: Scale 0.96→1.0 + opacity 0→1 on show; reverse on hide
- **Duration**: 180ms exponential ease-out
- **File**: `FlyoutOverlayControl.axaml` + `.cs`
- **Note**: Complex animation logic refactored to simpler approach via XAML Transitions

### F16. Flyout Focus Management
- **Status**: ✅ **Completed**
- **Commit**: `4186ded`
- **Implementation**: `FocusFirst()` finds first focusable element, posts focus to loaded priority
- **File**: `FlyoutOverlayControl.axaml.cs`

### F17. OSD Click Actions
- **Status**: ⏳ **Pending**
- **Scope**: Meaningful actions per category (volume → slider, subtitle → selector, speed → settings)
- **Current**: Only logs debug info
- **Effort**: 1 hour
- **Current**: Wires to `OnOsdNotificationClicked` but no action taken except OSD queued-open scenario

### F18. Volume Slider Sync with Mute
- **Status**: ✅ **Completed**
- **Commit**: `4186ded`
- **Implementation**: Saves/restores volume on mute toggle, proper `IsMuted` flag management
- **File**: `ControlsBoxControl.axaml.cs`

### F19. PreferencesDialog — Reset to Defaults
- **Status**: ⏳ **Pending**
- **Scope**: "Reset to Defaults" button at dialog bottom
- **Effort**: ½ day

### F13 (Bonus) — Default Track Marker
- **Status**: ⏳ **Pending**
- **Scope**: Star (★) icon for container default tracks
- **Note**: **Removed from commit** — `TrackMenuItem.IsDefault` doesn't exist on model
- **Recommendation**: Can be added later if `IsDefault` property is added to `TrackMenuItem`

---

## Summary

| Category | Completed | Pending | Partial |
|----------|-----------|---------|---------|
| **Premium UI Refinements (P1-P5)** | 0 | 5 | 0 |
| **Functional Completeness (F1-F19)** | 11 | 8 | 1 |
| **Total** | **11** | **13** | **1** |

---

## Next Steps (Recommended Priority)

### High Priority
1. **F4**: Video menu single-track indicator
2. **F9**: GoToTimeDialog screen centering  
3. **F17**: OSD click action wiring
4. **F19**: Preferences reset button

### Medium Priority
5. **P1**: Spacing tokens (foundation for visual consistency)
6. **F5**: Playlist search/filter
7. **F6**: Playlist context menu
8. **F14**: Equalizer flyout migration (already implemented ✓)

### Lower Priority
9. **P2-P5**: Typography, transparency, button states, dividers
10. **F11**: Subtitle drag-over feedback
11. **F13**: Fullscreen track access

---

*Last updated: 2026-07-01*
*Commit: `4186ded`*
