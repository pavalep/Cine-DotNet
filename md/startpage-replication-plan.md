# StartPage Replication Plan

> Tracking the fidelity of [StartPage.html](../web/StartPage.html) → [StartPage.axaml](../src/App/UI/Components/Start/StartPage.axaml)

---

## Phase 1: Typography & Fonts

- [x] **1.1** Use two distinct font families (`Outfit` for display, `Inter` for UI) instead of single `font-family-default`

  **Debug:** Open the XAML preview and verify the wordmark "SIMBA" renders in a different typeface than body text. If both look the same, the resource mapping is wrong.
  
  **Fix:** Add `font-family-display` and `font-family-ui` resources in App.axaml. Apply display font to wordmark/tagline, UI font to all other text.

- [x] **1.2** Change wordmark `FontWeight` from `Black` (900) to `Bold` (700)

  **Debug:** Screenshot the wordmark side-by-side with the HTML version in a browser at 100% zoom. If the XAML wordmark looks noticeably thicker, the weight is still too high.

  **Fix:** Set `FontWeight="Bold"` on the "SIMBA" `TextBlock` in both WideLayout and NarrowLayout.

- [x] **1.3** Wordmark font size should be fluid (`clamp(28px, 3.5vw, 52px)`) instead of fixed `52`/`36`

  **Debug:** Resize the window between 800px and 1600px wide. If the wordmark stays at exactly 52px then jumps to 36px, fluid sizing is not working.

  **Fix:** Bind `FontSize` to a `Double` property in code-behind that scales linearly with `Bounds.Width` (clamped between 28 and 52). Or use a `ScaleTransform` on the wordmark `TextBlock` driven by a `LayoutUpdated` handler.

- [x] **1.4** Tagline font size should be fluid (`clamp(14px, 1.2vw, 18px)`) instead of fixed `17`/`14`

  **Debug:** Same resize test as 1.3 but observing the "Play anything." text. If it snaps between discrete sizes, fluid sizing is missing.

  **Fix:** Apply the same bound/clamped approach as 1.3 with range 14–18.

---

## Phase 2: Background & Atmosphere

- [x] **2.1** Add the missing linear gradient (170deg) to the background

  **Debug:** Open the HTML in a browser and the XAML preview at the same window size. If the XAML background looks flatter or has a different color temperature, the linear gradient is absent.

  **Fix:** Add a `Rectangle` filled with a `LinearGradientBrush` matching: `StartPoint="0,0" EndPoint="1,1"` with stops at `#08080A`, `#0C0C0E` (50%), `#0F0F12` (100%), placed behind all other background layers.

- [x] **2.2** Add SVG noise texture overlay (`.bg::after` at 2.5% opacity)

  **Debug:** Zoom in to 200% on both versions. If the XAML background looks perfectly smooth while the HTML shows subtle grain, the noise overlay is missing.

  **Fix:** Render the noise SVG to a `DrawingImage` resource or use an `Image` with a pre-rendered noise PNG. Set `Opacity="0.025"`, `IsHitTestVisible="False"`, stretch to fill.

- [x] **2.3** Vignette should match HTML's inset box-shadow shape more closely

  **Debug:** Compare the darkness falloff at the corners of both versions. If the XAML vignette is more circular while the HTML vignette has a rectangular inset feel, they don't match.

  **Fix:** Adjust the `RadialGradientBrush` center and stops, or use a `Border` with a `BorderBrush` that uses a `LinearGradientBrush` on each edge to approximate the inset box-shadow.

- [x] **2.4** Glass panel should have `backdrop-filter: blur(24px)`

  **Debug:** Place a busy image/content behind the glass panel area. If the glass panel background is a static translucent color with no blur, backdrop blur is absent.

  **Fix:** Avalonia does not natively support `BackdropBlur`. Evaluate using `BlurBehind` (Windows-only) or a custom `BlurEffect` shader. If not feasible, document as a known platform limitation.

---

## Phase 3: Animations

