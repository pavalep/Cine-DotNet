# Cine Media Player — Suggested UI/UX Enhancements & Premium Roadmap

This guide outlines recommended premium improvements for the Cine Media Player C# / Avalonia application to align it with global UX standards.

---

## 1. Options Menu UI Overhaul (Visual Cleanliness & Grouping)

### Noted Problem
The current "Options" menu is a single, tall ScrollViewer containing a flat list of 12+ rows. 
* It uses 10+ identical "reset" icons, creating repetitive visual noise.
* The `NumericUpDown` controls look heavy, clunky, and occupy too much space.
* Scrolling is required, making adjustments slow and visually overwhelming.

### Suggested Solution
1. **Tabbed Navigation:** Split the options into three clean tabs at the top of the flyout:
   * **🎬 Video** (Aspect Ratio, Rotate, Flip, Zoom, Contrast, Brightness, Saturation, Hue, Gamma)
   * **🎵 Audio** (Audio Delay, Speed pills)
   * **💬 Subtitles** (Subtitle Delay, Font Size)
2. **Simplified Resets:** Remove individual reset buttons from each row. Replace them with a single **"Reset Section"** or **"Reset Tab"** button at the top/bottom of each tab view.
3. **Compact Sliders & Segmented Pills:**
   * Replace `NumericUpDown` for video adjustments (contrast, brightness, zoom) with compact horizontal sliders accompanied by a small text label showing the active percentage.
   * Group aspect ratio options into a grid of clean segmented buttons (e.g. `Original`, `16:9`, `4:3`, `2.35:1`) instead of a drop-down combo box.

### Proposed XAML Layout (Video Tab Example)
```xml
<TabControl Classes="options-tabs">
    <TabItem Header="Video">
        <StackPanel Spacing="10" Padding="8">
            <Button Content="Reset Video Settings" Classes="flat-action" Click="OnResetVideoClick" />
            <!-- Brightness Slider -->
            <Grid ColumnDefinitions="Auto,*,Auto">
                <TextBlock Text="Brightness" FontSize="12" Width="80" VerticalAlignment="Center"/>
                <Slider Grid.Column="1" Value="{Binding BrightnessValue}" Minimum="-100" Maximum="100" Height="24" />
                <TextBlock Grid.Column="2" Text="{Binding BrightnessValue, StringFormat='{}{0:+0;-0;0}'}" FontSize="12" Width="30" TextAlignment="Right"/>
            </Grid>
            <!-- Contrast Slider -->
            <Grid ColumnDefinitions="Auto,*,Auto">
                <TextBlock Text="Contrast" FontSize="12" Width="80" VerticalAlignment="Center"/>
                <Slider Grid.Column="1" Value="{Binding ContrastValue}" Minimum="-100" Maximum="100" Height="24" />
                <TextBlock Grid.Column="2" Text="{Binding ContrastValue, StringFormat='{}{0:+0;-0;0}'}" FontSize="12" Width="30" TextAlignment="Right"/>
            </Grid>
        </StackPanel>
    </TabItem>
    <TabItem Header="Audio">
        <!-- Audio settings & speed pills -->
    </TabItem>
    <TabItem Header="Subtitles">
        <!-- Subtitle delay & font size -->
    </TabItem>
</TabControl>
```

---

## 2. Controls Auto-Hide & Hover Reveal ("Hover Top/Bottom Fix")

### Noted Problem
In `MainWindow.AutoHide.cs`, any mouse movement triggers `ShowUiControls()`. This causes the controls bar and header bar to constantly flash on and off even if the user just slightly nudges the mouse in the middle of the screen.

### Suggested Solution
Implement **Edge-Triggered Hover Zones**:
* **Top Hover Zone (Top 15% of Window Height):** Only show the `HeaderBarControl` when the mouse enters the top region of the window.
* **Bottom Hover Zone (Bottom 15% of Window Height):** Only show the `ControlsBoxControl` (seek bar, transport buttons) when the mouse enters the bottom region of the window.
* **Middle Zone:** Keep controls hidden unless a mouse click occurs, allowing a completely immersive, distraction-free viewing experience.
* **Smooth Slide Transitions:** Animate the position using a `RenderTransform` (TranslateY) transition in addition to Opacity, so the bars smoothly slide down from the top and up from the bottom when hovered.

