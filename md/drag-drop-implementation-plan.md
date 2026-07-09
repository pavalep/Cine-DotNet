# Drag & Drop Implementation Plan — Cine (Avalonia / .NET)

> Deep analysis of the current state, industry best practices, what to remove,
> what to fix, and a phased implementation checklist.

---

## Industry Best Practices (Reference)

These are the standard patterns professional media-player and file-manager apps follow for drag & drop. All implementation decisions below are anchored to these.

1. **Single authoritative drop handler.** One layer owns file processing. Visual layers only manage appearance; they never touch the file system or player.
2. **`DataFormat.Files` guard on every DragOver.** Always check the data format before setting `DragDropEffects`; reject non-file payloads with `None` so the OS shows the "forbidden" cursor.
3. **Counter-based enter/leave tracking.** Child elements fire synthetic Leave/Enter pairs as the cursor moves. Use an integer counter (`_dragCounter++` / `--`) rather than a boolean flag to correctly detect when the drag truly exits the window.
4. **Recursive folder scan in a background thread.** Never block the UI thread enumerating directories. Scan on a `Task.Run` thread pool thread, then marshal the result back to UI.
5. **Centralised media-extension registry.** One static `HashSet<string>` owned by a service. No duplicate extension lists scattered across multiple files.
6. **Magic-byte validation as the second gate.** Extension check is fast but spoofable. Read the first 8 bytes to confirm the file type before handing it to the player.
7. **Distinct UX for "no media playing" vs "media playing".** When idle → replace playlist and start from the top. When playing → ask (or default to a documented policy: replace, append, or queue-next).
8. **Sort dropped files naturally.** When a folder or multi-file drop arrives, sort by natural filename order (track number, episode number) before loading into the playlist.
9. **Feedback hierarchy:** DragOver shows a subtle tinted overlay; Drop plays a short transition; invalid-format drops show the system "forbidden" cursor (no overlay).
10. **Accessibility.** The drop zone must have `AutomationProperties.Name` and `AutomationProperties.HelpText` so screen readers announce the affordance.
11. **No UI stutter.** Show/hide of drag overlays must never trigger a layout reflow on the main grid — use `Opacity`+`IsHitTestVisible` toggling, not `IsVisible` toggling inside a shared layout container.
12. **Error feedback.** If a drop results in zero playable files (e.g., an image folder), show a brief OSD toast — never silently fail.

---

## Current State — What Exists (Code Audit)

### Architecture Overview

There are currently **two drag-and-drop systems** operating in parallel, plus one media-extension registry that is duplicated in three places.

```
MainWindow (window-level)
  ├── DragDrop.AllowDrop="True"  ← on Window, Grid, Border, Grid, VideoClickOverlay (5 elements)
  ├── AddHandler(DragDrop.*) in MainWindow.Wiring.cs
  └── Handlers in MainWindow.Input.cs
        ├── OnWindowDragEnter  → shows DragDropOverlay
        ├── OnWindowDragOver   → DataFormat.File check ✓
        ├── OnWindowDragLeave  → hides DragDropOverlay
        └── OnWindowDrop       → OpenDroppedFiles() → _viewModel.OpenFiles()

StartPage (visual layer — component-level)
  ├── DragDrop.SetAllowDrop(this, true) in constructor
  ├── AddHandler(DragDrop.*) in constructor
  └── Handlers in StartPage.axaml.cs
        ├── OnGlobalDragEnter  → _dragCounter++, SetDropZoneActive(true), DropTarget.IsVisible=true
        ├── OnGlobalDragOver   → DataFormat.File check ✓
        ├── OnGlobalDragLeave  → _dragCounter--, clears visuals at 0
        └── OnGlobalDrop       → resets visuals only (NO FILE PROCESSING)

DragDropOverlay component
  └── Show()/Hide() with 150ms CSS-easing fade animation
```

### Problems Identified

#### Problem 1 — Duplicate, Scattered Media Extension Lists

The same `HashSet<string>` of media extensions is defined in **three separate places**:

| File | Symbol | Extensions |
|---|---|---|
| `MainWindow.Input.cs` | `MediaExtensions` (private static) | Video + Audio (34 extensions) |
| `MediaFileService.cs` | `VideoExtensions` + `AudioExtensions` (private static) | Video (17) + Audio (8) — different from above |
| `StartPage.axaml.cs` | `VideoExts` (private static) | Video only (17 extensions) |

`MainWindow.Input.cs` includes `.ac3`, `.dts`, `.alac`, `.ape`, `.aiff` that `MediaFileService` does not. They drift independently and will diverge further over time.

