# Cine V3 — UI Functionality & UX Flow Improvement Roadmap

> **Status**: Functionally broken in critical paths. UI element placement is chaotic — buttons crammed without grouping, no visual hierarchy, inconsistent sizing, broken popovers, zero flow between states. Code-behind is 1778-line monolith.
> **Focus**: Fix ALL placement, layout, UX flow, and interaction problems. No visual polish (colors, animations) — pure functional correctness.
> **Rule**: Build must pass after each item. No regressions.

---

## Architecture Diagnosis

### Root Problems

| # | Problem | Severity | Area |
|---|---------|----------|------|
| P1 | No visual hierarchy — transport buttons, tools, and menu buttons mixed in one flat row | **Critical** | Layout |
| P2 | ControlsBox hidden until media loads — user sees nothing but black+StartPage | **Critical** | UX Flow |
| P3 | StartPage instantly disappears when media loads — jarring, no transition | **Critical** | UX Flow |
| P4 | Volume popover is 48px wide with everything vertical — unusable on high-DPI | High | Layout |
| P5 | ChapterPreviewPopover positioned with magic -34px offset — breaks at window edges | High | Layout |
| P6 | Time labels use BOTH Binding AND code-behind — dual source of truth | High | Data Flow |
| P7 | Open Menu Flyout uses MenuItem — doesn't render (same bug as track menus) | High | Rendering |
| P8 | Fullscreen has NO header — no menu access, no window controls, no back button | High | UX Flow |
| P9 | PIP transport buttons are 28px vs 34px on main — inconsistent | Medium | Layout |
| P10 | PIP seek bar has no thumb — just a fill bar, can't see position at a glance | Medium | Layout |
| P11 | Seek thumb IsHitTestVisible=False — can't grab it directly | Medium | Interaction |
| P12 | LoadingSpinner behind StartPage (ZIndex) — never visible | High | Layout |
| P13 | OSD notification overlaps controls bar at bottom | Medium | Layout |
| P14 | ControlsBox gradient has no bottom margin — video goes edge-to-edge | Medium | Layout |
| P15 | No recent files list — must navigate filesystem every time | Medium | UX Flow |
| P16 | Track menus show "No video tracks" even when tracks exist | High | Bug |
| P17 | Escape doesn't close Options menu flyout | Medium | Bug |
| P18 | No keyboard shortcut guide shown anywhere during onboarding | Medium | UX Flow |
| P19 | Playlist dialog empty with no guidance text | Low | UX Flow |
| P20 | Volume slider range (0-130) hidden — user has no idea max is 130 | Low | UX |
| P21 | PIP closes instantly with no confirmation — lose state accidentally | Medium | UX Flow |
| P22 | Finished video (MediaEnd) doesn't show replay button — user must manually seek | Medium | UX Flow |
| P23 | Speed change OSD dismisses in 2s — no time to read | Low | UX |
| P24 | WindowControlsPanel has no margin from right edge | Low | Layout |

---

## Phase 0 — Layout Hierarchy & Element Placement

### 0.1 Redesign Controls Button Order (Critical)
**Current**: 14 buttons in one flat Grid row. Transport (prev/play/next) mixed with volume, subtitles, audio, video, shuffle, loop, playlist, options, fullscreen — zero grouping.

**Problem**: Users can't find what they need. No visual distinction between playback controls, tool toggles, and playback mode buttons.

**Fix**: Group into 4 visually separated sections with tiny dividers:

```
[  |< ]  [ ▶ ]  [ >| ]    |    [ 🔊 ]  [ CC ]  [ 🎵 ]  [ 🎬 ]    |    [ 🔀 ]  [ 🔁 ]  [ 🔂 ]  [ 📋 ]    |    [ ⚙ ]  [ ⛶ ]
  TRANSPORT          |        AUDIO/SUBTITLE          |           PLAYBACK MODE             |        TOOLS
  (left-aligned)     |        (left-aligned)           |           (right of spacer)         |        (far right)
```

**Implementation**: Add tiny vertical separator Borders (Width=1, Height=16, Opacity=0.15) between column groups in the transport Grid:

```xml
<!-- After Video column (6), before Shuffle column (8) -->
<Border Grid.Column="7" Width="1" Height="16" 
        Background="{StaticResource OsdForeground}" Opacity="0.15"
        VerticalAlignment="Center" Margin="4,0" />

<!-- After LoopPlaylist column (9), before LoopFile column (10) -->
<!-- Actually, separate: AUDIO group | MODE group | TOOLS group -->

Better approach: use 3 inner Grid panels instead of 14 columns:
```