### Proposed Code Adjustment in `MainWindow.AutoHide.cs`
```csharp
private void OnWindowPointerMoved(object? sender, PointerEventArgs e)
{
    var pos = e.GetCurrentPoint(this).Position;
    double windowHeight = Bounds.Height;

    // Check if mouse is in top 15% (header zone) or bottom 15% (controls zone)
    bool inHeaderZone = pos.Y <= windowHeight * 0.15;
    bool inControlsZone = pos.Y >= windowHeight * 0.85;

    if (!_uiVisible)
    {
        if (inHeaderZone || inControlsZone)
        {
            ShowUiControls();
        }
        return;
    }

    // Adjust visibility timers or individual panel states
    _isMouseOverControls = inHeaderZone || inControlsZone;

    if (Math.Abs(pos.X - _lastMousePosition.X) > 1 || Math.Abs(pos.Y - _lastMousePosition.Y) > 1)
    {
        _lastMousePosition = pos;
        _autoHideTimer?.Stop();
        _autoHideTimer?.Start();
    }
}
```

---

## 3. Right-Click Context Menu (Desktop Standard)

### Noted Problem
Modern desktop media players (VLC, IINA, MPC-HC) allow users to access core playback options, tracks, aspect ratios, and speed directly via a right-click context menu over the video screen. Currently, Cine has no right-click menu.

### Suggested Solution
Attach a `ContextMenu` to the `VideoClickOverlay` border. This menu will host standard shortcuts:
* **Play / Pause**
* **Aspect Ratio** (Flyout menu with options)
* **Audio Track** (Sub-menu loaded dynamically)
* **Subtitle Track** (Sub-menu loaded dynamically)
* **Playback Speed** (Sub-menu with multipliers)
* **Fullscreen (F)**
* **Always on Top**

### Proposed XAML (`MainWindow.axaml`)
```xml
<Border x:Name="VideoClickOverlay"
        Background="Transparent"
        ZIndex="5">
    <Border.ContextMenu>
        <ContextMenu x:Name="PlayerContextMenu" Theme="{DynamicResource FluentContextMenuTheme}">
            <MenuItem Header="Play / Pause" Command="{Binding PlayPauseCommand}" InputGesture="Space" />
            <Separator />
            <MenuItem Header="Aspect Ratio" x:Name="CtxAspectMenu">
                <MenuItem Header="Original" Click="OnCtxAspectClick" Tag="-1" />
                <MenuItem Header="16:9" Click="OnCtxAspectClick" Tag="1.7778" />
                <MenuItem Header="4:3" Click="OnCtxAspectClick" Tag="1.3333" />
                <MenuItem Header="2.35:1" Click="OnCtxAspectClick" Tag="2.35" />
            </MenuItem>
            <MenuItem Header="Speed">
                <MenuItem Header="0.5x" Click="OnCtxSpeedClick" Tag="0.5" />
                <MenuItem Header="1.0x" Click="OnCtxSpeedClick" Tag="1.0" />
                <MenuItem Header="1.5x" Click="OnCtxSpeedClick" Tag="1.5" />
                <MenuItem Header="2.0x" Click="OnCtxSpeedClick" Tag="2.0" />
            </MenuItem>
            <Separator />
            <MenuItem Header="Fullscreen" Click="OnToggleFullscreen" InputGesture="F" />
            <MenuItem Header="Always on Top" Click="OnToggleAlwaysOnTop" />
        </ContextMenu>
    </Border.ContextMenu>
</Border>
```

---

## 4. Advanced Seek Bar & Chapter Visualization

### Concept
Standard seek bars are plain lines. Premium players divide the seek bar into visual segments representing chapters, and display the chapter name dynamically as the mouse scrubs along the timeline.

