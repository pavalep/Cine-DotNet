# StartPage UI Overhaul — Phased Execution Plan

> **Goal:** Deliver an Apple-quality, fully responsive StartPage for the Cine Avalonia desktop app.
> Every font, spacing, element, and layout section must respond fluidly to window resize — exactly like React Native's flexbox-first layout model.
>
> **File under review:** [`StartPage.axaml`](../src/App/Views/Pages/StartPage.axaml)
> **Tracking:** Mark each step `[x]` when complete. Mark `[~]` when in-progress.

---

## Quick Reference — Key Files

| File | Purpose |
|---|---|
| `src/App/Views/Pages/StartPage.axaml` | Main file being overhauled |
| `src/App/Views/Pages/StartPage.axaml.cs` | Code-behind (window controls, keyboard, drag-drop) |
| `src/App/Views/Resources/Typography.axaml` | Global font family tokens |
| `src/App/Views/Resources/Colors.axaml` | Global color brushes |
| `src/App/Views/Resources/Radius.axaml` | Corner radius tokens |
| `src/App/Views/Resources/Sizes.axaml` | Global size tokens |
| `src/App/Views/Shell/MainWindow.axaml.cs` | Breakpoint definitions (XS/S/M/L/XL/XXL) |
| `src/App/App.csproj` | Package references |

---

## Breakpoint Reference (Already Configured in MainWindow)

```
XS   = 0 px   (tiny — very small window)
S    = 400 px
M    = 495 px
L    = 600 px  (default min window size)
XL   = 1024 px
XXL  = 1400 px
```

AVVI94 markup extension syntax:
```xml
xmlns:a="https://github.com/AVVI94"
Value="{a:Breakpoint XS=48, S=56, M=64, L=72, XL=80, XXL=80}"
```

---

## Responsive Sizing Targets

| Token | XS | S | M | L | XL | XXL |
|---|---|---|---|---|---|---|
| Logo size (px) | 48 | 56 | 64 | 72 | 80 | 80 |
| Wordmark font (sp) | 22 | 26 | 30 | 34 | 38 | 40 |
| Tagline font (sp) | 11 | 12 | 13 | 14 | 15 | 16 |
| Button width (px) | 150 | 170 | 190 | 200 | 210 | 220 |
| Button height (px) | 38 | 40 | 42 | 44 | 46 | 46 |
| Button font (sp) | 12 | 12 | 13 | 14 | 14 | 14 |
| Button icon (px) | 15 | 16 | 17 | 18 | 18 | 18 |
| Page margin H | 20 | 28 | 36 | 44 | 48 | 56 |
| Page margin V-top | 12 | 16 | 24 | 32 | 40 | 48 |
| Page margin V-bot | 20 | 28 | 36 | 48 | 60 | 72 |
| Brand spacing | 6 | 8 | 10 | 12 | 14 | 16 |
| Button panel spacing | 8 | 10 | 10 | 12 | 12 | 12 |
| Section spacing | 8 | 10 | 10 | 12 | 12 | 12 |
| Recent header font | 10 | 10 | 11 | 11 | 11 | 11 |
| Card meta font | 8 | 8 | 9 | 9 | 9 | 9 |
| Glow orb size | 180 | 220 | 260 | 290 | 320 | 360 |
| Kbd font | 9 | 9 | 10 | 10 | 10 | 10 |

---

---

# PHASE 1 — Diagnosis & Audit

> Understand every current broken pattern before touching code.

---

### Step 1.1 — Document current broken behaviors

- [ ] Open app and resize window from full-screen down to 600×420 (min size)
- [ ] Confirm: wordmark text does **NOT** scale — stays at 40px at all sizes
- [ ] Confirm: logo does **NOT** scale — stays at 80px at all sizes
- [ ] Confirm: button panel overflows or disappears at low heights
- [ ] Confirm: Recent section `ScrollViewer` height is unconstrained (no virtualization)
- [ ] Screenshot current state at three sizes: 1200×800, 800×600, 650×450