**Simpler fix**: Use 3 separate horizontal StackPanels inside a parent StackPanel with Orientation="Horizontal":

```xml
<StackPanel Orientation="Horizontal" Spacing="0">
    <!-- Group 1: Transport -->
    <StackPanel Orientation="Horizontal" Spacing="2">
        <Button x:Name="BtnPrevious" ... />
        <Button x:Name="BtnPlayPause" ... />
        <Button x:Name="BtnNext" ... />
    </StackPanel>
    
    <Rectangle Width="1" Height="20" Fill="#26FFFFFF" VerticalAlignment="Center" Margin="8,0" />
    
    <!-- Group 2: Audio/Subtitle -->
    <StackPanel Orientation="Horizontal" Spacing="2">
        <Button x:Name="BtnVolumeMenu" ... />
        <Button x:Name="BtnSubtitlesMenu" ... />
        <Button x:Name="BtnAudioMenu" ... />
        <Button x:Name="BtnVideoMenu" ... />
    </StackPanel>
    
    <Rectangle Width="1" Height="20" Fill="#26FFFFFF" VerticalAlignment="Center" Margin="8,0" />
    
    <!-- Group 3: Mode -->
    <StackPanel Orientation="Horizontal" Spacing="2">
        <ToggleButton x:Name="BtnShufflePlaylist" ... />
        <ToggleButton x:Name="BtnLoopPlaylist" ... />
        <ToggleButton x:Name="BtnLoopFile" ... />
        <Button x:Name="BtnPlaylistDialog" ... />
    </StackPanel>
    
    <Rectangle Width="1" Height="20" Fill="#26FFFFFF" VerticalAlignment="Center" Margin="8,0" />
    
    <!-- Group 4: Tools -->
    <StackPanel Orientation="Horizontal" Spacing="2">
        <components:OptionsMenuButton ... />
        <ToggleButton x:Name="BtnFullscreen" ... />
    </StackPanel>
</StackPanel>
```

### 0.2 Fix StartPage → VideoHost Transition (Critical)
**Current**: `StartPage.IsVisible = false` and `VideoHost.IsVideoSurfaceVisible = true` happen in the same `Dispatcher.UIThread.Post` — instant swap with no crossfade.

**Problem**: Users see a black flash between StartPage disappearing and video appearing. Feels broken.

**Fix**: Add a 300ms crossfade:
1. StartPage fades out (opacity 1→0 over 250ms)
2. After 100ms delay, VideoHost appears (opacity 0→1 over 300ms)
3. After fade completes, set StartPage.IsVisible = false

```csharp
// In OnMediaOpened
Dispatcher.UIThread.Post(async () =>
{
    StopLoadingSpinner();
    
    // Crossfade: StartPage out, VideoHost in
    if (StartPage != null)
    {
        await FadeVisual(StartPage, 1, 0, 200, true);
        StartPage.IsVisible = false;
        StartPage.Opacity = 1; // Reset for next time
    }
    
    if (VideoHost != null)
    {
        VideoHost.IsVideoSurfaceVisible = true;
        VideoHost.Opacity = 0;
        await FadeVisual(VideoHost, 0, 1, 300, false);
    }
    
    // ... rest
});
```

Also add opacity transition to VideoHost in XAML:
```xml
<ctrl:D3D11VideoHost x:Name="VideoHost"
                     IsVideoSurfaceVisible="False"
                     HorizontalAlignment="Stretch"
                     VerticalAlignment="Stretch"
                     Opacity="0">
    <ctrl:D3D11VideoHost.Transitions>
        <Transitions>
            <DoubleTransition Property="Opacity" Duration="0:0:0.3" />
        </Transitions>
    </ctrl:D3D11VideoHost.Transitions>
</ctrl:D3D11VideoHost>
```

### 0.3 Fix LoadingSpinner ZIndex (Critical)
**Current**: LoadingSpinner has no ZIndex set (defaults to 0). StartPage has ZIndex="10". The spinner sits BEHIND StartPage and is never visible.

**Problem**: When a file is opened, `StartLoadingSpinner()` runs but user sees nothing because StartPage covers it.