### Suggested Solution
* **Segmented Track:** Instead of a single continuous progress bar, render the seek track as a series of visual segments bounded by chapter markers, leaving a tiny `1px` transparent gap between them.
* **Hover Chapter Previews:** When the mouse hovers over the seek bar, display the chapter title and timestamp in a floating tooltip that follows the horizontal position of the cursor.

---

## 5. Premium Picture-in-Picture (PiP) Window

### Concept
Cine includes a PiP window, but it can be enhanced to feel integrated with the operating system's native window controls and behavior.

### Suggested Solutions
* **Snap-to-Edge:** Implement dragging physics on the PiP Window that automatically snaps it to the nearest screen corner (top-left, top-right, bottom-left, bottom-right) when released, with a smooth animation.
* **Auto-Dimming Hover States:** When the mouse cursor is *not* over the PiP window, fade its opacity to `80%` and hide all playback overlay controls to ensure it doesn't distract from other tasks. When hovered, animate the opacity back to `100%` and fade in the transport controls.

---

## 6. Smart Audio Enhancements

### 1. dialogue Boost & Speech Clarity
* **Concept:** Dynamic audio range compression to make dialogue louder and explosions/background music softer (ideal for late-night viewing).
* **Implementation:** Add a "Speech Booster" option that activates mpv's built-in `acompressor` filter command:
  ```csharp
  _player.Command("af", "toggle", "lavfi=[acompressor=threshold=-20dB:ratio=4:makeup=8dB]");
  ```

### 2. Software Volume Pre-Amplification (Audio Boost)
* **Concept:** Allow volume levels up to 200% for extremely quiet video files, with dynamic limiting to prevent digital clipping/distortion.
* **Implementation:** Extend the `VolumeMax` slider limit to 200 and hook up an auto-limiter filter (`softclip`) when exceeding 100%.

---

## 7. Dynamic Ambient Accent Tinting

### Concept
Just like Apple QuickTime and premium streaming players, extract the dominant color scheme from the currently playing video frames and apply a subtle, blurred ambient glow or gradient background behind the controls panel to fit the mood of the movie.

### Suggested Solution
* Periodically extract a low-resolution thumbnail from the mpv renderer (e.g. every 5 seconds) using `screenshot-to-file` or RGB pixel polling.
* Calculate the average/dominant color using a quick pixel-average algorithm.
* Update the accent resource brush with a low-opacity variant of that color, letting the UI elements dynamically tint to match the video.

---

## 8. Advanced Subtitle Gestures & Custom Styling

### 1. Subtitle Drag-to-Position
* **Concept:** Allow users to grab subtitles on-screen and drag them vertically to reposition them (useful to move subtitles out of the way of burned-in text).
* **Implementation:** Track drag gestures on the video screen area and translate them into mpv's `sub-pos` property (from `0` to `100`).

### 2. Subtitle Styling Dialog
* **Concept:** Provide a settings popover to configure font family (Serif, Sans-Serif, Monospaced), outline color, text shadow, and background translucent boxes for improved readability.
* **Implementation:** Map these to mpv's native properties: `sub-color`, `sub-border-color`, `sub-border-size`, `sub-back-color`.

---

## 9. Binge-Watching & Playback Convenience

### 1. "Skip Intro" & "Skip Recap" Buttons
* **Concept:** Detect intros or recaps based on chapter titles (e.g., "Opening", "Prologue", "Recap") and show a floating button in the bottom-right corner to skip ahead instantly.
* **Implementation:** Listen for chapter changes. If the active chapter title contains keywords like "intro", "opening", or "recap", show a "Skip Intro" border that seeks to the start of the next chapter when clicked.

### 2. Auto-Play Next Episode
* **Concept:** When a file in a playlist is within 10 seconds of ending, display a "Next episode playing in 5..." overlay.
* **Implementation:** Monitor playback position and trigger the next item in the playlist automatically, displaying a countdown overlay with a cancel option.

### 3. Sleep Timer / Auto-Shutdown
* **Concept:** Stop playback or close the application after a specified duration (e.g. 15m, 30m, 60m) or at the end of the current movie.
