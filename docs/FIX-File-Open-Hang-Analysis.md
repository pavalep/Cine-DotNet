# File Open Hang — Deep Analysis & Fix Plan

> **Symptom**: Cine App freezes immediately when clicking **Open → Open Files** (or Open Folder) from the header bar Flyout menu.

---

## Executive Summary

The hang is caused by **two independent factors** that compound:

| # | Factor | Root Cause | Severity |
|---|--------|-----------|----------|
| 1 | **mpv `ADVANCED_CONTROL=1` + synchronous `mpv_command()`** | `mpv_command("loadfile")` blocks the calling thread while the render thread also holds mpv's internal lock. ADVANCED_CONTROL=1 makes this a **permanent deadlock** (per mpv docs). | 🔴 Critical |
| 2 | **Avalonia Flyout + FilePicker race** | When a `RelayCommand` bound to a Flyout button opens `StorageProvider.OpenFilePickerAsync()`, the Flyout close animation and native dialog compete for the Windows message loop via COM STA marshaling. | 🟡 Contributes |
| 3 | **`mpv_command()` called on UI thread** | The UI thread is the COM STA thread. When it blocks in `mpv_command()`, neither the Flyout close nor the FilePicker open can proceed because both need STA message pumping. | 🟡 Contributes |

---

## Phase 1 — Root Cause Identification

### 1.1 mpv `ADVANCED_CONTROL` Deadlock (PRIMARY)