**Fix**: Set ZIndex on LoadingSpinner to 20 (above StartPage's 10):
```xml
<Border x:Name="LoadingSpinner" ZIndex="20" ... />
```

Also ensure LoadingSpinner is positioned after StartPage in the XAML child order (last child renders on top).

### 0.4 Fix OSD Notification Position (High)
**Current**: `OsdNotificationBorder` is positioned at `VerticalAlignment="Bottom" Margin="0,0,0,80"`. When controls are visible, the OSD overlaps the ControlsBox.

**Problem**: OSD text is hidden behind ControlsBox when controls are visible (playing state).

**Fix**: Position OSD above ControlsBox when visible, centered vertically when controls are hidden:

```csharp
private void ShowOsdNotification(string text, double durationMs = 2000)
{
    // Move OSD above controls when they're visible
    if (ControlsBox?.IsVisible == true && OsdNotificationBorder != null)
    {
        // Position above the controls box
        OsdNotificationBorder.VerticalAlignment = Avalonia.Layout.VerticalAlignment.Bottom;
        OsdNotificationBorder.Margin = new Thickness(0, 0, 0, 100); // Above controls
    }
    else
    {
        // Center vertically when no controls
        OsdNotificationBorder.VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center;
        OsdNotificationBorder.Margin = new Thickness(0);
    }
    // ... rest of fade logic ...
}
```

### 0.5 Add Bottom Padding to ControlsBox (Medium)
**Current**: ControlsBox gradient goes edge-to-edge at the bottom. Video surface extends underneath.

**Problem**: Looks unfinished. No safe area for the gradient to breathe.

**Fix**: Add bottom padding to the ControlsGrid:
```xml
<Grid x:Name="ControlsGrid" Margin="0,0,0,8">
```

### 0.6 Fix WindowControlsPanel Right Margin (Low)
**Current**: Window buttons (minimize/maximize/close) are flush with the right edge of the window.

**Problem**: Close button is right at the edge — accidental clicks.

**Fix**: Add right margin:
```xml
<StackPanel Orientation="Horizontal" Spacing="0"
            IsVisible="True" x:Name="WindowControlsPanel"
            Margin="0,0,4,0">
```

### 0.7 Redesign Volume Popover Size (High)
**Current**: Volume popover is 48px wide with vertical stack (mute button + slider + label). The slider is 100px tall. Everything is cramped.

**Problem**: On high-DPI or touch, the 48px width makes the slider knob impossible to grab. The mute button is too close to the slider.

**Fix**: Widen to 64px, increase spacing:
```xml
<StackPanel Orientation="Vertical" Spacing="10" Width="64"
            HorizontalAlignment="Center">
    <ToggleButton ... Width="40" Height="40" />   <!-- Bigger mute button -->
    <Slider ... Height="120" />                    <!-- Taller slider -->
    <TextBlock ... FontSize="13" />                <!-- Bigger percentage -->
</StackPanel>
```

---

## Phase 1 — Eliminate Dead Code & Fix Critical Bugs

### 1.1 Remove Dead SeekBarControl
**Problem**: `SeekBarControl.axaml` + `.axaml.cs` were created in Phase 1 but never wired into MainWindow. The `global::Avalonia.Controls.UserControl` qualification caused the XAML source generator to not produce `InitializeComponent()`. It's dead code.

**Fix**: Delete both files. The seek bar already works inline in MainWindow's ControlsBox with direct code-behind positioning.

**Files**: Delete `src/App/UI/Components/SeekBarControl.axaml` and `SeekBarControl.axaml.cs`

### 1.2 Fix Seek Bar Dual Update Paths
**Problem**: Two independent update paths update the seek bar:
1. `_seekUpdateTimer` (DispatcherTimer at 100ms) calls `UpdateSeekBar()` which reads `_lastPosition`/`_lastDuration`
2. `OnPositionChanged` (from mpv event loop) calls `Dispatcher.UIThread.Post` which sets time label min-width

These race. When both fire within the same frame, the seek bar can snap back to 0.

**Fix**: Remove the `_seekUpdateTimer` entirely. Make `OnPositionChanged` the single source of truth for seek bar updates:

Remove from `InitializeAutoHide()`:
```csharp
// DELETE these 5 lines:
_seekUpdateTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(100) };
_seekUpdateTimer.Tick += OnSeekUpdateTick;
_seekUpdateTimer.Start();
```

Delete `OnSeekUpdateTick` method entirely.

Make `OnPositionChanged` the single update path:
```csharp
private void OnPositionChanged(object? sender, PositionChangedEventArgs e)
{
    _lastPosition = e.Position;
    _lastDuration = e.Duration;
    
    Dispatcher.UIThread.Post(() =>
    {
        if (_isSeeking) return;
        UpdateSeekBar();
        UpdateTimeLabels();
    });
}
```

Add `UpdateTimeLabels`:
```csharp
private void UpdateTimeLabels()
{
    if (PositionTimeLabel != null)
        PositionTimeLabel.Text = FormatTimeSpan(_lastPosition);
    if (DurationTimeLabel != null)
        DurationTimeLabel.Text = FormatTimeSpan(_lastDuration);
}
```

### 1.3 Fix Track Menu Rendering
**Problem**: `BuildTrackMenuFlyout` creates `MenuItem` inside `MenuFlyout`. `MenuItem` doesn't render properly when used inside regular `Flyout` — they render as broken text with no hover states.

**Fix**: Replace `BuildTrackMenuFlyout` with styled `Button`-based items inside a regular `Flyout`, matching the primary menu pattern.

### 1.4 Fix Open Menu Flyout Rendering (NEW)
**Current**: `BtnOpenMenu.Flyout` uses `MenuFlyout` with `MenuItem` children — same broken pattern.

**Fix**: Replace with regular `Flyout` containing styled buttons, matching primary menu pattern:
```xml
<Button.Flyout>
    <Flyout Placement="BottomEdgeAlignedLeft">
        <Border Background="{StaticResource PopoverBackground}" CornerRadius="8" Padding="4">
            <StackPanel Width="200">
                <Button Classes="flyout-item" Command="{Binding OpenFilesCommand}">
                    <Grid ColumnDefinitions="Auto,*">
                        <Viewbox Width="14" Height="14">
                            <Path Data="{StaticResource OpenFilesIcon}" Fill="{StaticResource OsdForeground}" />
                        </Viewbox>
                        <TextBlock Grid.Column="1" Text="Open Files" Margin="10,0,0,0" />
                    </Grid>
                </Button>
                <!-- ... more items ... -->
            </StackPanel>
        </Border>
    </Flyout>
</Button.Flyout>
```

---

## Phase 1.5 — UX Flow & State Transitions

### 1.5.1 Fullscreen Redesign (Critical)
**Current**: In fullscreen mode, the ENTIRE header bar is hidden (`HeaderBar.IsVisible = false`). No menu access, no window controls, no PIP button, no title. Only the bottom controls remain. `BtnFullscreenClose` is also hidden.

**Problem**: User enters fullscreen and has NO way to:
1. Access the primary menu (save/exit, shortcuts, preferences)
2. See the window title/filename
3. Know they're in fullscreen (no indicator)
4. Exit fullscreen other than pressing F (no visible button)

**Fix**: Show a **minimal fullscreen header** that auto-hides like the bottom controls:

```xml
<!-- Fullscreen-only header overlay -->
<Border x:Name="FullscreenHeader"
        Classes="fullscreen-header"
        VerticalAlignment="Top" Height="44"
        IsVisible="False"
        Background="{StaticResource HeaderGradient}"
        ZIndex="25">
    <Grid ColumnDefinitions="Auto,*,Auto" Margin="8,0">
        <TextBlock Grid.Column="0" 
                   Text="{Binding Title}" 
                   Foreground="{StaticResource OsdForeground}"
                   FontSize="13" FontWeight="SemiBold"
                   VerticalAlignment="Center" />
        <StackPanel Grid.Column="2" Orientation="Horizontal" Spacing="4">
            <Button Classes="circular-menu" Width="30" Height="30"
                    Click="OnToggleFullscreen"
                    ToolTip.Tip="Exit Fullscreen (F)">
                <Viewbox Width="12" Height="12">
                    <Path Data="{StaticResource FullscreenExitIcon}" Stroke="{StaticResource OsdForeground}" StrokeThickness="1.5" />
                </Viewbox>
            </Button>
            <Button Classes="circular-menu" Width="30" Height="30"
                    ToolTip.Tip="Menu">
                <Viewbox Width="12" Height="12">
                    <Path Data="{StaticResource MenuIcon}" Fill="{StaticResource OsdForeground}" />
                </Viewbox>
                <Button.Flyout>
                    <!-- Same flyout as BtnPrimaryMenu -->
                </Button.Flyout>
            </Button>
        </StackPanel>
    </Grid>
</Border>
```

Update `RefreshFullscreenUi()`:
```csharp
if (_playerService.Player.IsFullscreen)
{
    if (HeaderBar != null) HeaderBar.IsVisible = false;
    if (WindowControlsPanel != null) WindowControlsPanel.IsVisible = false;
    if (FullscreenHeader != null)
    {
        FullscreenHeader.IsVisible = true;
        FullscreenHeader.Opacity = 1;
    }
}
else
{
    if (HeaderBar != null) HeaderBar.IsVisible = true;
    if (WindowControlsPanel != null) WindowControlsPanel.IsVisible = true;
    if (FullscreenHeader != null) FullscreenHeader.IsVisible = false;
}
```

Auto-hide the fullscreen header with the same timer as bottom controls.

### 1.5.2 Add Replay Button on Media End (High)
**Current**: When `OnMediaEnded` fires, it seeks to 0 and pauses. User sees a frozen last frame with no indication the video ended.

**Problem**: User doesn't know the video finished. No way to replay without pressing Play.

**Fix**: Show a centered replay button overlay:
```xml
<!-- Replay overlay -->
<Border x:Name="ReplayOverlay"
        ZIndex="30" IsVisible="False"
        HorizontalAlignment="Center" VerticalAlignment="Center"
        Background="#80000000" CornerRadius="12" Padding="24">
    <StackPanel Spacing="8" HorizontalAlignment="Center">
        <Button Classes="circular-transport" Width="56" Height="56"
                Click="OnReplayClick">
            <Viewbox Width="24" Height="24">
                <Path Data="{StaticResource ReplayIcon}" Fill="{StaticResource OsdForeground}" />
            </Viewbox>
        </Button>
        <TextBlock Text="Replay" Foreground="White" 
                   FontSize="14" HorizontalAlignment="Center" />
    </StackPanel>
</Border>
```

```csharp
private void OnMediaEnded(object? sender, EventArgs e)
{
    Dispatcher.UIThread.Post(() =>
    {
        _playerService?.Player?.Seek(TimeSpan.Zero);
        _playerService?.Player?.Pause();
        ShowUiControls();
        if (ReplayOverlay != null) ReplayOverlay.IsVisible = true;
    });
}

private void OnReplayClick(object? sender, RoutedEventArgs e)
{
    if (ReplayOverlay != null) ReplayOverlay.IsVisible = false;
    _playerService?.Player?.Seek(TimeSpan.Zero);
    _playerService?.Player?.Play();
}
```

### 1.5.3 Add Recent Files to Start Page (Medium)
**Problem**: Every time user opens Cine, they must navigate through the file system to find their videos. No recent files list.

**Fix**: Add a "Recent" section to the StartPage, populated from a JSON file:

```csharp
// In MainViewModel
private static string RecentPath => Path.Combine(
    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
    "Cine", "recent.json");

public ObservableCollection<string> RecentFiles { get; } = new();

public void AddRecentFile(string path)
{
    RecentFiles.Remove(path); // Move to top if exists
    RecentFiles.Insert(0, path);
    while (RecentFiles.Count > 10) RecentFiles.RemoveAt(RecentFiles.Count - 1);
    SaveRecentFiles();
}

private void SaveRecentFiles()
{
    try { File.WriteAllText(RecentPath, JsonSerializer.Serialize(RecentFiles.ToList())); } catch { }
}

public void LoadRecentFiles()
{
    try
    {
        if (!File.Exists(RecentPath)) return;
        var json = File.ReadAllText(RecentPath);
        var list = JsonSerializer.Deserialize<List<string>>(json);
        if (list != null) { RecentFiles.Clear(); foreach (var f in list) RecentFiles.Add(f); }
    } catch { }
}
```

StartPage XAML addition:
```xml
<!-- Recent files section -->
<StackPanel Spacing="4" Margin="0,16,0,0" IsVisible="{Binding HasRecentFiles}">
    <TextBlock Text="Recent" FontSize="12" FontWeight="SemiBold" 
               Foreground="#80FFFFFF" Margin="0,0,0,4" />
    <ItemsControl ItemsSource="{Binding RecentFiles}">
        <ItemsControl.ItemTemplate>
            <DataTemplate>
                <Button Content="{Binding}" Background="Transparent"
                        Foreground="#CCFFFFFF" FontSize="12"
                        HorizontalContentAlignment="Left" Padding="8,4"
                        Command="{Binding OpenRecentCommand}"
                        CommandParameter="{Binding}" />
            </DataTemplate>
        </ItemsControl.ItemTemplate>
    </ItemsControl>
</StackPanel>
```

### 1.5.4 Add Keyboard Shortcut Hint on Start Page (Low)
**Problem**: New users don't know they can press Space to play, F for fullscreen, etc.

**Fix**: Add a small hint text at the bottom of StartPage:
```xml
<TextBlock Text="Press Space to play  ·  F for fullscreen  ·  Esc to exit"
           FontSize="11" Foreground="#60FFFFFF"
           HorizontalAlignment="Center" VerticalAlignment="Bottom"
           Margin="0,0,0,24" />
```

---

## Phase 2 — Refactor MainWindow (Break Up Monolith)

### 2.1 Extract Seek Bar Logic into Partial Class
**Problem**: All seek bar code (~200 lines) lives in MainWindow.axaml.cs.

**Fix**: Create `MainWindow.SeekBar.cs` partial class file with all seek-related methods.

### 2.2 Extract Keyboard Handler into Partial Class
**Problem**: `OnKeyDown` is a 120-line switch statement.

**Fix**: Create `MainWindow.Keyboard.cs` partial. Replace switch statement with `Dictionary<KeyGesture, Action>` lookup table.

### 2.3 Extract Drag-and-Drop & Auto-Hide into Partial Classes
**Problem**: Drag-and-drop handlers (~80 lines) + auto-hide logic (~100 lines) mixed in main window.

**Fix**: Create `MainWindow.DragDrop.cs` and `MainWindow.AutoHide.cs` partials.

---

## Phase 3 — Fix PIP Architecture

### 3.1 Safe PIP Initialization
**Problem**: `OnPipVideoHostReady` uses `Task.Run` to call `_pipPlayer.InitializeRenderer(hwnd)`. If the hwnd becomes invalid (window closed during init), it crashes.

**Fix**: Use async initialization with `CancellationToken`.

### 3.2 PIP Auto-Pause Main Player
**Problem**: When entering PIP mode, main player continues playing. Both instances play audio simultaneously.

**Fix**: Pause main player when PIP opens, resume when PIP closes.

### 3.3 PIP Uniform Buttons & Seek Thumb (NEW)
**Problem**: PIP transport buttons are 28px (main uses 34px). PIP seek bar has no thumb — just a fill bar.

**Fix**: 
- All PIP buttons: change to 32px to match main window style
- Add thumb to PIP seek bar:
```xml
<Border x:Name="PipSeekThumb"
        Width="10" Height="10" CornerRadius="5"
        Background="{StaticResource ProgressSliderBackground}"
        HorizontalAlignment="Left" VerticalAlignment="Center"
        IsHitTestVisible="False" />
```

### 3.4 PIP Transport Sync (Two-Way)
**Problem**: PIP prev/next buttons seek main player by ±30s, but PIP doesn't re-sync after.

**Fix**: After seeking main player, wait 300ms then re-sync PIP to main position.

---

## Phase 4 — Session & State Improvements

### 4.1 Auto-Resume Without Click
**Problem**: Session resume requires user to click OSD notification. Users expect auto-resume.

**Fix**: Auto-resume after 4 seconds if user doesn't click.

### 4.2 Persist Playlist
**Problem**: Playlist is lost when app restarts.

**Fix**: Save/load playlist alongside session in session.json.

### 4.3 Remove Magic Delay in OpenFile
**Problem**: `await Task.Delay(50)` in `OpenFile` with no explanation.

**Fix**: Replace with proper await on the player's `Opened` event with timeout.

---

## Phase 5 — Error Handling & Edge Cases

### 5.1 Player Initialization Failure
**Problem**: If `MpvPlayer()` constructor fails, the app silently shows a black window.

**Fix**: Show error dialog and close.

### 5.2 File Open Error Feedback
**Problem**: When `_player.Open(path)` fails, nothing is shown to user.

**Fix**: Wire `player.Error` event to OSD notification.

### 5.3 Fix Loading Spinner Race
**Problem**: `OnViewModelPropertyChanged` and `OnMediaOpened` both control the spinner. They can race.

**Fix**: Add `_isLoading` guard flag.

### 5.4 Fix Volume Icon on Startup
**Problem**: `RefreshVolumeIcon()` runs before media loads. `_viewModel.VolumeValue` is 0, so mute cross shows incorrectly.

**Fix**: Only check `IsMuted`, not `VolumeValue == 0`:
```csharp
private void RefreshVolumeIcon()
{
    if (_viewModel == null) return;
    bool isMuted = _viewModel.IsMuted; // Don't check VolumeValue == 0
    if (VolumeArcsPath != null) VolumeArcsPath.IsVisible = !isMuted;
    if (VolumeMuteCrossPath != null) VolumeMuteCrossPath.IsVisible = isMuted;
}
```

### 5.5 Fix Escape Closes Options Flyout (NEW)
**Problem**: `CloseOpenFlyouts()` doesn't close the Options menu flyout (it's managed by the `OptionsMenuButton` component internally).