---

### Step 1.2 — Audit all hardcoded sizing tokens in UserControl.Resources

- [ ] List every `<sys:Double>` token in `StartPage.axaml` lines 48–81
- [ ] For each token, note: Is it applied via `{StaticResource}` or inline?
- [ ] Confirm all 14 tokens are static (not reactive) — root cause documented

**Tokens to audit:**
```
StartBrandSpacing, StartLogoSize, StartWordmarkFontSize,
StartTaglineFontSize, StartButtonPanelSpacing, StartButtonWidth,
StartIconSize, StartButtonFontSize, StartRecentSpacing,
StartRecentHeaderFontSize, StartRecentCountFontSize,
StartCardPadding, StartPlayIconSize, StartCardTitleFontSize,
StartMetaFontSize, StartScrollPadding, StartKbdFontSize,
StartKbdHintFontSize
```

---

### Step 1.3 — Audit layout structure problems

- [ ] Verify outer `FlexPanel` with `JustifyContent="Center"` — causes content to float, no compression
- [ ] Verify `StackPanel#RecentSection` with `Flex.Grow="1"` — StackPanel ignores grow constraint
- [ ] Verify `ScrollViewer` inside RecentSection has no constrained height
- [ ] Verify `ItemsRepeater` — confirm virtualization never activates (no bounded viewport)
- [ ] Verify the global `FlexPanel > :is(Control)` style conflict with inner `HorizontalAlignment="Center"`

---

### Step 1.4 — Audit typography & font consistency

- [ ] Check: Is `Outfit` font installed system-wide on this machine?
- [ ] Check: Are font family tokens (`font-family-display`) ever applied in StartPage? (They are NOT currently)
- [ ] Check: All TextBlock elements — are they using style classes or inline `FontSize`?
- [ ] Determine: Embed Outfit font as project asset OR rely on system font?

---

---

# PHASE 2 — Layout Architecture Rebuild

> Replace the broken outer layout with a `Grid`-based structure.
> This mirrors how React Native's `flex: 1` ScrollView pattern works — guaranteed space allocation per section.

---

### Step 2.1 — Replace outer `FlexPanel` with `Grid`

**Current structure:**
```xml
<labs:FlexPanel Direction="Column"
                JustifyContent="Center"
                AlignItems="Stretch"
                Margin="48,40,48,60">
    <StackPanel x:Name="BrandPanel"    labs:Flex.Grow="0" />
    <StackPanel x:Name="ButtonPanel"   labs:Flex.Grow="0" />
    <StackPanel x:Name="RecentSection" labs:Flex.Grow="1" />
</labs:FlexPanel>
```

**Target structure:**
```xml
<Grid x:Name="MainContentPanel"
      HorizontalAlignment="Stretch"
      VerticalAlignment="Stretch"
      RowDefinitions="Auto,Auto,*">
      <!-- Row 0: Brand  — takes its natural height -->
      <!-- Row 1: Buttons — takes its natural height -->
      <!-- Row 2: Recent — fills all remaining space (*) -->
</Grid>
```

**Tasks:**
- [ ] Remove `labs:FlexPanel` element (lines 374–607 in current file)
- [ ] Add `Grid` wrapper with `RowDefinitions="Auto,Auto,*"`
- [ ] Remove all `labs:Flex.Grow` attached properties from child panels
- [ ] Assign `Grid.Row="0"` to `BrandPanel`, `Grid.Row="1"` to `ButtonPanel`, `Grid.Row="2"` to `RecentSection`
- [ ] Verify: Grid still fills the full parent (HorizontalAlignment/VerticalAlignment="Stretch")
- [ ] Remove the global style `labs|FlexPanel > :is(Control)` (lines 325–328) — no longer needed
- [ ] Build and confirm no compile errors

---

### Step 2.2 — Fix Recent Section height constraint