#### Problem 2 — `OnOpenFolder` Does Not Scan for Media Files

In `MainViewModel.Actions.cs`:

```csharp
private async Task OnOpenFolder()
{
    var path = await _fileDialog.OpenFolderAsync();
    if (!string.IsNullOrEmpty(path))
        await OpenFile(path);   // ← passes the FOLDER PATH to the player directly
}
```

It passes a bare folder path to `_player.Open()`. The drag drop path (via `ScanFolderForMedia`) correctly enumerates the folder and extracts media files. The button/keyboard shortcut path does not. This is a **functional bug**: opening a folder via menu/button behaves differently than dropping a folder.

#### Problem 3 — StartPage Drop Handler Is Incomplete

`OnGlobalDrop` in `StartPage.axaml.cs` only clears visual state. It does not call `_viewModel.OpenFiles()`. This works *accidentally* because `MainWindow.Wiring.cs` registers its handlers with `handledEventsToo: true`, so the window-level handler fires regardless. But if that flag ever changes, or if StartPage is used without a MainWindow parent, drops silently do nothing.

#### Problem 4 — `ScanFolderForMedia` Lives in the Wrong Layer

`ScanFolderForMedia` and `IsMediaFile` (and `MediaExtensions`) are private static methods in `MainWindow.Input.cs` — a UI/shell partial class. File system scanning is application/domain logic, not UI logic. It belongs in `MediaFileService` (which already exists and is the right home for it).

#### Problem 5 — `DragDrop.AllowDrop="True"` Is Set on Five Elements

In `MainWindow.axaml`, `DragDrop.AllowDrop="True"` appears on:
- `<Window>`
- `<Grid>` (root grid)
- `<Border x:Name="ContentClip">`
- `<Grid x:Name="MainOverlay">`
- `<Border x:Name="VideoClickOverlay">`

Only the `Window` needs it. Avalonia routes drag events up the visual tree. Setting it on child elements is redundant and can cause spurious duplicate events in edge cases.

#### Problem 6 — No Feedback When Drop Yields Zero Playable Files

`OpenDroppedFiles` in `MainWindow.Input.cs` silently does nothing when a drop contains no recognised media files (e.g., user drops a folder full of images). Professional players show a brief OSD notification: "No playable files found."

#### Problem 7 — No Behavior Distinction: Idle vs. Playing

When a video is already playing and the user drops new files, `OpenFiles()` always clears the playlist and starts from the beginning. There is no "append to queue" or "play next" path from drag & drop. The UX is identical to the idle case, which removes the current video abruptly.

#### Problem 8 — `ScanFolderForMedia` Runs on UI Thread

`OpenDroppedFiles` is called from `OnWindowDrop` which is an event handler on the UI thread. The `Directory.EnumerateFiles` / `Directory.EnumerateDirectories` calls inside `ScanFolderForMedia` are synchronous file I/O. For large folder trees this blocks the UI thread.

#### Problem 9 — Demo Data Seeded Unconditionally in `StartPage.axaml.cs`

```csharp
if (vm.RecentFiles.Count == 0 && !_seededDemo)
{
    _seededDemo = true;
    var demoFiles = new[] { "Interstellar 2014.mp4", ... };
    foreach (var f in demoFiles)
        vm.RecentFiles.Add(f);
}
```

This seeds fake file names into the real `RecentFiles` observable collection on the ViewModel, which is persisted. This will corrupt the recent-files list in any non-demo build and is clearly leftover scaffolding.

#### Problem 10 — `DropTarget` Overlay in StartPage Is a Layout Sibling

In `StartPage.axaml`, `<Border x:Name="DropTarget">` is declared inside the same `<Grid>` as the main content. When it becomes visible it participates in layout measurement, which can cause a layout reflow on the rest of the grid.

---

## Desired Behavior (Target Spec)

### File Drop

| Payload | No video playing | Video playing |
|---|---|---|
| Single media file | Clear playlist, load and play | Replace playlist, load and play (default); future: offer "Play Next" / "Append" via OSD |
| Multiple media files | Sort naturally, load all into playlist, play first | Replace playlist, load all, play first |
| Single folder | Scan recursively for media, sort naturally, load all, play first | Replace playlist (same as above) |
| Multiple folders | Merge all scanned files, sort, load | Replace playlist |
| Mixed files + folders | Expand folders, merge, sort, load | Replace playlist |
| Non-media files only | Show OSD: "No playable files found" | No change; show OSD |
| Empty folder | Show OSD: "No playable files found" | No change |

### Drag Over (Visual Feedback)