**Fix**: The OptionsMenuButton should expose a `CloseFlyout()` method, or track its flyout state:
```csharp
// In OptionsMenuButton.axaml.cs
public void CloseFlyout()
{
    if (BtnOptionsMenu?.Flyout is Flyout fly && fly.IsOpen)
        fly.Hide();
}
```

Then in MainWindow:
```csharp
private void CloseOpenFlyouts()
{
    BtnOpenMenu?.Flyout?.Hide();
    BtnPrimaryMenu?.Flyout?.Hide();
    BtnVolumeMenu?.Flyout?.Hide();
    BtnOptionsMenu?.CloseFlyout();
    // Subtitles/Audio/Video use MenuFlyout — hide via their buttons
    BtnSubtitlesMenu?.Flyout?.Hide();
    BtnAudioMenu?.Flyout?.Hide();
    BtnVideoMenu?.Flyout?.Hide();
}
```

### 5.6 Fix Track Menu "No video tracks" Bug (NEW)
**Problem**: Track menus always show "No video tracks" / "No audio tracks" / "No subtitle tracks" as the first item, even when tracks exist. The `BuildEmptyTrackMenus()` method adds pseudo-entries, but `OnTrackListChanged` doesn't remove them — it appends real tracks after them.

**Fix**: In `OnTrackListChanged`, clear the collections before rebuilding:
```csharp
// Fix: Clear before adding real tracks
SubtitleTracks.Clear();
AudioTracks.Clear();
VideoTracks.Clear();

// Then rebuild with real tracks
```

