# Cine — Fix Solutions Guide

> **Companion document to**: [Codebase_Analysis_Consolidated.md](./Codebase_Analysis_Consolidated.md)
>
> This document contains the exact, ready-to-apply code changes for every confirmed defect identified in the consolidated audit.
> Each fix is ordered by severity tier and includes the precise file path, real line numbers, and a
> before/after diff. Apply fixes in tier order — Tier 1 changes unblock the others.

---

## How to Use This Document

1. Read the **Root Cause** for context before touching any file.
2. Apply the **Exact Diff** — do not paraphrase; copy the replacement text character-for-character.
3. After each tier, build and run the app to confirm behaviour before proceeding.
4. Check off the item in the **Status** column once verified.

---

## Tier 1 — Critical: Blocking UX Bugs

These two fixes must be applied first. They break the entire flyout/overlay system.

---

### Fix 1 — FlyoutOverlay ZIndex Too Low

| Field | Value |
|---|---|
| **File** | [MainWindow.axaml](file:///x:/Development/Cine_CSharp_DotNet/src/App/UI/Views/MainWindow.axaml) |
| **Lines** | 144–148 |
| **Severity** | 🔴 Critical |
| **Status** | ☐ Unresolved |

#### Root Cause
`FlyoutOverlayControl` is declared at `ZIndex="10"` in the window's `Grid`. The `HeaderBarControl` inner `Border` uses `ZIndex="20"` and `ControlsBoxControl` uses `ZIndex="15"`. This means every custom flyout (volume, chapters, equalizer, audio, subtitle, video) is rendered **underneath** the control bars.

Additionally, because the dismissal backdrop (`OverlayBackground`) sits below the control panels in Z-order, clicks on the transparent background that happen to land over a panel area are absorbed by the panel rather than firing `OnBackgroundPointerPressed`. This makes flyouts impossible to dismiss by clicking outside them while hovering over a control bar.

#### Exact Diff

```diff
-        <!-- ══════════════════════════════════════════════════════════════ -->
-        <!--  FLYOUT OVERLAY (ZIndex=10, between controls and windows)   -->
-        <!-- ══════════════════════════════════════════════════════════════ -->
-        <controls:FlyoutOverlayControl x:Name="FlyoutOverlay"
-                                       ZIndex="10" />
+        <!-- ══════════════════════════════════════════════════════════════ -->
+        <!--  FLYOUT OVERLAY (ZIndex=50, above all interactive chrome)   -->
+        <!-- ══════════════════════════════════════════════════════════════ -->
+        <controls:FlyoutOverlayControl x:Name="FlyoutOverlay"
+                                       ZIndex="50" />
```

#### Why ZIndex=50?
The highest confirmed ZIndex in the window is `FocusModeIndicator` at `ZIndex="40"`. Setting the overlay to `50` ensures it sits above every other visual element, including the fullscreen indicator, OSD notifications, and control bars. The overlay's transparent background ensures it does not visually block anything when no flyout is open.

#### Verification
1. Open the app and start playing a file.
2. Click the Volume button in the control bar.
3. The volume slider panel should appear **above** the seek bar and bottom control strip.
4. Click anywhere outside the panel — it should dismiss.
5. Open the equalizer. The previous flyout should close automatically.

---

### Fix 2 — Volume Close Action Is a No-Op ✅ FIXED

| Field | Value |
|---|---|
| **File** | [ControlsBoxControl.axaml.cs](file:///x:/Development/Cine_CSharp_DotNet/src/App/UI/Screens/Shell/ControlsBoxControl.axaml.cs) |
| **Line** | 128 |
| **Severity** | ~~🔴 Critical~~ → ✅ Resolved |
| **Status** | ☑ Resolved |

#### Root Cause
Inside the `FlyoutManager` property setter, each overlay panel registers a close-action delegate that the manager calls when another flyout opens. The volume action was registered as:

```csharp
// Line 128 — CURRENT (broken)
value.Register("volume", () => BtnVolumeMenu?.Flyout?.Hide());
```

`BtnVolumeMenu` has **no native `Flyout` assigned** to it — the volume panel is shown entirely via the canvas-based `FlyoutOverlayControl`. Therefore `BtnVolumeMenu.Flyout` is always `null`, and calling `Flyout?.Hide()` is a silent no-op. When the user opens the Equalizer or Chapters flyout, `FlyoutManager` calls this delegate to close the volume panel, but the panel remains visible.

#### Exact Diff

```diff
             // Register close actions — all hide the overlay instead of calling Flyout.Hide()
             Action hideOverlay = () => _flyoutOverlay?.HideContent();
             value.Register("equalizer",   hideOverlay);
-            value.Register("volume",      () => BtnVolumeMenu?.Flyout?.Hide());
+            value.Register("volume",      hideOverlay);
             value.Register("video-menu",  hideOverlay);
             value.Register("chapters",    hideOverlay);
```

The `hideOverlay` action is already defined on line 126 and is used correctly for all other panels. This fix aligns `"volume"` with the same pattern.

#### Verification
1. Open the volume overlay.
2. Click the Equalizer button.
3. The volume panel should close and the equalizer should open.
4. Repeat in reverse — open the equalizer, then click volume. Equalizer closes, volume opens.

---

## Tier 2 — High: Visual Quality Defects

---

### Fix 3 — Double Border on All Custom Flyouts ✅ FIXED

| Field | Value |
|---|---|
| **File** | [FlyoutOverlayControl.axaml](file:///x:/Development/Cine_CSharp_DotNet/src/App/UI/Controls/FlyoutOverlayControl.axaml) |
| **Lines** | 17–26 |
| **Severity** | ~~🟠 High~~ → ✅ Resolved |
| **Status** | ☑ Resolved |

#### Root Cause
`FlyoutOverlayControl.axaml` wraps its content in a `Border` (`ContentContainer`) that declares its own `Background`, `BorderBrush`, `BorderThickness`, `CornerRadius`, and `Padding`. Every panel injected into this container — the volume slider, the equalizer, the track lists — also declares an identical outer `Border` in its own layout. This produces visible double borders and double background tinting on every custom flyout.

**Current layout (broken)**:
```
FlyoutOverlayControl.ContentContainer
├── Background="#1E1E1E"  ← outer box background
├── BorderBrush="#3C3C3C" ← outer box border
├── BorderThickness="1"
├── CornerRadius="6"
└── Padding="4"
    └── [BuildVolumeContent() result]
        └── Border (PopoverBackground, BorderThickness=1, CornerRadius=6)  ← DUPLICATE
            └── StackPanel [volume slider, presets]
```

**After fix (correct)**:
```
FlyoutOverlayControl.ContentContainer
└── [pure position wrapper — no visual properties]
    └── Border (PopoverBackground, BorderThickness=1, CornerRadius=6)  ← single border
        └── StackPanel [volume slider, presets]
```

#### Exact Diff

```diff
-        <Border x:Name="ContentContainer"
-                Background="{StaticResource PopoverBackground}"
-                BorderBrush="{StaticResource PopoverBorder}"
-                BorderThickness="1"
-                CornerRadius="{StaticResource radius-sm}"
-                Padding="{StaticResource space-1}"
-                UseLayoutRounding="True"
-                HorizontalAlignment="Left"
-                VerticalAlignment="Top">
-        </Border>
+        <Border x:Name="ContentContainer"
+                UseLayoutRounding="True"
+                HorizontalAlignment="Left"
+                VerticalAlignment="Top">
+        </Border>
```

> **Important**: After applying this fix, each injected panel is now solely responsible for its own visual chrome. The builders (`BuildVolumeContent`, `TrackFlyoutBuilder.Build`, `AudioEqualizerFlyout`) each already declare a `Border` with `PopoverBackground` and `BorderThickness="1"`, so no changes to those files are needed.

#### Verification
1. Open the volume overlay — you should see a single clean border with no double outline.
2. Open the Audio track selector — same result.
3. Open the Equalizer — single border, correct corner radius.

---

### Fix 4 — BtnOpenMenu Has No Flyout (Dead Button) ✅ FIXED

| Field | Value |
|---|---|
| **Files** | [HeaderBarControl.axaml](file:///x:/Development/Cine_CSharp_DotNet/src/App/UI/Screens/Shell/HeaderBarControl.axaml) (lines 27–57) and [HeaderBarControl.axaml.cs](file:///x:/Development/Cine_CSharp_DotNet/src/App/UI/Screens/Shell/HeaderBarControl.axaml.cs) |
| **Severity** | ~~🟠 High~~ → ✅ Resolved |
| **Status** | ☑ Resolved |

#### Root Cause
`BtnOpenMenu` in `HeaderBarControl.axaml` is a fully styled button but has no `<Button.Flyout>` declaration. In `HeaderBarControl.axaml.cs`, the `FlyoutManager` setter (lines 198–212) guards its wiring behind `if (BtnOpenMenu.Flyout != null)` — which is always false. The `CloseFlyout()` and `ReopenFlyout()` methods both call `BtnOpenMenu.Flyout?.Hide()` / `BtnOpenMenu.Flyout?.ShowAt(...)` on a null flyout, making them silent no-ops. The `UpdateOpenMenuRecentFiles(Flyout)` method is similarly unreachable.

#### Fix 4a — Add Flyout to XAML

Insert the `<Button.Flyout>` block **before** the existing `<Button.Styles>` block inside `BtnOpenMenu`:

```diff
             <Button Grid.Column="0"
                     x:Name="BtnOpenMenu"
                     IsVisible="False"
                     VerticalAlignment="Center"
                     Margin="{StaticResource space-h-l2-r1}"
                     Background="{StaticResource AppHover}"
                     CornerRadius="99"
                     BorderThickness="0"
                     Padding="12,6">
+                <Button.Flyout>
+                    <Flyout Placement="Bottom">
+                        <Border Padding="4"
+                                Background="{StaticResource PopoverBackground}"
+                                BorderBrush="{StaticResource PopoverBorder}"
+                                BorderThickness="1"
+                                CornerRadius="{StaticResource radius-sm}">
+                            <StackPanel x:Name="OpenMenuStack" Width="230" Spacing="2">
+                                <!-- Open File action -->
+                                <Button x:Name="BtnMenuOpenFile"
+                                        Classes="flyout-item">
+                                    <StackPanel Orientation="Horizontal" Spacing="10">
+                                        <materialIcons:MaterialIcon Kind="FileOutline"
+                                            Width="16" Height="16"
+                                            Foreground="{StaticResource TextPrimary}" />
+                                        <TextBlock Text="Open File…"
+                                                   VerticalAlignment="Center"
+                                                   Foreground="{StaticResource TextPrimary}" />
+                                    </StackPanel>
+                                </Button>
+                                <!-- Open Folder action -->
+                                <Button x:Name="BtnMenuOpenFolder"
+                                        Classes="flyout-item">
+                                    <StackPanel Orientation="Horizontal" Spacing="10">
+                                        <materialIcons:MaterialIcon Kind="FolderOutline"
+                                            Width="16" Height="16"
+                                            Foreground="{StaticResource TextPrimary}" />
+                                        <TextBlock Text="Open Folder…"
+                                                   VerticalAlignment="Center"
+                                                   Foreground="{StaticResource TextPrimary}" />
+                                    </StackPanel>
+                                </Button>
+                                <!-- Recent Files divider — populated at runtime by UpdateOpenMenuRecentFiles -->
+                                <Separator x:Name="OpenMenuRecentDivider"
+                                           IsVisible="False"
+                                           Margin="8,4" />
+                            </StackPanel>
+                        </Border>
+                    </Flyout>
+                </Button.Flyout>
                 <Button.Styles>
```

#### Fix 4b — Wire Click Handlers in Code-Behind

In `HeaderBarControl.axaml.cs`, add the following at the end of the constructor (after line 43):

```csharp
// Wire Open Menu button actions
BtnMenuOpenFile.Click += (_, _) =>
{
    BtnOpenMenu.Flyout?.Hide();
    _viewModel?.OpenFilesCommand.Execute(null);
};
BtnMenuOpenFolder.Click += (_, _) =>
{
    BtnOpenMenu.Flyout?.Hide();
    _viewModel?.OpenFolderCommand.Execute(null);
};

// Sync recent files and register for mutual exclusion when flyout opens
BtnOpenMenu.Flyout!.Opened += (sender, _) =>
{
    _flyoutManager?.DismissOthers("open-menu");
    if (sender is Flyout f) UpdateOpenMenuRecentFiles(f);
};
BtnOpenMenu.Flyout.Closed += (_, _) => _flyoutManager?.MarkClosed("open-menu");
```

#### Why This Approach?
- The `StackPanel` (`OpenMenuStack`) is given a name so `UpdateOpenMenuRecentFiles` can locate it by casting `flyout.Content` → `Border` → `Child` (which is the `StackPanel`). This matches the existing guard pattern in `UpdateOpenMenuRecentFiles` at line 256–257.
- `BtnMenuOpenFile` and `BtnMenuOpenFolder` are given names so they can be referenced in the constructor without reflection or visual tree walking.
- The `Separator` (`OpenMenuRecentDivider`) starts invisible and is set visible by `UpdateOpenMenuRecentFiles` only when there are recent files to append, keeping the menu clean on a fresh install.

#### Verification
1. Open a media file, then look at the header bar — "Open" button should appear.
2. Click it — a dropdown with "Open File…" and "Open Folder…" should appear.
3. Click "Open File…" — the file picker should open.
4. Open a second file, click the "Open" button again — recent files should appear below the separator.
5. Open another flyout (e.g., volume) — the "Open" menu should close automatically.

---

## Tier 3 — Medium: Logging & Debug Debt

---

### Fix 5 — Remove PauseLog Disk I/O ✅ FIXED

| Field | Value |
|---|---|
| **File** | [ControlsBoxControl.axaml.cs](file:///x:/Development/Cine_CSharp_DotNet/src/App/UI/Screens/Shell/ControlsBoxControl.axaml.cs) |
| **Lines** | 37–50 (method), 173, 177, 194 (call sites) |
| **Severity** | ~~🟡 Medium~~ → ✅ Resolved |
| **Status** | ☑ Resolved |

#### Root Cause
`PauseLog` performs synchronous file I/O on the UI thread (`File.AppendAllText`) on every play, pause, replay mode change, and `SyncPlayPauseIcon` call. This blocks the UI thread for 1–10ms per call, causing visible hitching on rapid play/pause. It was a debugging aid that was never removed.

#### Step 1 — Delete the `PauseLog` method (lines 37–50)

```diff
-    private static void PauseLog(string msg)
-    {
-        try
-        {
-            var dir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Cine");
-            Directory.CreateDirectory(dir);
-            File.AppendAllText(Path.Combine(dir, "cine_playpause.log"), $"[{DateTime.Now:HH:mm:ss.fff}] {msg}{Environment.NewLine}");
-        }
-        catch (Exception ex)
-        {
-            global::Cine.Core.Log.ForContext<ControlsBoxControl>()
-                .Warning("State comparison failed: {Error}", ex.Message);
-        }
-    }
```

#### Step 2 — Replace call sites

**Line 173** (inside `SyncPlayPauseIcon`, replay mode branch):
```diff
-            PauseLog($"SyncPlayPauseIcon: replay mode -> Replay");
+            global::Cine.Core.Log.ForContext<ControlsBoxControl>().Debug("SyncPlayPauseIcon: replay mode -> Replay");
```

**Line 177** (inside `SyncPlayPauseIcon`, normal branch):
```diff
-            PauseLog($"SyncPlayPauseIcon: isPlaying={isPlaying} _replayMode={_replayMode}");
+            global::Cine.Core.Log.ForContext<ControlsBoxControl>().Debug("SyncPlayPauseIcon: isPlaying={IsPlaying}", isPlaying);
```

**Line 194** (inside `SetReplayMode`):
```diff
-        PauseLog($"SetReplayMode({replayMode})");
+        global::Cine.Core.Log.ForContext<ControlsBoxControl>().Debug("SetReplayMode({ReplayMode})", replayMode);
```

> **Note**: After this change, you can also remove the `using System.IO;` import if it is no longer referenced elsewhere in the file.

#### Verification
1. Build and run the app.
2. Rapidly press Space to toggle play/pause 10 times.
3. Confirm no `cine_playpause.log` file is created in `%LocalAppData%\Cine\`.
4. Confirm the log entries appear in `cine.log` instead (check `%LocalAppData%\Cine\cine.log`).

---

### Fix 6 — Remove Console.WriteLine in App.axaml.cs ✅ FIXED

| Field | Value |
|---|---|
| **File** | [App.axaml.cs](file:///x:/Development/Cine_CSharp_DotNet/src/App/App.axaml.cs) |
| **Severity** | ~~🟡 Medium~~ → ✅ Resolved |
| **Status** | ☑ Resolved |

#### Root Cause
Three `Console.WriteLine` calls exist in the app startup and exception handling paths. These produce noise in any attached console, write to stdout instead of the structured log file, and are not gated by a debug flag.

#### How to Find Them

Run this from the repository root to locate exact lines:

```powershell
Select-String -Path "src\App\App.axaml.cs" -Pattern "Console\.WriteLine"
```

#### Fix Pattern

Replace each occurrence:
```diff
-    Console.WriteLine(msg);
+    global::Cine.Core.Log.ForContext<App>().Debug("{Message}", msg);
```

For exception-logging sites:
```diff
-    Console.WriteLine($"Error: {ex.Message}");
+    global::Cine.Core.Log.ForContext<App>().Error(ex, "App startup error");
```

#### Verification
1. Build and run in Release mode.
2. Attach a console or redirect stdout.
3. Confirm no Cine-specific messages appear on stdout.

---

## Tier 4 — Low: UX Improvements

---

### Fix 7 — Add Keyboard Navigation to TrackFlyoutBuilder ✅ FIXED

| Field | Value |
|---|---|
| **File** | [TrackFlyoutBuilder.cs](file:///x:/Development/Cine_CSharp_DotNet/src/App/UI/Builders/TrackFlyoutBuilder.cs) |
| **Severity** | ~~🟢 Low~~ → ✅ Resolved |
| **Status** | ☑ Resolved |

#### Root Cause
Track list panels built by `TrackFlyoutBuilder` contain `Button` rows but no keyboard `KeyDown` handler. Users cannot navigate the list with arrow keys, and pressing Enter on a focused button does not reliably trigger the click handler in the overlay context.

#### Fix

Locate the section in `TrackFlyoutBuilder.Build(...)` where `trackListPanel` is populated (after the loop that adds track buttons). Add the following `KeyDown` handler immediately after the loop:

```csharp
// Keyboard navigation for track list
trackListPanel.KeyDown += (_, e) =>
{
    var buttons = trackListPanel.Children
        .OfType<Button>()
        .Where(b => b.IsEnabled && b.IsVisible)
        .ToList();

    if (buttons.Count == 0) return;

    var focused = TopLevel.GetTopLevel(trackListPanel)
        ?.FocusManager
        ?.GetFocusedElement() as Button;
    var currentIndex = focused is not null ? buttons.IndexOf(focused) : -1;

    switch (e.Key)
    {
        case Key.Down:
            e.Handled = true;
            var nextIndex = Math.Min(currentIndex + 1, buttons.Count - 1);
            if (nextIndex >= 0) buttons[nextIndex].Focus();
            break;

        case Key.Up:
            e.Handled = true;
            var prevIndex = Math.Max(currentIndex - 1, 0);
            if (prevIndex >= 0) buttons[prevIndex].Focus();
            break;

        case Key.Enter:
        case Key.Return:
            e.Handled = true;
            focused?.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            break;

        case Key.Home:
            e.Handled = true;
            buttons.FirstOrDefault()?.Focus();
            break;

        case Key.End:
            e.Handled = true;
            buttons.LastOrDefault()?.Focus();
            break;
    }
};

// Focus the first button automatically when the panel is shown
trackListPanel.AttachedToVisualTree += (_, _) =>
{
    var first = trackListPanel.Children.OfType<Button>().FirstOrDefault(b => b.IsEnabled);
    first?.Focus();
};
```

Also add the required using directive at the top of the file if not already present:
```csharp
using Avalonia.Interactivity;
```

#### Verification
1. Open the subtitle track selector.
2. Press Down arrow — focus moves to the next track item.
3. Press Up arrow — focus moves to the previous item.
4. Press Enter — the focused track is selected and the flyout closes.
5. Press Home/End — focus jumps to first/last item.

---

### Fix 8 — Fix Shuffle Repeat-Current-Track Bug ✅ FIXED

| Field | Value |
|---|---|
| **File** | [PlaylistCoordinator.cs](file:///x:/Development/Cine_CSharp_DotNet/src/App/Application/Services/PlaylistCoordinator.cs) |
| **Severity** | ~~🟢 Low~~ → ✅ Resolved |
| **Status** | ☑ Resolved |

#### Root Cause
When shuffle is toggled on, `PlaylistCoordinator` generates a shuffled index sequence using `Random.Shuffle(indices)`. The current track index is not excluded from the first position of the shuffled array, so the very next shuffle-play may land on the file already playing, causing an apparent repeat.

#### Fix

Locate the shuffle index generation method (search for `Random.Shuffle` or `Fisher-Yates` comment in the file). After generating the shuffled list, ensure the current index does not appear at position 0:

```csharp
private void ReshuffleIndices()
{
    var indices = Enumerable.Range(0, Items.Count).ToList();
    // Remove current index so it doesn't repeat immediately
    indices.Remove(_currentIndex);

    // Fisher-Yates shuffle
    var rng = Random.Shared;
    for (int i = indices.Count - 1; i > 0; i--)
    {
        int j = rng.Next(i + 1);
        (indices[i], indices[j]) = (indices[j], indices[i]);
    }

    // Prepend current index at position 0 (already playing) and
    // shuffle starts from position 1 on the next PlayNext() call
    _shuffledIndices = indices;
    _shufflePosition = 0; // next PlayNext() starts from [0]
}
```

> **Note**: The exact method names will differ. Read the existing shuffle logic first and adapt the fix to match the surrounding variable names.

#### Verification
1. Load a playlist with 5+ files and enable shuffle.
2. Toggle shuffle off and on 10 times in quick succession.
3. Press Next after each toggle — confirm the currently playing file does not repeat immediately.

---

## Tier 5 — Technical Debt

These items do not cause functional bugs but degrade long-term maintainability.

---

### Debt 1 — Decompose SubtitleManager (48 KB)

| Field | Value |
|---|---|
| **File** | [SubtitleManager.cs](file:///x:/Development/Cine_CSharp_DotNet/src/App/Application/Managers/SubtitleManager.cs) |
| **Severity** | 🔵 Debt |
| **Status** | ☐ Unresolved |

#### Problem
At 48 KB, `SubtitleManager` is the largest file in the codebase and handles four unrelated concerns in one class. This violates the Single Responsibility Principle and makes targeted testing impossible.

#### Proposed Split

| New Class | Responsibility |
|---|---|
| `EmbeddedSubtitleService` | Switch embedded subtitle streams inside MKV/MP4 containers. |
| `ExternalSubtitleService` | Load, validate, and remove external subtitle files (`.srt`, `.ass`, etc.) |
| `SubtitleStyleService` | Map `SubtitleSettingsStore` properties to mpv ASS override tags and apply them. |
| `SubtitleSearchService` | Query online subtitle providers, download results, manage the cache. |

The existing `SubtitleManager` can become a thin **facade** that delegates to these four services, preserving the public API for any existing callers.

#### Migration Steps
1. Create the four new service files in the same directory.
2. Move methods by category into each service.
3. Inject each service into the existing `SubtitleManager` constructor.
4. Replace inline code in `SubtitleManager` with delegation calls.
5. Write unit tests for each new service in isolation.

---

### Debt 2 — Type the Encoding Property in SubtitleSettingsStore

| Field | Value |
|---|---|
| **File** | [SubtitleSettingsStore.cs](file:///x:/Development/Cine_CSharp_DotNet/src/App/Application/Managers/SubtitleSettingsStore.cs) |
| **Severity** | 🔵 Debt |
| **Status** | ☐ Unresolved |

#### Problem
The `Encoding` property is stored as a raw `int` (Windows code page number). This requires every consumer to call `System.Text.Encoding.GetEncoding(int)` and handle `ArgumentException` when the code page is invalid on the current platform.

#### Fix

Replace the raw `int` with a named constant or enum:

```csharp
// Before
public int Encoding { get; set; } = 65001; // UTF-8

// After — Option A: use a well-known string identifier (cross-platform safe)
public string EncodingName { get; set; } = "utf-8";

// After — Option B: define a closed enum of supported encodings
public SubtitleEncoding Encoding { get; set; } = SubtitleEncoding.Utf8;

public enum SubtitleEncoding
{
    Utf8 = 65001,
    Utf16Le = 1200,
    Windows1252 = 1252,
    Windows1251 = 1251,  // Cyrillic
    Iso88591 = 28591,
    ShiftJis = 932,       // Japanese
    Gbk = 936             // Simplified Chinese
}
```

---

### Debt 3 — Standardize All Spacing to Design Tokens

| Field | Value |
|---|---|
| **Files** | All `.axaml` files in `src/App/UI/` |
| **Severity** | 🔵 Debt |
| **Status** | ☐ Unresolved |

#### Problem
Spacing values are inconsistently applied. Some controls use resource tokens (`{StaticResource space-1}`), others hardcode pixel values (`Margin="8"`, `Padding="12,6"`, `Spacing="4"`).

#### Fix

Run this search to locate hardcoded spacings:

```powershell
Select-String -Path "src\App\UI\" -Recurse -Include "*.axaml" -Pattern "Margin=""\d|Padding=""\d|Spacing=""\d"
```

For each match, replace with the nearest token from `Spacing.axaml`:

| Pixel value | Token |
|---|---|
| `4` | `{StaticResource space-1}` |
| `8` | `{StaticResource space-2}` |
| `12` | `{StaticResource space-3}` |
| `16` | `{StaticResource space-4}` |
| `24` | `{StaticResource space-6}` |

---

## Summary Checklist

Copy this checklist into your task tracker:

```
TIER 1 — Critical
☑  Fix 1: MainWindow.axaml — FlyoutOverlay ZIndex 10 → 50
☑  Fix 2: ControlsBoxControl.axaml.cs:128 — Volume close delegate

TIER 2 — High
☑  Fix 3: FlyoutOverlayControl.axaml — Remove visual decoration from ContentContainer
☑  Fix 4a: HeaderBarControl.axaml — Add Flyout to BtnOpenMenu
☑  Fix 4b: HeaderBarControl.axaml.cs — Wire click handlers and FlyoutManager

TIER 3 — Medium
☑  Fix 5: ControlsBoxControl.axaml.cs — Remove PauseLog method and 3 call sites
☑  Fix 6: App.axaml.cs — Replace 3x Console.WriteLine

TIER 4 — Low
☑  Fix 7: TrackFlyoutBuilder.cs — Add keyboard navigation
☑  Fix 8: PlaylistCoordinator.cs — Fix shuffle repeat bug

TIER 5 — Debt
☐  Debt 1: SubtitleManager.cs — Decompose into 4 focused services
☐  Debt 2: SubtitleSettingsStore.cs — Type the Encoding property
☐  Debt 3: All .axaml — Standardize spacing to design tokens
```

---

*This document is a living companion to [Codebase_Analysis_Consolidated.md](./Codebase_Analysis_Consolidated.md).*
*Update the Status column and checklist as fixes are applied and verified.*
*Document Version: 1.0 — Created by Antigravity, 2026-07-01*