| Context | Behavior |
|---|---|
| Files contain media | Show overlay, `DragDropEffects.Copy`, "Drop to Play" message |
| Files contain no media | `DragDropEffects.None` (OS forbidden cursor), no overlay |
| Non-file payload | `DragDropEffects.None`, no overlay |

### Drag Leave

- Overlay hides with fade-out regardless of reason for leave (cursor exited window or Escape pressed).

---

## Implementation Phases

---

### Phase 1 — Remove Bad Code

> **Goal:** Clean up the three problems that are outright wrong or harmful before adding anything new.

- [ ] **1.1** Remove the demo file seeding block from `StartPage.axaml.cs` (`RebuildRecentFiles` method, lines starting `if (vm.RecentFiles.Count == 0 && !_seededDemo)`). Remove the `_seededDemo` field declaration.
- [ ] **1.2** Remove `MediaExtensions`, `IsMediaFile`, and `ScanFolderForMedia` from `MainWindow.Input.cs`. These are private static members that will be replaced by a service method.
- [ ] **1.3** Remove `VideoExts` from `StartPage.axaml.cs`. The card factory will use the service.
- [ ] **1.4** Remove the redundant `DragDrop.AllowDrop="True"` attributes from `MainWindow.axaml` on all child elements except the `<Window>` root: remove from `<Grid>`, `<Border x:Name="ContentClip">`, `<Grid x:Name="MainOverlay">`, and `<Border x:Name="VideoClickOverlay">`.
- [ ] **1.5** In `StartPage.axaml`, ensure `<Border x:Name="DropTarget">` has `IsHitTestVisible="False"` and `ZIndex` set high enough (e.g. `ZIndex="10"`) so it renders as a pure visual overlay without affecting layout of siblings. Confirm it is a direct child of the root `<Grid>` (already is) — no changes needed if confirmed.

---

### Phase 2 — Centralise the Media Extension Registry

> **Goal:** One place defines what a "media file" is. Everything else calls the service.

- [ ] **2.1** Add `ScanFolderAsync(string folder)` and `NaturalSort(IEnumerable<string> paths)` to `IMediaFileService`:
  ```csharp
  /// <summary>Recursively scan a folder for media files. Runs on a background thread.</summary>
  Task<string[]> ScanFolderAsync(string folder, CancellationToken ct = default);

  /// <summary>Sort paths in natural order (numeric segments compared by value).</summary>
  string[] NaturalSort(IEnumerable<string> paths);
  ```
- [ ] **2.2** Implement `ScanFolderAsync` in `MediaFileService.cs`. Use `Task.Run` internally so callers on the UI thread are safe. Catch `UnauthorizedAccessException` and `IOException` per-directory and continue (skip inaccessible paths). Respect the cancellation token.
- [ ] **2.3** Implement `NaturalSort` using a `StringComparer` that splits path segments on digit/non-digit boundaries and compares numeric parts as integers (standard natural sort algorithm).
- [ ] **2.4** Ensure `MediaFileService.IsValidMediaFile` extension list includes all extensions formerly in `MainWindow.Input.cs` (`.ac3`, `.dts`, `.alac`, `.ape`, `.aiff`). Add them to `AudioExtensions` and `MagicBytes` where applicable.
- [ ] **2.5** Inject `IMediaFileService` into `MainViewModel` (it is already there as `_mediaFile`). No DI changes needed.
- [ ] **2.6** In `StartPage.axaml.cs` card factory, replace `VideoExts.Contains(ext)` with `_mediaFileService.IsValidMediaFile(filePath)` — or pass the service via the `CreateRecentCard` method signature, or resolve it through the ViewModel. Choose the cleanest path given existing DI setup.

---

### Phase 3 — Fix the Core Drop Logic

> **Goal:** One handler owns file processing; it runs off the UI thread; it handles all payloads correctly.

- [ ] **3.1** Create `IDragDropService` interface in `src/App/Application/Services/`:
  ```csharp
  public interface IDragDropService
  {
      /// <summary>
      /// Returns true if the drag data contains at least one item that is or
      /// contains a media file. Sets DragEffects on the event accordingly.
      /// </summary>
      bool EvaluateDragOver(DragEventArgs e);

      /// <summary>
      /// Extracts all media files from the drop payload (expanding folders).
      /// Returns an empty array if no media files are found.
      /// </summary>
      Task<string[]> ExtractMediaFilesAsync(DragEventArgs e, CancellationToken ct = default);
  }
  ```