---

## Phase 6 — Quick Wins (Low Effort, High Impact)

### 6.1 Make Seek Thumb Draggable
**Problem**: `SeekThumb.IsHitTestVisible="False"` — user can't grab the thumb directly.

**Fix**: Remove `IsHitTestVisible="False"` from SeekThumb.

### 6.2 Fix Double-Tap After Seek
**Problem**: If user clicks seek bar and then taps to play, the `_lastTapTime` may still be within 300ms.

**Fix**: Reset `_lastTapTime` on seek interaction.

### 6.3 Fix ChapterPreviewPopover Edge Clipping (NEW)
**Problem**: ChapterPreviewPopover uses `Margin="0,-34,0,0"` to position above the seek bar. When near the left or right edge, it clips outside the window.

**Fix**: Clamp the popover position to stay within SeekArea bounds:
```csharp
// In OnSeekAreaPointerMoved
var xPos = (normalized * trackWidth) - (popoverWidth / 2);
xPos = Math.Clamp(xPos, 4, Math.Max(4, SeekArea.Bounds.Width - popoverWidth - 4));
ChapterPreviewPopover.Margin = new Thickness(xPos, -34, 0, 0);
```

### 6.4 Fix Escape Priority Order
**Problem**: Escape should dismiss flyouts first, THEN exit fullscreen. Currently reversed.