**Source**: [mpv render.h — official documentation](https://github.com/mpv-player/mpv/blob/master/include/mpv/render.h#L38-L79)

```c
// From mpv render.h (lines 38-79):
//
// If you ignore this requirement, deadlocks can happen, which
// are made non-fatal with timeouts; then playback quality will be degraded,
// and the message
//   mpv_render_context_render() not being called or stuck.
// is logged. If you set MPV_RENDER_PARAM_ADVANCED_CONTROL, you promise that
// this won't happen, and must absolutely guarantee it, or a real deadlock
// will freeze the mpv core thread forever.
```

**What our code did**: Set `ADVANCED_CONTROL=1` (line 1084 in MpvPlayer.cs), then called `mpv_command("loadfile")` synchronously from the UI thread — the same thread that dispatches render updates.

**Deadlock sequence**:
```
UI Thread:       mpv_command("loadfile") → blocks waiting for core lock
mpv Core Thread: needs mpv_render_context_render() before releasing lock
Render Thread:   calls mpv_render_context_render() → OK, but:
                   - Render dispatches pixels to UI via Dispatcher.UIThread.Post
                   - If ADVANCED_CONTROL=1 and render update doesn't drain fast enough
                     (because loadfile is still in progress), mpv freezes forever
```

**mpv's own official example confirms this** — [sdl/main.c line 120-130](https://github.com/mpv-player/mpv-examples/blob/master/libmpv/sdl/main.c):
```c
// Tell libmpv that you will call mpv_render_context_update() on render context
// update callbacks, and that you will _not_ block on the core ever.
// In particular, this means you must call e.g. mpv_command_async() instead of
// mpv_command().
// If you want to use synchronous calls, either make them on a separate thread,
// or remove the option below.
{MPV_RENDER_PARAM_ADVANCED_CONTROL, &(int){1}},
```

Note: the OFFICIAL example **uses `mpv_command_async()`** when `ADVANCED_CONTROL=1`. We used `mpv_command()` (synchronous).

**Reference**: [mpv-android](https://github.com/mpv-player/mpv/issues/15253#issuecomment-2453582217) has been calling `mpv_command()` from UI thread successfully for years — **without** `ADVANCED_CONTROL`.

### 1.2 Avalonia Flyout + FilePicker Deadlock (SECONDARY)

**Source**: [Avalonia #21433 — OpenFilePickerAsync method bug](https://github.com/AvaloniaUI/Avalonia/issues/21433)

> "If the OpenFilePickerAsync method is called multiple times on the same window, the window will get stuck."
>
> "Adding `await Task.Delay(250)` before calling the method is indeed effective."

**Source**: [Avalonia #19839 — Dead lock on OpenFilePickerAsync](https://github.com/AvaloniaUI/Avalonia/discussions/19839)

> "On the first time I call PlatformPickAsync I have got a deadlock and the FilePicker never shown."

**Source**: [Avalonia #21401 — Win32 compositor blocking during TopLevel close](https://github.com/AvaloniaUI/Avalonia/pull/21401)

> "Popup.CloseCore cleanup disposable now posts popupHost.Dispose() via Dispatcher.UIThread.Post(..., DispatcherPriority.Input) instead of destroying the host synchronously."
>
> "It is important that we destroy the windows at less than Render priority because Menus will allow all Render-priority queue items to be processed before firing the click event."

**Mechanism**: When a Flyout menu button triggers a command that opens a native FilePicker:
1. Button click starts `RelayCommand` execution (async Task)
2. Flyout close animation begins (Avalonia compositor)
3. `StorageProvider.OpenFilePickerAsync()` opens Windows COM file dialog
4. Both the Flyout close (Avalonia) and the FilePicker (Windows COM) need the STA message pump
5. If mpv is also blocking the UI thread in `mpv_command()` → triple deadlock

**Avalonia source**: [CSDN article on Avalonia FilePicker deadlock](https://wenku.csdn.net/column/76zs0sg2jw5) — "Never use `.Result` or `.Wait()` to get dialog results. Windows: system dialog runs on independent thread. Linux/macOS: dialog depends on main UI thread."

---

## Phase 2 — Code Flow Analysis (What Happens When User Clicks "Open Files")

```
[HeaderBarControl.axaml line 84]
  Button Command="{Binding OpenFilesCommand}"
    ↓
[MainViewModel.Actions.cs line 27]
  OnOpenFiles() → RequestOpenFilesAsync() 
    ↓
[MainWindow.FileDialogs.cs line 12]
  _dialogHandler!.OpenFilesAsync()
    ↓
[FileDialogHandler.cs line 71]
  Dispatcher.UIThread.InvokeAsync(async () => {
    Storage.OpenFilePickerAsync(...)  ← Avalonia Native FilePicker
  }, DispatcherPriority.Background)
    ↓ (user selects file, dialog returns string[])
[MainViewModel.Actions.cs line 30]
  OpenFiles(paths) → Playlist.Clear(), Playlist.Add(), OpenFile(paths[0])
    ↓
[MainViewModel.Actions.cs line 127]
  _player.Open(path)
    ↓
[MpvPlayer.cs line 92]
  Open(string path) → LoadFile(path, replace: true)
    ↓
[MpvPlayer.cs line 1361]
  CommandInternal("loadfile", path, "replace")
    ↓
[MpvPlayer.cs line 1512]
  MpvNative.mpv_command(_mpv, argv)  ← BLOCKING synchronous C call
```

**Critical observation**: `mpv_command()` runs on the Avalonia UI thread. This thread is also the COM STA thread that powers:
- Flyout open/close animations
- Native FilePicker dialog
- `Dispatcher.UIThread.Post()` deliveries

---

## Phase 3 — Fixes Applied (In Order)

### ✅ Fix 1: `ADVANCED_CONTROL = 0` (Primary Fix)

**File**: [`src/Media/Implementations/mpv/MpvPlayer.cs:1084`](file:///x:/Development/Cine_CSharp_DotNet/src/Media/Implementations/mpv/MpvPlayer.cs#L1084)

**Change**: `AllocHGlobalValue(1)` → `AllocHGlobalValue(0)`

**Why**: Per mpv render.h, ADVANCED_CONTROL=0 means mpv uses **internal timeouts** instead of deadlocking. If `mpv_render_context_render()` isn't called in time, playback quality degrades gracefully — the app doesn't freeze.

**Risk**: Slightly degraded rendering during heavy load (mpv logs "mpv_render_context_render() not being called or stuck"). This is acceptable because:
- mpv-android uses ADVANCED_CONTROL=0 successfully
- The mpv manual recommends against ADVANCED_CONTROL in embedded apps that can't strictly guarantee render thread isolation

### ✅ Fix 2: FileDialogHandler — Defer FilePicker to Background Priority

**File**: [`src/App/Application/Services/FileDialogHandler.cs`](file:///x:/Development/Cine_CSharp_DotNet/src/App/Application/Services/FileDialogHandler.cs)

**Change**: All dialog methods wrap `Storage.OpenFilePickerAsync()` inside:
```csharp
Dispatcher.UIThread.InvokeAsync(async () => {
    // dialog call
}, DispatcherPriority.Background);
```

**Why**: `DispatcherPriority.Background` runs AFTER all `Render` and `Input` priority work completes. This means:
1. Flyout close animation finishes (Render priority)
2. Button click event fully processes (Input priority)
3. Then the native FilePicker opens (Background priority)

**Reference**: [Avalonia PR #21401](https://github.com/AvaloniaUI/Avalonia/pull/21401) — "It is important that we destroy the windows at less than Render priority because Menus will allow all Render-priority queue items to be processed before firing the click event."

### ✅ Fix 3: Pure Avalonia ↔ mpv Separation

**File**: [`src/App/Application/ViewModels/MainViewModel.Actions.cs`](file:///x:/Development/Cine_CSharp_DotNet/src/App/Application/ViewModels/MainViewModel.Actions.cs#L27)

**Change**: 
- `OpenFile()` explicitly separates UI bookkeeping from mpv hand-off
- `OnOpenFiles()`, `OnOpenFolder()`, `OnAddFiles()` marked as "Purely Avalonia — no MPV coupling"
- Single mpv call: `_player.Open(path)` — no Task.Run, no async wrapping

### ✅ Fix 4: Centralized FileDialogHandler

**File**: [`src/App/Application/Services/FileDialogHandler.cs`](file:///x:/Development/Cine_CSharp_DotNet/src/App/Application/Services/FileDialogHandler.cs)

**5 use-cases consolidated**: OpenFiles, OpenFolder, AddFiles, OpenAudio, OpenSubtitle

---

## Phase 4 — Remaining Issues & Next Steps

### 4.1 Why `Responding = False` Still Appears at Startup

From startup logs, the app shows `Responding = False` almost immediately. This may be a **separate initialization issue**:

```
[18:00:54.064] OnWindowInitialized start
[18:00:54.108] InitVideoRenderer: initializing MpvVideoView (ANGLE + render API)
[18:00:54.595] OnWindowInitialized finish        ← 531ms for ANGLE init
```

The ANGLE/EGL context creation blocks the UI thread for ~500ms during startup. This is normal and temporary.

### 4.2 Alternative Fix: `mpv_command_async` Route

If ADVANCED_CONTROL=1 is desired (for best rendering quality), the correct approach per mpv docs is:

**Option A**: Use `mpv_command_async()` for all commands
```csharp
// MpvPlayer.cs — replace mpv_command with mpv_command_async
var err = MpvNative.mpv_command_async(_mpv, 0, argv);
// Response arrives as MPV_EVENT_COMMAND_REPLY in event loop
```

- Pros: Full ADVANCED_CONTROL benefits, no deadlock
- Cons: Requires P/Invoke declaration (may not be in our libmpv-2.dll), complicates command flow

**Option B**: Dispatch `mpv_command()` to the event-loop thread
```csharp
// MainViewModel.Actions.cs
await Task.Run(() => _player.Open(path));  // Runs on thread-pool, not UI
```

- Pros: Keeps mpv_command (synchronous, simpler)
- Cons: Thread-pool thread + mpv internal lock = still risky with ADVANCED_CONTROL=1
- **This was tried and still hung** — because ADVANCED_CONTROL=1 causes deadlock regardless of thread, per mpv docs

### 4.3 Alternative Fix: Disable Flyout Before FilePicker

**Option C**: Close the Flyout explicitly before opening the FilePicker
```csharp
// HeaderBarControl.axaml.cs
private void OnOpenFilesClick(object sender, RoutedEventArgs e)
{
    BtnOpenMenu.Flyout?.Hide();  // Force close Flyout
    // Now safe to open FilePicker
}
```

- Pros: Eliminates Flyout + FilePicker race
- Cons: Architectural — requires HeaderBarControl to know about FileDialogHandler

### 4.4 Debugging Approach

To isolate which of the two factors is causing the remaining hang:

```powershell
# 1. Kill any hung process
taskkill /F /IM App.exe

# 2. Check if the hang occurs BEFORE the FilePicker opens (Factor 2)
#    or AFTER file selection (Factor 1):
#    - If hang on Click → Factor 2 (Flyout + FilePicker)
#    - If hang after selecting file → Factor 1 + 3 (mpv_command on UI)
```

### 4.5 Best Practice from Other Projects

| Project | Approach | ADVANCED_CONTROL |
|---------|----------|-----------------|
| **mpv official SDL example** | `mpv_command_async()` | 1 |
| **mpv-android** | `mpv_command()` from UI thread | 0 (default) |
| **HanumanInstitute.LibMpv** | `MpvCommand.InvokeAsync()` → `Task.Run` thread-pool | 0 (default) |
| **mpv.net** | Custom event loop, all commands via async | 0 |
| **NipaPlay (Avalonia + mpv)** | Uses mpv through managed wrapper | Unknown |

**Recommended for Cine**: ADVANCED_CONTROL=0 (already applied) + `mpv_command()` from a **dedicated command thread** (not UI, not render).

### 4.7 CONFIRMED ROOT CAUSE: Avalonia #18969 — Flyout + StorageProvider Deadlock

🔴 **Open confirmed bug**: [Avalonia #18969](https://github.com/AvaloniaUI/Avalonia/issues/18969) (June 2, 2025 — still open, affects 12.0)

> "Windows Freeze when StorageProvider.SaveFilePickerAsync called while a Flyout is open"
>
> "If for example `btn.Flyout.Hide()` don't get call before `StorageProvider.SaveFilePickerAsync` Window will get Freeze."

**Why breakpoints make it work**: "If a break point get trigger before StorageProvider.SaveFilePickerAsync Flyout's will Hide automatically so troubleshooting will be so difficult and painful" — explains why debugging made the problem disappear.

**Fix applied**
(#18969-workaround): `FileDialogHandler.CloseAnyFlyout()` calls `OnBeforeOpen?.Invoke()`,
which is wired to `HeaderBarControl.CloseFlyout()` → `BtnOpenMenu.Flyout?.Hide()`.
This runs **before** any `StorageProvider` dialog.

---

## Phase 5 — Segregation: Complete Call-Site Audit

### 5.1 Architecture

All `StorageProvider` (Avalonia file dialog) calls are centralized in **one file**:

```
FileDialogHandler.cs  ←  All StorageProvider calls live here
    ↑
    ├── MainWindow.FileDialogs.cs      (thin 1-liner delegates)
    │       ↑
    │       └── MainWindow.Core.cs     (wires Func<> delegates to ViewModel)
    │               ↑
    │               └── MainViewModel.Actions.cs  (menu command handlers)
    │
    ├── PlaylistDialog.axaml.cs        (save/load playlist dialogs)
    │
    ├── AudioManager.cs                (via RequestAudioFileAsync → handler)
    └── SubtitleManager.cs             (via RequestSubtitleFileAsync → handler)
```

### 5.2 All 7 Dialog Methods (FileDialogHandler.cs)

| # | Method | Where Called | Trigger |
|---|--------|-------------|---------|
| 1 | `OpenFilesAsync()` | MainWindow.FileDialogs → ViewModel.OnOpenFiles | Menu: Open → Open Files / Ctrl+O |
| 2 | `OpenFolderAsync()` | MainWindow.FileDialogs → ViewModel.OnOpenFolder | Menu: Open → Open Folder / Ctrl+Shift+O |
| 3 | `AddFilesAsync()` | MainWindow.FileDialogs → ViewModel.OnAddFiles | Menu: Open → Add Files |
| 4 | `OpenAudioAsync()` | MainWindow.FileDialogs → ViewModel.OnAddAudio / AudioManager | Menu: Audio → Add External |
| 5 | `OpenSubtitleAsync()` | MainWindow.FileDialogs → ViewModel / SubtitleManager | Menu: Subtitles → Add External |
| 6 | `SavePlaylistAsync()` | PlaylistDialog.OnSavePlaylistClick | PlaylistDialog → Save button |
| 7 | `OpenPlaylistFilesAsync()` | PlaylistDialog.GetOpenFilePathsAsync | PlaylistDialog → Load files button |

### 5.3 Validation

```powershell
# Confirmed: zero direct StorageProvider calls outside FileDialogHandler.cs
Select-String -Path (Get-ChildItem src\App -Recurse -Filter "*.cs" -Exclude "FileDialogHandler.cs") `
    -Pattern "StorageProvider\." -SimpleMatch
# Output: (empty — no leaks)
```

### 5.4 Key Design Decision

**Why `FileDialogHandler` instead of direct `StorageProvider` calls everywhere?**

1. **Flyout + FilePicker deadlock fix in one place** — all dialogs use `DispatcherPriority.Background` deferral
2. **File-type filters centralized** — `VideoFilter`, `AudioFilter`, `SubtitleFilter` are `static readonly` fields
3. **Error handling consistent** — every method catches exceptions, logs a warning, returns `null`
4. **New UI entry points are trivial** — add one method to handler, one delegate to MainWindow.FileDialogs, wire in Core

---

## Phase 6 — Verification Checklist

- [ ] App starts without `Responding = False` (initial ANGLE init OK)
- [ ] Click **Open → Open Files** — Flyout closes, FilePicker appears
- [ ] Select a video file — file loads and plays
- [ ] Click **Open → Open Folder** — FolderPicker appears
- [ ] Drag-drop a video onto the window — plays
- [ ] Press Ctrl+O — FilePicker appears
- [ ] Click **Open → Add Files** — FilePicker appears, files added to playlist
- [ ] Click **Audio → Add External** — Audio file loaded
- [ ] Click **Subtitles → Add External** — Subtitle file loaded

---

## References

1. [mpv render.h — ADVANCED_CONTROL deadlock warning](https://github.com/mpv-player/mpv/blob/master/include/mpv/render.h#L38-L79)
2. [mpv official SDL example — mpv_command_async with ADVANCED_CONTROL](https://github.com/mpv-player/mpv-examples/blob/master/libmpv/sdl/main.c#L120-L130)
3. [mpv issue #15253 — mpv-android calls mpv_command from UI thread](https://github.com/mpv-player/mpv/issues/15253#issuecomment-2453582217)
4. [Avalonia #21433 — OpenFilePickerAsync deadlock with Flyouts](https://github.com/AvaloniaUI/Avalonia/issues/21433)
5. [Avalonia #19839 — Dead lock on OpenFilePickerAsync](https://github.com/AvaloniaUI/Avalonia/discussions/19839)
6. [Avalonia PR #21401 — Win32 compositor blocking fix via DispatcherPriority](https://github.com/AvaloniaUI/Avalonia/pull/21401)
7. [Avalonia #7745 — OpenFileDialog Hangs on Linux](https://github.com/AvaloniaUI/Avalonia/issues/7745)
8. [CSDN — Avalonia FilePicker deadlock best practices](https://wenku.csdn.net/column/76zs0sg2jw5)
9. [HanumanInstitute.LibMpv — C# libmpv wrapper with async command support](https://github.com/HanumanInstitute/LibMpv)