- [ ] **3.2** Implement `DragDropService` in `src/App/Application/Services/DragDropService.cs`:
  - `EvaluateDragOver`: check `e.DataTransfer.Contains(DataFormat.Files)`, set `DragDropEffects.Copy` or `DragDropEffects.None`, return bool.
  - `ExtractMediaFilesAsync`: iterate `e.DataTransfer.GetFiles()`, expand directories via `IMediaFileService.ScanFolderAsync`, filter single files via `IMediaFileService.IsValidMediaFile`, collect, natural-sort via `IMediaFileService.NaturalSort`, return array.
- [ ] **3.3** Register `IDragDropService` → `DragDropService` in the DI container (find where other services are registered in `App.axaml.cs` or the service collection setup).
- [ ] **3.4** Inject `IDragDropService` into `MainViewModel` alongside `IMediaFileService`.
- [ ] **3.5** Add `OpenDroppedFilesAsync(DragEventArgs e)` to `MainViewModel`:
  ```csharp
  public async Task OpenDroppedFilesAsync(DragEventArgs e)
  {
      var paths = await _dragDrop.ExtractMediaFilesAsync(e);
      if (paths.Length == 0)
      {
          ShowOsd("No playable files found");
          return;
      }
      await OpenFiles(paths);
  }
  ```
- [ ] **3.6** Update `OnWindowDrop` in `MainWindow.Input.cs` to call `await _viewModel.OpenDroppedFilesAsync(e)` instead of the local `OpenDroppedFiles(e)`.
- [ ] **3.7** Fix `OnOpenFolder` in `MainViewModel.Actions.cs` — after obtaining a folder path from the dialog, call `_mediaFile.ScanFolderAsync(path)` and pass the results to `OpenFiles()`. This brings button/keyboard behavior in line with drag & drop:
  ```csharp
  private async Task OnOpenFolder()
  {
      var path = await _fileDialog.OpenFolderAsync();
      if (string.IsNullOrEmpty(path)) return;
      var files = await _mediaFile.ScanFolderAsync(path);
      if (files.Length == 0) { /* show OSD? */ return; }
      await OpenFiles(files);
  }
  ```

---

### Phase 4 — Fix the StartPage Visual Layer

> **Goal:** StartPage handles its own visuals cleanly, delegates file processing to the ViewModel.

- [ ] **4.1** In `StartPage.axaml.cs`, update `OnGlobalDrop` to also call the ViewModel:
  ```csharp
  private async void OnGlobalDrop(object? sender, DragEventArgs e)
  {
      _dragCounter = 0;
      SetDropZoneActive(false);
      if (DropTarget is not null) DropTarget.IsVisible = false;

      if (DataContext is MainViewModel vm)
          await vm.OpenDroppedFilesAsync(e);
  }
  ```
  This removes the dependency on accidental `handledEventsToo` propagation.
