# PlaybackStateManager — Architecture & Implementation Plan

## Problem

The play/pause icon state is set from **6+ scattered code paths** that fire in unpredictable order:

| # | Code Path | Method | Trigger |
|---|-----------|--------|---------|
| 1 | `MainWindow.OnMediaOpened` | `SetPlayPauseIconFromPlayerState()` | Player `Opened` event |
| 2 | `MainWindow.OnPlaybackStateChanged` | `SetPlayPauseIconFromPlayerState()` | Player `PlaybackStateChangedEvent` |
| 3 | `MainWindow.PropertyWatcher(IsPlaying)` | `UpdatePlayPauseIcon()` | ViewModel `PropertyChanged` |
| 4 | `MainWindow.PropertyWatcher(IsPaused)` | `UpdatePlayPauseIcon()` | ViewModel `PropertyChanged` |
| 5 | `ControlsBoxControl.OnPlayPause` | Optimistic toggle + `PlayPause()` | User click |
| 6 | `MainWindow.SyncPipPlayState` | `SetPlayingState()` | Same triggers as above |

All 6 paths run independently with no coordination, causing:
- Icon shows "Play" when video is actually playing
- Must press twice to pause (first press toggles icon, second actually triggers MpvPlayer)
- PIP icon desyncs from main window
- Race conditions when events fire from background thread via Dispatcher

## Solution: PlaybackStateManager

A centralized component that is the **single authority** for all playback state
transitions. All consumers read from / subscribe to it — never from the player
directly or from each other's notifications.

```
┌──────────────────────────────────────────────────────────────┐
│                        IMediaPlayer                          │
│  (MpvPlayer — the C backend wrapper, fires raw events)       │
└──────────────┬───────────────────────────────────────────────┘
               │ subscribes
               ▼
┌──────────────────────────────────────────────────────────────┐
│                   PlaybackStateManager                        │
│                                                              │
│  • Maintains authoritative State, Position, Volume, etc.     │
│  • Implements INotifyPropertyChanged for UI bindings         │
│  • Fires typed events for complex payloads                   │
│  • Single State→IsPlaying→IsPaused→IsStopped derivation      │
│  • Single path: player event → manager → all consumers       │
└──────────────┬───────────────────────────────────────────────┘
               │ one source, many consumers
     ┌─────────┼─────────────┬──────────────────┐
     ▼         ▼             ▼                  ▼
┌────────┐ ┌────────┐ ┌────────────┐ ┌────────────────┐
│Main    │ │Controls│ │PipWindow   │ │MainViewModel   │
│Window  │ │Box     │ │(icon,      │ │(IsPlaying,      │
│(events,│ │Control │ │ position,  │ │ IsPaused,       │
│overlays│ │(icon)  │ │ mute)      │ │ Position, State)│
│, PIP)  │ │        │ │            │ │                 │
└────────┘ └────────┘ └────────────┘ └────────────────┘
```

## File Changes

### NEW: `src/App/Application/Helpers/PlaybackStateManager.cs`

Central state hub that:
- Subscribes to all `IMediaPlayer` events (the one and only subscription point)
- Exposes:
  - **Properties** (with INotifyPropertyChanged): `State`, `IsPlaying`, `IsPaused`, `IsStopped`, `Position`, `Duration`, `NormalizedPosition`, `Volume`, `IsMuted`, `Speed`, `IsReplayMode`, `IsMediaLoaded`, `FilePath`, `VolumeMax`
  - **Events**: `StateChanged`, `PositionChanged`, `VolumeChanged`, `TrackListChanged`, `ChapterListChanged`, `LoopChanged`, `PlaylistChanged`, `MediaOpened`, `MediaEnded`, `Error`
- Provides `Refresh()` to query all state from player at once
- Handles the Stopped→ReplayMode transition automatically
- Implements `IDisposable` to unsubscribe all event handlers

### Phase 2: `ControlsBoxControl.axaml.cs`
- Remove `UpdatePlayPauseIcon()` (depends on ViewModel)
- Remove `SetPlayPauseIconFromPlayerState()` (depends on player state enum)
- Remove `SetReplayMode()` (depends on _replayMode flag)
- Replace with single `OnPlaybackStateChanged(PlaybackStateManager manager)` that reads `manager.IsPlaying` and `manager.IsReplayMode` to set icon in one place
- On `OnPlayPause` click: remove optimistic toggle, just call `_viewModel.PlayPause()` and let the manager event update the icon

### Phase 3: `MainWindow.Core.cs`
- Create `PlaybackStateManager` instance after player creation
- Wire it to player: `_stateManager = new PlaybackStateManager(player)`
- Subscribe to `_stateManager.StateChanged` → ControlsBox icon, overlays, PIP sync
- Subscribe to `_stateManager.PositionChanged` → seek bar, PIP position
- Subscribe to `_stateManager.MediaOpened` → UI visibility, header bar
- Subscribe to `_stateManager.MediaEnded` → replay overlay
- Remove direct player event subscriptions (they're now in the manager)
- Remove PropertyWatcher entries for IsPlaying/IsPaused (they fire from manager)
- Remove `UpdatePlayPauseFromState()` helper

### Phase 4: `MainViewModel.cs`
- Accept `PlaybackStateManager` instead of (or in addition to) `IMediaPlayer`
- Wire `OnPlaybackStateChanged` → read from `_stateManager.State` instead of `e.State`
- `RefreshState()` calls `_stateManager.Refresh()` then reads all properties
- Keep INotifyPropertyChanged for bindings, but derive values from manager
- Remove direct player event subscriptions (they're in the manager now)

### Phase 5: `MainWindow.Pip.cs`
- Replace `SyncPipPlayState()` with subscription to `_stateManager.StateChanged`
- Replace `SyncPipReplayMode()` with subscription to `_stateManager.MediaEnded`
- Replace `SyncPipPosition()` with subscription to `_stateManager.PositionChanged`
- PIP sync becomes automatic — no manual `SyncPip*` calls needed

### Phase 6: Cleanup
- Remove `UpdatePlayPauseFromState()` from MainWindow
- Remove redundant PropertyWatcher entries
- Remove SetReplayMode/SetPlayPauseIconFromPlayerState from ControlsBoxControl
- Remove UpdatePlayPauseIcon from ControlsBoxControl

## Benefits

| Concern | Before | After |
|---------|--------|-------|
| Icon state sources | 6 competing sources | 1 authoritative source |
| PIP sync | Manual `SyncPip*` calls after every event | Auto via manager events |
| Race conditions | Dispatcher posts from 3+ threads | Single coordinated dispatch |
| Adding tracks/subtitles | Must wire through MainWindow+ViewModel | Subscribe to manager events |
| Testing | Mock player + all consumers | Mock player + manager = 1 test surface |

## Migration Strategy

Implementation is done in phases, each independently testable:

1. **Create** PlaybackStateManager (done — no breaking changes)
2. **Wire** into MainWindow alongside existing code (dual path — verify no regression)
3. **Migrate** ControlsBoxControl to manager events
4. **Migrate** MainViewModel to manager
5. **Migrate** PipWindow to manager
6. **Remove** old code paths