- [x] **3.1** Add app-container-level fade-in (`appFadeIn`, 500ms, 100ms delay)

  **Debug:** On app startup, watch if the entire page pops in instantly vs. fading in smoothly. If instant, the animation is missing.

  **Fix:** Add a `Style` for the root `Border#StartPageRoot` with an `Animation` that transitions `Opacity` from 0 to 1 over 500ms with 100ms delay.

- [x] **3.2** Logo wrapper needs its own **staggered** animation separate from the BrandPanel

  **Debug:** Watch the entrance animation frame-by-frame. If the logo and wordmark appear at exactly the same time, there is no stagger.

  **Fix:** Create a separate `Style` targeting the logo `Border` (give it an `x:Name`) with its own animation: 500ms duration, 150ms delay (before the BrandPanel's 250ms).

- [x] **3.3** Glow orb animation should use 3 keyframes with translateX, translateY, scale, and opacity over 12s

  **Debug:** Compare orb movement between versions over a 12-second loop. If the XAML orb only bobs vertically with no horizontal drift or scale change, the animation is simplified.

  **Fix:** Expand the orb `Animation` to 3 `KeyFrame` entries at 0%, 50%, 100%. Add `ScaleTransform` alongside `TranslateTransform`. Change `Duration="0:0:6"` to `Duration="0:0:12"`.

- [x] **3.4** Honor `prefers-reduced-motion`

  **Debug:** Enable "Reduce motion" in the OS accessibility settings and restart the app. If animations still play, the preference is not respected.

  **Fix:** Query platform reduced-motion setting in code-behind. If enabled, set a property that conditionally disables all entrance animations (e.g., set `FillMode="None"` or remove animations programmatically).

---

## Phase 4: Glow Orb

- [x] **4.1** Orb position should match HTML (`top: 20%; right: -10%`) more precisely

  **Debug:** Overlay a screenshot of the HTML orb on the XAML orb. If the XAML orb is significantly further off-screen or at a different vertical position, positioning is off.

  **Fix:** Replace `Margin="0,-100,-100,0"` with `VerticalAlignment="Top"` plus a `RenderTransform` or a container `Grid` with proportional row/column definitions. Target ~20% from top, ~10% past the right edge.

---

## Phase 5: Layout Grid (Wide Mode)

- [x] **5.1** Match HTML's 3-column grid structure more closely

  **Debug:** Draw horizontal guidelines at column boundaries in both versions. If the XAML has a visible fixed 32px gap column that doesn't exist in HTML, the grid differs.

  **Fix:** Change `ColumnDefinitions="*,32,Auto,*"` to `ColumnDefinitions="*,Auto,*"` and absorb the gap into margins or padding on the center column.

- [x] **5.2** Brand horizontal position should match left-column placement of HTML

  **Debug:** Measure the distance of the "SIMBA" wordmark from the left edge of the window at a few sizes. If the XAML brand is offset differently, the column math is wrong.

  **Fix:** Keep brand in column 0 (or 1 if keeping 4 cols) and ensure the column ratios produce the same proportional position as the HTML `1fr` left column.

- [x] **5.3** Recent section should span full page width at the bottom, not be nested in the right column

  **Debug:** Look at the recent cards at 1400px window width. If they are constrained to the right panel area (~480px) instead of spanning the full page width, the nesting is wrong.

  **Fix:** Move the recent section out of the right-column `StackPanel` and place it in the root `Grid` at `Grid.Row="2" Grid.ColumnSpan="3"` (or full span), matching the HTML grid-row: 3 / grid-column: 1 / -1.

---

## Phase 6: Glass Panel

- [x] **6.1** Panel should use `MaxWidth="500"` instead of fixed `Width="480"`

  **Debug:** Shrink the window to 600px wide. If the panel doesn't scale down and gets clipped, it's using fixed width. If it shrinks but caps at 500px, max-width is working.

  **Fix:** Replace `Width="480"` with `MaxWidth="500"` and add `HorizontalAlignment="Stretch"`.

- [x] **6.2** Panel padding should be fluid (`clamp(28px, 3vw, 40px)`) instead of fixed `28,24,28,28`

  **Debug:** Resize the window from 800px to 1600px. If the inner content margins stay at exactly 28px/24px, padding is static.

  **Fix:** Bind `Margin` or `Padding` to a `Thickness` property computed from the panel's `Bounds.Width`.

- [x] **6.3** Hover effect should add an accent-border glow (expanded box-shadow), not just change BorderBrush

  **Debug:** Hover the mouse over the glass panel. If only the border color changes subtly but there's no "glow" spread, the expanded box-shadow effect is missing.

  **Fix:** On `:pointerover`, add a second `DropShadowEffect` (or increase existing blur radius) with accent color. Or use a `Border` behind the panel that becomes visible on hover.

- [x] **6.4** Inner highlight is implemented differently but functionally equivalent — verify parity

  **Debug:** Inspect the top edge of the glass panel at 400% zoom. If the XAML version has a distinctly visible 1px line instead of a subtle 0.5px glow, adjust.

  **Fix:** Fine-tune the highlight `Border` height (try 0.5 via `ScaleTransform`) and opacity to match the HTML `inset 0 0.5px 0 var(--glass-highlight)`.

---

## Phase 7: Drop Zone

- [x] **7.1** Drop zone hover should transition border-color to accent AND background to accent-dim AND icon color to accent

  **Debug:** Hover over the drop zone. If only the background changes (or nothing changes), the full hover feedback is incomplete.

  **Fix:** Add `Style` selectors for `Border#DropZone:pointerover` that set `BorderBrush="{StaticResource StartAccentBorder}"` and `Background="{StaticResource StartAccentDim}"`. Add a `Style` selector for the `MaterialIcon` inside a hovered DropZone to change `Foreground="{StaticResource StartAccent}"`.

- [x] **7.2** Drop zone icon should match HTML's custom SVG upload icon more closely

  **Debug:** Compare the upload icon side-by-side. If the Material `UploadOutline` icon has different stroke weight, proportions, or style than the HTML SVG, they don't match.

  **Fix:** Export the HTML SVG as a `DrawingImage` resource and use an `Image` control instead of `MaterialIcon`. Or find a closer `MaterialIcon` variant.

- [x] **7.3** Drop zone hint text styling should use distinct spacing matching HTML's `.drop-zone-hint` class

  **Debug:** Measure the gap between "Drag media here" and "or use the buttons below". If the spacing differs from the HTML, adjust.

  **Fix:** Set `Spacing="2"` or `Margin="2,0,0,0"` on the hint `TextBlock` to match the HTML `margin-top: 2px`.

---

## Phase 8: Action Buttons

- [x] **8.1** "Open Media" button should use glass style (translucent bg, border, light text), not solid bronze fill with black text

  **Debug:** Compare the Open Media button between versions. If the XAML button is a solid warm-gold block while HTML is a translucent glass button with white text, they completely mismatch.

  **Fix:** Change `Background="{StaticResource StartGlassBg}"` (or `AppHoverSubtle`), add `BorderBrush="{StaticResource StartGlassBorder}"` + `BorderThickness="0.5"`, change text `Foreground="{StaticResource AppTextPrimary}"` and icon `Foreground` to light color.

- [x] **8.2** "Open Folder" button background should be `Transparent`, not `AppHoverSubtle`

  **Debug:** Toggle between versions. If the XAML Open Folder button has a visible tinted background while HTML looks fully transparent, it's wrong.

  **Fix:** Set `Background="Transparent"` on `BtnOpenFolder`.

- [x] **8.3** Button icons should start at 0.7 opacity and transition to 1.0 on hover

  **Debug:** Hover over each button and watch the icon. If the icon opacity doesn't change, the transition is missing.

  **Fix:** Add `Opacity="0.7"` to button icons. Add `:pointerover` styles that set `Opacity="1"`. Add `DoubleTransition` for `Opacity` on the icon.

- [x] **8.4** Add focus-visible ring: `box-shadow: 0 0 0 2px accent` equivalent

  **Debug:** Tab through the buttons with the keyboard. If there is no visible focus indicator, it's missing.

  **Fix:** Add a `Style Selector="Button:focus-visible"` that sets a `BorderBrush="{StaticResource StartAccent}"` with `BorderThickness="2"`.

- [x] **8.5** Buttons should flex-fill the panel width equally, not use fixed `MinWidth="150"`

  **Debug:** If one button text is much shorter than the other and they're not the same width, flex-fill is not working.

  **Fix:** Set `HorizontalAlignment="Stretch"` on both buttons inside a `Grid` with equal `ColumnDefinitions="*,*"`, replacing the `StackPanel` orientation.

---

## Phase 9: Recent Section

- [x] **9.1** Recent section should be full-width at page bottom (grid row 3, span all columns), not nested in right column

  **Debug:** Same as 5.3. If recent cards are trapped in the right panel column, the layout is wrong.

  **Fix:** Same as 5.3 — move `RecentSection` to the root `Grid`.

- [x] **9.2** Recent count badge should show just the number (e.g., "8"), not "0 items"

  **Debug:** Check the count badge text. If it reads "8 items" instead of just "8", the format is wrong.

  **Fix:** Change `Text="{Binding RecentCount}"` or update code-behind to set the bare number instead of "N items".

- [x] **9.3** Recent track scrollbar should be hidden, matching HTML

  **Debug:** If a horizontal scrollbar is always visible below the recent cards, it's not hidden.

  **Fix:** Set `HorizontalScrollBarVisibility="Hidden"` instead of `"Auto"` on the recent track `ScrollViewer`.

- [x] **9.4** Verify media card rendering fidelity in code-behind

  **Debug:** Compare a rendered media card side-by-side with HTML. Check: width (180px), aspect-ratio (16:10 thumbnail), blurred cover image overlay, title truncation with ellipsis, meta row (duration · lastOpened). Flag any mismatch.

  **Fix:** Adjust code-behind card template: set `Width="180"`, thumbnail `Height` bound to width for 16:10 ratio, add blur effect on cover image, `TextWrapping="NoWrap"` with `TextTrimming="CharacterEllipsis"`, use `·` separator in meta.

---

## Phase 10: Empty State

- [x] **10.1** Add empty state UI for when no recent media exists

  **Debug:** Clear all recent media and observe the recent section. If the track area is just blank/empty with no message, the empty state is missing.

  **Fix:** Add a `TextBlock` with text matching `.empty-state` styling (12px, tertiary color, centered) that is visible when `RecentTracks.Children.Count == 0`. Show an icon + message like "No recent media" using the same layout as `.empty-state`.

---

## Phase 11: Keyboard Hint

- [x] **11.1** Decide platform convention for keyboard hint (Mac `⌘` vs Windows `Ctrl`)

  **Debug:** On macOS, look at the keyboard hint. If it says "Ctrl" instead of showing the `⌘` symbol, it's wrong for Mac users.

  **Fix:** Detect OS at runtime. On macOS, show `⌘ O Open file`. On Windows/Linux, show `Ctrl+O Open file`. Use a bound property or code-behind to swap the text.

---

## Phase 12: Drag-and-Drop

- [x] **12.1** Evaluate whether the XAML `DropTarget` overlay approach should be replaced with inline drop-zone highlighting (matching HTML)

  **Debug:** Drag a file over the window. If a large "Drop to Play" overlay appears instead of the drop zone itself changing appearance, the UX differs from HTML.

  **Fix (Option A):** Remove the `DropTarget` overlay and implement inline highlighting on `DropZone` (set `IsVisible` + accent styling on drag-enter).  
  **Fix (Option B):** Keep the overlay but ensure the inline drop zone also highlights simultaneously for consistency.

- [x] **12.2** Verify drag-counter logic in code-behind matches HTML's nested-element handling

  **Debug:** Drag a file in and out of child elements within the window rapidly. If the drag-over state gets stuck or flickers, the counter logic is incorrect.

  **Fix:** Review code-behind drag event handlers. Implement a drag-counter pattern (increment on `DragEnter`, decrement on `DragLeave`, reset on `Drop`) matching the HTML `dragCounter` variable.

---

## Phase 13: Responsive Breakpoints

- [x] **13.1** Add intermediate breakpoint at ~1024px for tablet-optimized layout

  **Debug:** Resize the window to 1000px. If the layout is still in full wide mode (brand left, panel right) while HTML switches to centered single-column at 1024px, the breakpoint is missing.

  **Fix:** In code-behind `LayoutUpdated`, add a check for `Bounds.Width <= 1024` that switches to a `TabletLayout` (centered brand, single column, smaller elements) distinct from both Wide and Narrow.

- [x] **13.2** Add large desktop breakpoint around 1600px+ with expanded layoutsizes

  **Debug:** Resize the window to 1800px. If the panel stays at 480px and cards stay at default size, large-desktop optimization is missing.

  **Fix:** Add a check for `Bounds.Width >= 1600` that increases panel `MaxWidth` to 560px, increases card width to 200px, and scales up padding proportionally.

- [x] **13.3** Change narrow breakpoint from 820px to 768px to match HTML mobile breakpoint

  **Debug:** Resize to 800px. If the layout already switched to narrow mode (stacked vertically) while HTML still shows the tablet layout, the breakpoint triggers too early.

  **Fix:** Change the narrow-layout threshold from 820 to 768 in code-behind.

---

## Phase 14: Miscellaneous

- [x] **14.1** Remove or reduce the brand logo `DropShadowEffect` glow to match HTML (no glow)

  **Debug:** Look at the lion logo. If it has a visible bronze ambient glow behind it that's absent in the HTML, it's an unwanted addition.

  **Fix:** Remove the `Border.Effect` `DropShadowEffect` from the logo `Border`, or reduce `Opacity` to `0.05` if a very subtle glow is desired as an enhancement.

- [x] **14.2** Glass panel shadow should match HTML's negative-spread shadow

  **Debug:** Compare panel shadows side-by-side. If the XAML shadow has a softer/wider spread and doesn't tighten toward the panel, the negative spread is missing.

  **Fix:** Avalonia `DropShadowEffect` doesn't support spread. Approximate by reducing `BlurRadius` slightly (try 32) and increasing `Opacity` (try 0.6), or layer two `DropShadowEffect`s.

- [x] **14.3** Confirm `Ctrl/Cmd+O` keyboard shortcut is implemented in code-behind

  **Debug:** Press Ctrl+O (or Cmd+O on Mac). If no file dialog opens, the shortcut is not wired up.

  **Fix:** Add a `KeyDown` handler on the root control that checks for `Ctrl+O` or `Cmd+O` and invokes `BtnOpenFile_Click`.

- [x] **14.4** Confirm demo data seeding behavior in code-behind

  **Debug:** Clear all app data and restart with no recent files. If the recent section is completely empty instead of showing demo cards, seeding is missing.

  **Fix:** In code-behind initialization, if `RecentMedia.Count == 0`, populate with demo items matching the HTML demo data (Interstellar, Dune, RAM, etc.).

- [x] **14.5** Hidden file inputs are not needed (platform difference) — document as intentional

  **Debug:** N/A — this is an architectural difference.

  **Fix:** No code change needed. Ensure the XAML uses platform-native file dialogs (`OpenFileDialog`, `OpenFolderDialog`) via code-behind button click handlers, which is the correct Avalonia approach.

---

## Progress Summary

| Phase | Total | Done | Left |
|-------|-------|------|------|
| 1  | 4 | 4 | 0 |
| 2  | 4 | 4 | 0 |
| 3  | 4 | 4 | 0 |
| 4  | 1 | 1 | 0 |
| 5  | 3 | 3 | 0 |
| 6  | 4 | 4 | 0 |
| 7  | 3 | 3 | 0 |
| 8  | 5 | 5 | 0 |
| 9  | 4 | 4 | 0 |
| 10 | 1 | 1 | 0 |
| 11 | 1 | 1 | 0 |
| 12 | 2 | 2 | 0 |
| 13 | 3 | 3 | 0 |
| 14 | 5 | 5 | 0 |
| **All** | **44** | **44** | **0** |