- [ ] **4.2** Update `OnGlobalDragOver` in `StartPage.axaml.cs` to use the new `IDragDropService.EvaluateDragOver` (or inline the `DataFormat.Files` check — both acceptable; service is more testable).
- [ ] **4.3** In `StartPage.axaml`, add `AutomationProperties.Name="Drop zone — drag media files here"` and `AutomationProperties.HelpText="Drag video or audio files onto this area to play them"` to `<Border x:Name="DropZone">`.
- [ ] **4.4** Verify `DropZone` pointer-over styles also apply during active drag (they won't by default — `:pointerover` pseudo-class does not fire during drag). Replace or supplement the CSS hover style with the code-behind `SetDropZoneActive(true/false)` calls that already exist — confirm they correctly set `DropZone.Background` for both wide and narrow layouts. Currently `SetDropZoneActive` only updates `DropZone`; add a matching update for `DropZoneNarrow`.
- [ ] **4.5** In the `DropTarget` border in `StartPage.axaml`, change `IsVisible="False"` to `Opacity="0"` and manage show/hide via opacity only (prevents layout reflow). Update `OnGlobalDragEnter` and `OnGlobalDrop` to set `DropTarget.Opacity = 1 / 0` instead of `IsVisible`.

---

### Phase 5 — "Playing" vs. "Idle" Drop Behavior

> **Goal:** When media is already playing, the drop does not brutally interrupt without context.

- [ ] **5.1** Add a `IsMediaPlaying` computed property to `MainViewModel` (or use existing `IsPlaying`).
- [ ] **5.2** In `OpenDroppedFilesAsync`, distinguish the two cases:
  ```csharp
  public async Task OpenDroppedFilesAsync(DragEventArgs e)
  {
      var paths = await _dragDrop.ExtractMediaFilesAsync(e);
      if (paths.Length == 0) { ShowOsd("No playable files found"); return; }

      if (IsPlaying || IsPaused)
      {
          // Replace current playlist — same as current behavior.
          // Future: offer "append" or "play next" via OSD action button.
          await OpenFiles(paths);
          ShowOsd($"Loaded {paths.Length} file{(paths.Length > 1 ? "s" : "")}");
      }
      else
      {
          await OpenFiles(paths);
      }
  }
  ```
  This is a minimal first step. The OSD confirmation path can be added later as a follow-on feature.
- [ ] **5.3** In `DragDropOverlay.axaml`, update the "Drop to Play" text to be context-sensitive. When `IsPlaying` is true, show "Drop to Replace". Pass the state via a bindable property or update the text from `MainWindow` after the `Show()` call.

---

### Phase 6 — OSD Feedback for Drop Results

> **Goal:** User always gets feedback, especially on failure.

- [ ] **6.1** Ensure `MainViewModel` exposes a `ShowOsd(string message, int durationMs = 2500)` method that routes to `MainWindow`'s `ShowOsdNotification`. If this coupling is unwanted, raise an event (`OsdRequested`) that MainWindow subscribes to.
- [ ] **6.2** Call `ShowOsd(...)` in `OpenDroppedFilesAsync` for the no-files case (see Phase 5.2 above).
- [ ] **6.3** Call `ShowOsd(...)` on success when dropping during playback: `"Loaded 3 files"` or `"Loading: Dune Part Two.mkv"`.

---

### Phase 7 — Polish and Hardening

- [ ] **7.1** Write unit tests for `DragDropService.ExtractMediaFilesAsync` covering: single file, multiple files, single folder, mixed files+folder, no-media payload, empty folder, inaccessible folder (permissions), very deep folder tree.
- [ ] **7.2** Write unit tests for `MediaFileService.ScanFolderAsync` and `NaturalSort`.
- [ ] **7.3** Verify the `_dragCounter` approach in `StartPage.axaml.cs` works correctly when the cursor moves between child elements rapidly — confirm no negative-counter edge case.
- [ ] **7.4** Test drop behavior on Windows with files from an archive manager (ZIP opened in Explorer) — `TryGetLocalPath()` may return null for these. Add a null/empty check and a graceful skip with logging.
- [ ] **7.5** Add a `[Test / Smoke]` manual test checklist entry in the project's test docs: drag a folder with 200+ files, confirm UI does not freeze (folder scan is off UI thread per Phase 3.2).
- [ ] **7.6** Audit `DragDrop.AllowDrop` is only set on `<Window>` after Phase 1 cleanup — do a grep to confirm no new ones were introduced.
- [ ] **7.7** Confirm `DragDropOverlay` is correctly hidden on `Escape` key press during drag (Avalonia fires `DragLeave` on Escape — verify this fires for the Window-level handler).

---

## File Change Summary

| File | Change | Phase |
|---|---|---|
| `StartPage.axaml.cs` | Remove demo seed block + `_seededDemo` field; remove `VideoExts`; fix `OnGlobalDrop`; fix `SetDropZoneActive` for narrow; add service access for card factory | 1, 2, 4 |
| `StartPage.axaml` | Add AutomationProperties to DropZone; switch DropTarget from IsVisible to Opacity | 4 |
| `MainWindow.Input.cs` | Remove `MediaExtensions`, `IsMediaFile`, `ScanFolderForMedia`; update `OnWindowDrop` to call ViewModel | 1, 3 |
| `MainWindow.axaml` | Remove `DragDrop.AllowDrop="True"` from 4 child elements | 1 |
| `IMediaFileService.cs` | Add `ScanFolderAsync` + `NaturalSort` signatures | 2 |
| `MediaFileService.cs` | Implement `ScanFolderAsync` (background thread) + `NaturalSort`; extend extension list | 2 |
| `IDragDropService.cs` | Create new interface | 3 |
| `DragDropService.cs` | Create new implementation | 3 |
| `MainViewModel.Actions.cs` | Fix `OnOpenFolder` to scan; add `OpenDroppedFilesAsync`; add OSD calls | 3, 5, 6 |
| `MainViewModel.cs` | Inject `IDragDropService`; wire OSD event if needed | 3, 6 |
| DI container (App startup) | Register `IDragDropService → DragDropService` | 3 |
| `DragDropOverlay.axaml` | Add dynamic "Drop to Replace" text option | 5 |

---

## Notes

- The `src-backup/` folder contains an older version of some files. Do not edit anything in `src-backup/` — it is reference only.
- The `alternate_code/` folder also contains reference code. Do not edit.
- All phases are independent. Phase 1 can be merged alone without breaking anything. Phases 2–3 should be merged together since Phase 3 depends on the new service. Phases 4–7 are additive and can be merged in any order after Phase 3.