**Fix**:
```csharp
if (key == Key.Escape)
{
    if (_activeFlyouts > 0)
        CloseOpenFlyouts();
    else if (_playerService?.Player?.IsFullscreen == true)
        _viewModel?.ToggleFullscreen();
    e.Handled = true;
}
```

### 6.5 Add Keyboard Repeat for Seek
**Problem**: Holding Left/Right arrow seeks once. Should repeat when held.

**Fix**: Add 90ms debounce guard for repeat:
```csharp
private DateTime _lastSeekRepeat = DateTime.MinValue;

// In OnKeyDown for Left/Right:
if (key == Key.Left)
{
    var now = DateTime.UtcNow;
    if ((now - _lastSeekRepeat).TotalMilliseconds < 90) return;
    _lastSeekRepeat = now;
    Handle(() => _viewModel?.SeekBackward());
}
```

### 6.6 Add Playlist Empty State
**Problem**: Opening playlist dialog with no items shows empty window.

**Fix**: Add centered text when no items.

### 6.7 Increase Speed OSD Duration (NEW)
**Problem**: Speed change OSD notification lasts 2 seconds — too short to read.

**Fix**: Increase to 3 seconds:
```csharp
ShowOsdNotification($"Speed: {_viewModel.SpeedValue:F1}x", 3000);
```