**Current problem:**
```xml
<!-- StackPanel grows to infinity — ScrollViewer gets infinite space -->
<StackPanel x:Name="RecentSection" labs:Flex.Grow="1" Orientation="Vertical">
    <Grid ... />          <!-- Header — Auto -->
    <TextBlock ... />     <!-- Empty state — Auto -->
    <ScrollViewer ... />  <!-- Gets leftover, but StackPanel doesn't constrain it -->
</StackPanel>
```

**Target structure:**
```xml
<Grid x:Name="RecentSection" Grid.Row="2">
    <Grid.RowDefinitions>
        <RowDefinition Height="Auto" />   <!-- Header label + count badge -->
        <RowDefinition Height="Auto" />   <!-- Empty state text -->
        <RowDefinition Height="*" />      <!-- ScrollViewer — fills remaining -->
    </Grid.RowDefinitions>

    <Grid Grid.Row="0" ... />         <!-- Header -->
    <TextBlock Grid.Row="1" ... />    <!-- Empty state -->
    <ScrollViewer Grid.Row="2" ... /> <!-- Horizontal card track -->
</Grid>
```

**Tasks:**
- [ ] Replace `StackPanel#RecentSection` with `Grid#RecentSection`
- [ ] Set `Grid.RowDefinitions="Auto,Auto,*"` on the new Grid
- [ ] Assign `Grid.Row` to Header Grid, EmptyState TextBlock, and ScrollViewer
- [ ] Remove `Orientation="Vertical"` and `ClipToBounds="False"` from old StackPanel
- [ ] Set `VerticalAlignment="Stretch"` on the `ScrollViewer`
- [ ] Verify: At 600×420 window, cards are visible and scrollable horizontally
- [ ] Verify: `ItemsRepeater` receives a bounded viewport — virtualization now active

---

### Step 2.3 — Fix Button Panel centering

**Current problem (double-centering):**
```xml
<StackPanel x:Name="ButtonPanel" labs:Flex.Grow="0" Orientation="Horizontal">
    <!-- Inner StackPanel with HorizontalAlignment="Center" -->
    <StackPanel Orientation="Horizontal"
                HorizontalAlignment="Center"
                Spacing="...">
        <Button .../> <Button .../>
    </StackPanel>
</StackPanel>
```

**Target (single centering):**
```xml
<StackPanel x:Name="ButtonPanel"
            Grid.Row="1"
            Orientation="Horizontal"
            HorizontalAlignment="Center">
    <Button x:Name="BtnOpenFile"  ... />
    <Button x:Name="BtnOpenFolder" ... />
</StackPanel>
```

**Tasks:**
- [ ] Flatten the double `StackPanel` nesting — merge inner into outer
- [ ] Move `HorizontalAlignment="Center"` to the outer `ButtonPanel`
- [ ] Move `Spacing` from inner to outer panel
- [ ] Verify buttons are centered at all window widths

---

### Step 2.4 — Fix the outer margin to be responsive

**Current:**
```xml
Margin="48,40,48,60"
```

**Target (breakpoint-driven):**
```xml
<Grid.Margin>
    <a:Breakpoint XS="20,12,20,20"
                  S="28,16,28,28"
                  M="36,24,36,36"
                  L="44,32,44,48"
                  XL="48,40,48,60"
                  XXL="56,48,56,72" />
</Grid.Margin>
```

**Tasks:**
- [ ] Replace static `Margin="48,40,48,60"` on outer panel with Breakpoint markup
- [ ] Add `xmlns:a="https://github.com/AVVI94"` to the `UserControl` root if not present
- [ ] Test: At XS/S sizes, margin tightens and more space is given to content
- [ ] Test: At XL+, margin expands for a comfortable, wide-canvas feel

---

---

# PHASE 3 — Responsive Sizing (Fonts & Elements)

> Replace every `{StaticResource sys:Double}` token with `{a:Breakpoint}` values.
> This is the direct equivalent of React Native's `useWindowDimensions` approach.