### 6.8 Add Volume Range Indicator (NEW)
**Problem**: Volume slider goes to 130 with no indication of the max value.

**Fix**: Show the max value in the tooltip and label:
```xml
<TextBlock Text="{Binding VolumeValue, StringFormat='{}{0:F0} / 130 %'}" ... />
```

---

## Implementation Order

| Order | Phase | Item | Effort | Impact |
|-------|-------|------|--------|--------|
| 1 | 0.3 | Fix LoadingSpinner ZIndex | 1 min | Critical |
| 2 | 5.3 | Fix loading spinner race | 5 min | Critical |
| 3 | 1.1 | Delete dead SeekBarControl | 2 min | Build fix |
| 4 | 1.2 | Fix seek bar dual update | 15 min | Critical |
| 5 | 1.5.1 | Fullscreen redesign (minimal header) | 30 min | Critical |
| 6 | 0.1 | Redesign controls button groups | 20 min | High |
| 7 | 0.2 | Fix StartPage→VideoHost transition | 15 min | High |
| 8 | 5.5 | Fix Escape closes all flyouts | 5 min | High |
| 9 | 5.6 | Fix track menu "No tracks" bug | 5 min | High |
| 10 | 1.3 | Fix track menu rendering | 30 min | High |
| 11 | 1.4 | Fix Open Menu flyout rendering | 15 min | High |
| 12 | 0.4 | Fix OSD position overlap | 10 min | High |
| 13 | 1.5.2 | Add replay button on media end | 20 min | High |
| 14 | 3.2 | PIP auto-pause main player | 5 min | High |
| 15 | 5.4 | Fix volume icon on startup | 5 min | High |
| 16 | 6.4 | Fix Escape priority | 2 min | High |
| 17 | 1.5.3 | Add recent files | 30 min | Medium |
| 18 | 3.3 | PIP uniform buttons & thumb | 10 min | Medium |
| 19 | 6.3 | Fix chapter preview edge clip | 5 min | Medium |
| 20 | 6.8 | Add volume range indicator | 2 min | Medium |
| 21 | 5.1 | Player init error handling | 15 min | Medium |
| 22 | 5.2 | File open error feedback | 5 min | Medium |
| 23 | 3.1 | Safe PIP initialization | 20 min | Medium |
| 24 | 6.5 | Keyboard repeat for seek | 10 min | Medium |
| 25 | 0.5 | Add bottom padding to ControlsBox | 2 min | Medium |
| 26 | 0.6 | Fix WindowControlsPanel margin | 2 min | Low |
| 27 | 0.7 | Redesign volume popover size | 10 min | Low |
| 28 | 1.5.4 | Add keyboard shortcut hint on start page | 5 min | Low |
| 29 | 6.1 | Make seek thumb draggable | 1 min | Low |
| 30 | 6.2 | Fix double-tap after seek | 1 min | Low |
| 31 | 6.7 | Increase speed OSD duration | 1 min | Low |
| 32 | 6.6 | Add playlist empty state | 10 min | Low |
| 33 | 4.1 | Auto-resume session | 15 min | Low |
| 34 | 4.2 | Persist playlist | 20 min | Low |
| 35 | 2.1–2.4 | Break up MainWindow into partials | 45 min | Low |
| 36 | 3.4 | PIP two-way transport sync | 10 min | Low |
| 37 | 4.3 | Remove magic delay in OpenFile | 15 min | Low |

---

## Build Verification

After each item, run:
```
dotnet build
```

Expected: 0 errors, warnings kept below 3.

## Files Modified/Created

| File | Changes |
|------|---------|
| `SeekBarControl.axaml` | DELETE |
| `SeekBarControl.axaml.cs` | DELETE |
| `MainWindow.axaml` | ZIndex on spinner; VideoHost transitions; fullscreen header; replay overlay; controls group layout; volume popover size; OpenMenu flyout; bottom padding; window controls margin |
| `MainWindow.axaml.cs` | Remove seek timer; add crossfade; fix OSD position; fix fullscreen UI; fix Escape; fix track menus; add replay; fix loading guard; fix volume icon; fix CloseOpenFlyouts |
| `MainWindow.SeekBar.cs` | NEW — all seek methods |
| `MainWindow.Keyboard.cs` | NEW — Dictionary-based shortcuts |
| `MainWindow.DragDrop.cs` | NEW — drag-drop handlers |
| `MainWindow.AutoHide.cs` | NEW — auto-hide logic |
| `OptionsMenuButton.axaml.cs` | Add `CloseFlyout()` method |
| `MainViewModel.cs` | Remove magic delay; persist playlist; add recent files; fix track menu clear |
| `StartPage.axaml` | Add recent files section; add keyboard shortcut hint |
| `PipWindow.axaml` | Uniform 32px buttons; add seek thumb; safe init with CancellationToken |
| `PipWindow.axaml.cs` | CancellationToken; auto-pause main; two-way sync |
| `PlaylistDialog.axaml` | Add empty state text |