---

### Step 3.1 — Remove all `sys:Double` tokens from `UserControl.Resources`

- [ ] Delete lines 59–81 in current `StartPage.axaml` (all `sys:Double` sizing tokens)
- [ ] Keep the brush/color resources (lines 17–57) — those don't need to change
- [ ] Keep the `CardWidth`, `ThumbWidth`, `ThumbHeight` tokens — cards are fixed-width by design
- [ ] Keep `NegateConverter` resource
- [ ] Build — expect compile errors where `{StaticResource}` still references deleted tokens (that's fine, fix in next steps)

---

### Step 3.2 — Logo: Responsive size

**Current:**
```xml
<Style Selector="Border#BrandLogoBorder">
    <Setter Property="Width"  Value="{StaticResource StartLogoSize}" />
    <Setter Property="Height" Value="{StaticResource StartLogoSize}" />
</Style>
<Style Selector="Svg#BrandLogo">
    <Setter Property="Width"  Value="{StaticResource StartLogoSize}" />
    <Setter Property="Height" Value="{StaticResource StartLogoSize}" />
</Style>
```

**Target (inline on element):**
```xml
<Border x:Name="BrandLogoBorder"
        CornerRadius="{StaticResource radius-xl}"
        HorizontalAlignment="Center"
        Width="{a:Breakpoint XS=48, S=56, M=64, L=72, XL=80, XXL=80}"
        Height="{a:Breakpoint XS=48, S=56, M=64, L=72, XL=80, XXL=80}">
    <svg:Svg x:Name="BrandLogo"
             Stretch="Uniform"
             Path="/Assets/simba-logo.svg"
             CurrentColor="{StaticResource Bronze}"
             Width="{a:Breakpoint XS=48, S=56, M=64, L=72, XL=80, XXL=80}"
             Height="{a:Breakpoint XS=48, S=56, M=64, L=72, XL=80, XXL=80}" />
</Border>
```

**Tasks:**
- [ ] Remove `Style Selector="Border#BrandLogoBorder"` from `UserControl.Styles`
- [ ] Remove `Style Selector="Svg#BrandLogo"` width/height setters from `UserControl.Styles`
- [ ] Apply `{a:Breakpoint}` directly on `Border#BrandLogoBorder` Width/Height attributes
- [ ] Apply matching `{a:Breakpoint}` on `Svg#BrandLogo` Width/Height
- [ ] Test: Logo visually shrinks at small window heights

---

### Step 3.3 — Wordmark: Responsive font size

**Current:**
```xml
<Style Selector="TextBlock#WordmarkText">
    <Setter Property="FontSize" Value="{StaticResource StartWordmarkFontSize}" />
</Style>
```

**Target:**
```xml
<TextBlock x:Name="WordmarkText"
           FontSize="{a:Breakpoint XS=22, S=26, M=30, L=34, XL=38, XXL=40}"
           FontFamily="{StaticResource font-family-display}"
           FontWeight="Bold"
           LetterSpacing="8"
           TextAlignment="Center"
           Foreground="{StaticResource AppTextPrimary}" />
```

**Tasks:**
- [ ] Remove `Style Selector="TextBlock#WordmarkText"` from `UserControl.Styles`
- [ ] Apply `FontSize="{a:Breakpoint ...}"` directly on the `TextBlock#WordmarkText` element
- [ ] Replace `FontFamily="Outfit"` with `FontFamily="{StaticResource font-family-display}"`
- [ ] Test: Text scales smoothly as window is resized

---

### Step 3.4 — Tagline: Responsive font size

**Tasks:**
- [ ] Remove `Style Selector="TextBlock#TaglineText"` from `UserControl.Styles`
- [ ] Apply `FontSize="{a:Breakpoint XS=11, S=12, M=13, L=14, XL=15, XXL=16}"` inline
- [ ] Replace `FontFamily="Outfit"` with `FontFamily="{StaticResource font-family-display}"`
- [ ] Confirm `TextWrapping="Wrap"` is retained

---

### Step 3.5 — Brand panel spacing: Responsive

**Tasks:**
- [ ] Remove `Style Selector="StackPanel#BrandPanel"` spacing setter from `UserControl.Styles` (keep animations)
- [ ] Apply `Spacing="{a:Breakpoint XS=6, S=8, M=10, L=12, XL=14, XXL=16}"` directly on `BrandPanel`

---

### Step 3.6 — Buttons: Responsive width, height, font, icon size

**Current:**
```xml
<Style Selector="Button.PremiumBtn">
    <Setter Property="Height" Value="46" />
    ...
</Style>
<Style Selector="Button#BtnOpenFile">
    <Setter Property="Width" Value="{StaticResource StartButtonWidth}" />
</Style>
```

**Target:**
```xml
<Button x:Name="BtnOpenFile"
        Width="{a:Breakpoint XS=150, S=170, M=190, L=200, XL=210, XXL=220}"
        Height="{a:Breakpoint XS=38, S=40, M=42, L=44, XL=46, XXL=46}"
        ...>
    <StackPanel Orientation="Horizontal" Spacing="10" HorizontalAlignment="Center">
        <materialIcons:MaterialIcon Kind="PlayCircleOutline"
            Width="{a:Breakpoint XS=15, S=16, M=17, L=18, XL=18, XXL=18}"
            Height="{a:Breakpoint XS=15, S=16, M=17, L=18, XL=18, XXL=18}"
            Foreground="{StaticResource StartAccent}" />
        <TextBlock Text="Open Media"
                   FontSize="{a:Breakpoint XS=12, S=12, M=13, L=14, XL=14, XXL=14}"
                   ... />
    </StackPanel>
</Button>
```

**Tasks:**
- [ ] Remove `Style Selector="Button#BtnOpenFile"` width setter from `UserControl.Styles`
- [ ] Remove `Style Selector="Button#BtnOpenFolder"` width setter from `UserControl.Styles`
- [ ] Remove the fixed `Height="46"` from `.PremiumBtn` style — it will be set inline
- [ ] Apply `Width` breakpoint on `BtnOpenFile` inline
- [ ] Apply `Width` breakpoint on `BtnOpenFolder` inline
- [ ] Apply `Height` breakpoint on both buttons inline
- [ ] Apply `FontSize` breakpoint on both button TextBlocks
- [ ] Apply `Width`/`Height` breakpoint on both button MaterialIcon elements
- [ ] Apply button panel `Spacing="{a:Breakpoint XS=8, S=10, M=10, L=12, XL=12, XXL=12}"`

---

### Step 3.7 — Recent section header: Responsive font

**Tasks:**
- [ ] Remove `Style Selector="TextBlock#RecentCountText"` font setter from `UserControl.Styles`
- [ ] Apply `FontSize="{a:Breakpoint XS=10, S=10, M=11, L=11, XL=11, XXL=11}"` on header TextBlock
- [ ] Apply matching font size on `TextBlock#RecentCountText`
- [ ] Apply `Margin="{a:Breakpoint XS=0,12,0,6, M=0,16,0,8, XL=0,20,0,10}"` on `RecentSection`

---

### Step 3.8 — Card meta font: Responsive (minor)

**Tasks:**
- [ ] Apply `FontSize="{a:Breakpoint XS=8, S=8, M=9, L=9, XL=9, XXL=9}"` on card title TextBlock
- [ ] Apply matching size on meta tag TextBlocks
- [ ] (Card width stays fixed at `180` — horizontal scrolling handles overflow)

---

### Step 3.9 — Glow orb: Responsive size

**Current:**
```xml
<Ellipse x:Name="GlowOrb" Width="320" Height="320" ... />
```

**Target:**
```xml
<Ellipse x:Name="GlowOrb"
         Width="{a:Breakpoint XS=180, S=220, M=260, L=290, XL=320, XXL=360}"
         Height="{a:Breakpoint XS=180, S=220, M=260, L=290, XL=320, XXL=360}"
         ... />
```

**Tasks:**
- [ ] Replace static `Width="320" Height="320"` with Breakpoint markup on `GlowOrb`

---

### Step 3.10 — Keyboard hint: Responsive font

**Tasks:**
- [ ] Apply `FontSize="{a:Breakpoint XS=9, S=9, M=10, L=10, XL=10, XXL=10}"` on `KbdModifierText`
- [ ] Apply matching size on the "O" key TextBlock and "Open file" hint TextBlock
- [ ] Scale `Margin` on `KbdHint` border: `{a:Breakpoint XS=0,0,12,12, M=0,0,16,16, XL=0,0,24,24}`

---

---

# PHASE 4 — Visual Polish (Apple-Grade)

> Fine-tune spacing, depth, and motion to feel premium at every size.

---

### Step 4.1 — Improve background depth layers

**Current issue:** Three radial gradient overlays are very subtle (`#0A`, `#04`, `#06` alpha).

**Target:** Slightly increase warmth and depth without losing the dark cinematic feel.

- [ ] Adjust `BgRadialWarm` center stop from `#0AC9A96E` → `#12C9A96E` (more warmth at center)
- [ ] Add a subtle top-edge linear gradient (like iOS home screen top fade):
  ```xml
  <Rectangle IsHitTestVisible="False">
      <Rectangle.Fill>
          <LinearGradientBrush StartPoint="0,0" EndPoint="0,0.3">
              <GradientStop Offset="0"   Color="#08FFFFFF" />
              <GradientStop Offset="1"   Color="#00000000" />
          </LinearGradientBrush>
      </Rectangle.Fill>
  </Rectangle>
  ```
- [ ] Verify background still feels dark and cinematic, not washed out

---

### Step 4.2 — Improve button visual depth

**Current:** Buttons use near-transparent backgrounds (`#05FFFFFF`, `#06FFFFFF`).

**Target:** Subtle but readable glass surface.

- [ ] Update `StartGlassBg` color from `#05FFFFFF` → `#09FFFFFF`
- [ ] Update `CardGlassBg` from `#06FFFFFF` → `#0AFFFFFF`
- [ ] Update `StartAccentBorder` from `#33C9A96E` → `#44C9A96E` (slightly more visible)
- [ ] On `:pointerover`, increase `BtnPrimary` background from `#0EC9A96E` → `#18C9A96E`
- [ ] Add a subtle `BoxShadow` to the primary button for lift effect:
  ```xml
  <!-- Add to BtnPrimary style -->
  <Setter Property="BoxShadow" Value="0 2 12 0 #20C9A96E" />
  ```

---

### Step 4.3 — Section spacing between Brand, Buttons, Recent

The three sections currently butt up against each other with no breathing room controlled by the outer layout.

**Target:** Add `Margin` to each section's Grid row for measured white-space:

- [ ] `BrandPanel`: `Margin="{a:Breakpoint XS=0,20,0,12, M=0,32,0,16, XL=0,48,0,20}"`
- [ ] `ButtonPanel`: `Margin="{a:Breakpoint XS=0,0,0,12, M=0,0,0,16, XL=0,0,0,20}"`
- [ ] `RecentSection`: `Margin="{a:Breakpoint XS=0,4,0,0, M=0,8,0,0, XL=0,12,0,0}"`

---

### Step 4.4 — Wordmark letter-spacing refinement

**Current:** `LetterSpacing="8"` — feels overly stretched at small sizes.

**Target:** Scale with font size:

- [ ] Apply `LetterSpacing="{a:Breakpoint XS=4, S=5, M=6, L=7, XL=8, XXL=8}"` on `WordmarkText`

---

### Step 4.5 — Tagline color refinement

**Current:** `Foreground="{StaticResource AppTextOnDarkTertiary}"` → `#80FFFFFF` (50% white)

**Target:** Slightly more visible at small sizes, consistent at large:
- [ ] Change to `{StaticResource AppTextOnDarkHint}` (`#99FFFFFF`, 60%) — better legibility

---

### Step 4.6 — Button entrance animation — add scale-up

The current entrance only translates Y. Apple-style: elements scale in from 95% + fade.

- [ ] Update `StackPanel#ButtonPanel` animation:
  ```xml
  <Animation Duration="0:0:0.6" Delay="0:0:0.2" FillMode="Forward" Easing="CubicEaseOut">
      <KeyFrame Cue="0%">
          <Setter Property="Opacity" Value="0" />
          <Setter Property="TranslateTransform.Y" Value="12" />
          <Setter Property="ScaleTransform.ScaleX" Value="0.97" />
          <Setter Property="ScaleTransform.ScaleY" Value="0.97" />
      </KeyFrame>
      <KeyFrame Cue="100%">
          <Setter Property="Opacity" Value="1" />
          <Setter Property="TranslateTransform.Y" Value="0" />
          <Setter Property="ScaleTransform.ScaleX" Value="1" />
          <Setter Property="ScaleTransform.ScaleY" Value="1" />
      </KeyFrame>
  </Animation>
  ```
- [ ] Add `TransformGroup` with `TranslateTransform` + `ScaleTransform` to `ButtonPanel` `RenderTransform`

---

### Step 4.7 — Close button hover: Red highlight

**Current:** Close button has no hover state differentiation.

- [ ] Add style:
  ```xml
  <Style Selector="Button.WindowCtrl.close:pointerover">
      <Setter Property="Background" Value="{StaticResource WindowCloseButtonHoverBackground}" />
  </Style>
  ```
  (The `WindowCloseButtonHoverBackground` brush (`#E81123`) already exists in `Colors.axaml`)
- [ ] Add `Transitions` to `WindowCtrl` style for smooth hover fade

---

### Step 4.8 — Window controls: Matching minimize/maximize hover

- [ ] Add style:
  ```xml
  <Style Selector="Button.WindowCtrl:pointerover">
      <Setter Property="Background" Value="{StaticResource WindowButtonHoverBackground}" />
  </Style>
  ```
  (The `WindowButtonHoverBackground` brush (`#2BFFFFFF`) already exists)

---

---

# PHASE 5 — Code-Behind Hardening

---

### Step 5.1 — Verify Breakpoints propagate to UserControl

The AVVI94 `{a:Breakpoint}` extension requires `a:Breakpoints.IsBreakpointProvider="True"` on an ancestor. `MainWindow` sets this. Verify the chain:

- [ ] Add a single test breakpoint on a visible element (e.g., `WordmarkText` FontSize)
- [ ] Run app and resize window
- [ ] Confirm: FontSize changes at defined breakpoints in real-time
- [ ] If it does NOT change: Add `a:Breakpoints.IsBreakpointProvider="True"` to the `StartPage` UserControl root and retest

---

### Step 5.2 — Verify ItemsRepeater virtualization is working

- [ ] Add 10+ recent file items to the list
- [ ] Resize window small
- [ ] In debug, confirm `ItemsRepeater` is not materializing all items at once
- [ ] If not virtualizing: Ensure `ScrollViewer` has `VerticalScrollBarVisibility="Disabled"` and `HorizontalScrollBarVisibility="Hidden"` with a bounded height parent

---

### Step 5.3 — Review Task usage in OnNavigatedFrom

**Current issue:** `Task.Run` + `Task.Delay` + `Dispatcher.UIThread.OnUiThreadAsync` for hide animation.

```csharp
// Current — fire-and-forget, could leak if page is shown again quickly
_ = Task.Run(async () =>
{
    await Task.Delay(350);
    await Dispatcher.UIThread.OnUiThreadAsync(() => IsVisible = false);
});
```

- [ ] Add a cancellation token to cancel pending hide if OnNavigatedTo fires before 350ms
- [ ] Or: Use `Dispatcher.UIThread.InvokeAsync` with a timeout
- [ ] Verify no visual flash when switching back to start page immediately

---

### Step 5.4 — Entrance animation re-trigger on navigation return

**Current:** `OnNavigatedTo` sets `Opacity = 0` then `1` — but the CSS animations on `Border#StartPageRoot` only fire once (they use `FillMode="Forward"`, which means the animation runs once and holds its final value). Re-navigation doesn't replay them.

- [ ] Test: Navigate to start page twice (open file, close player)
- [ ] Confirm: Entrance animations replay on second navigation
- [ ] If they don't: Reset styles via code-behind using `Classes.Remove` / `Classes.Add` trick, or use `Animation` replay via `Animate` method

---

---

# PHASE 6 — Build, Test & Sign-Off

---

### Step 6.1 — Build verification

- [ ] Run: `dotnet build src/App/App.csproj`
- [ ] Zero errors
- [ ] Zero warnings on new code (existing warnings are acceptable if pre-existing)

---

### Step 6.2 — Visual smoke test at all breakpoints

Launch app, resize window, verify at each breakpoint:

| Size | Width × Height | Logo | Wordmark | Buttons | Recent Section |
|---|---|---|---|---|---|
| XS | 400 × 420 | [ ] 48px | [ ] 22sp | [ ] 150px wide | [ ] scrollable |
| S  | 495 × 500 | [ ] 56px | [ ] 26sp | [ ] 170px wide | [ ] scrollable |
| M  | 600 × 550 | [ ] 64px | [ ] 30sp | [ ] 190px wide | [ ] scrollable |
| L  | 800 × 600 | [ ] 72px | [ ] 34sp | [ ] 200px wide | [ ] scrollable |
| XL | 1100 × 700 | [ ] 80px | [ ] 38sp | [ ] 210px wide | [ ] scrollable |
| XXL | 1440 × 900 | [ ] 80px | [ ] 40sp | [ ] 220px wide | [ ] scrollable |

---

### Step 6.3 — Interaction tests

- [ ] Hover `Open Media` → lift + glow border appears
- [ ] Hover `Open Folder` → glass border brightens
- [ ] Press button → drops back to resting position
- [ ] Hover recent card → card lifts 4px with border highlight
- [ ] Click recent card → file opens
- [ ] Drag file onto window → file opens
- [ ] `Ctrl+O` keyboard shortcut → file picker opens
- [ ] Minimize, Maximize, Close window buttons work
- [ ] Close button turns red on hover
- [ ] Entrance animation plays on first launch
- [ ] Entrance animation replays on returning from player

---

### Step 6.4 — Edge cases

- [ ] Empty recent files: "No recent media" text is visible and centered
- [ ] 1 recent file: Card renders correctly without stretch artifacts
- [ ] 20+ recent files: Horizontal scroll works, no vertical overflow
- [ ] Very long filename: Title text truncates with ellipsis (`TextTrimming="CharacterEllipsis"`)
- [ ] Window maximized: Layout fills correctly, not just centered in viewport
- [ ] Window restored to normal from maximized: Maximize icon updates correctly

---

### Step 6.5 — Final sign-off

- [ ] Screenshots taken at 3 key sizes and reviewed
- [ ] No hardcoded pixel sizes remain on font/spacing (except cards — intentional)
- [ ] All `sys:Double` sizing tokens removed from `UserControl.Resources`
- [ ] No `labs:FlexPanel` used for vertical section layout (only for card content if needed)
- [ ] Code-behind hardening steps resolved
- [ ] Plan marked complete ✅

---

## Notes & Decisions Log

> Use this section to record any decisions made during implementation.

| Date | Decision | Rationale |
|---|---|---|
| | | |
| | | |

---

*Plan created: 2026-07-13. File: `md/startpage-ui-overhaul.md`*
