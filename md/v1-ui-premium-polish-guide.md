# Simba V1 UI Premium Polish Guide

Date: `2026-07-13`  
Project: `x:\Development\Cine_CSharp_DotNet`  
Scope: Finish the current V1 design without adding new product features  
Target: Move the current UI from roughly `30%` polish to `100%` release-ready visual quality

## Purpose

This guide is for the **current V1 product**, not the V2 redesign.

We are **not** adding new screens, new flows, or new product features here.  
We are fixing the existing visual system so the app feels:

- premium
- intentional
- consistent
- on-theme
- ready to show to management

Allowed:

- visual refactor
- layout cleanup
- theme/token cleanup
- spacing/typography cleanup
- control restyling
- fixing broken existing functionality if it blocks the current UI

Not allowed:

- new product modules
- new navigation structure
- new feature additions
- V2 information architecture work

## Status Legend

- `[ ]` done
- `[~]` partial / needs cleanup
- `[ ]` pending
- `[!]` high-importance issue

---

## Current V1 Reality Check

The app already has a decent foundation, but the visual language is split across multiple directions:

1. `StartPage` uses a **bronze premium** language with local resources.
2. playback chrome uses a **dark glass** language.
3. dialogs/panels use a **generic dark popover** language.
4. some controls still use **plain white Fluent-like slider/thumb values**.
5. preferences window feels like an older internal tool, not part of the same product.

That is why the app feels "working" but not yet "premium".

---

## Main Problems To Fix First

### 1. Accent system is fragmented

Files:

- `src/App/Views/Resources/Colors.axaml`
- `src/App/Views/Pages/StartPage.axaml`
- `src/App/Views/Resources/AppColors.cs`

Observed issues:

- `Colors.axaml` contains a legacy `Accent` blue (`#5B9BD5`) and also a newer `AppAccent` blue (`#6CB4FF`).
- `StartPage.axaml` introduces a separate bronze palette locally.
- code-created UI in `TrackFlyoutBuilder.cs` uses `AppColors.Accent`, while some XAML still references `Accent`.
- selected states in `App.axaml` still use `AppDragAccentDim` (drag accent) as generic selection background.
- **No bronze/gold accent family exists in the shared theme.**

Impact:

- the app has no single premium accent identity
- seek bars, selected states, toggles, and buttons do not feel related
- start screen branding and playback theme feel like different products

V1 rule:

- pick **one V1 accent family**
- everything else derives from that
- no component-local hero palette unless it is explicitly a branded exception

Recommendation for V1:

- keep the playback/product accent in one controlled family
- let `StartPage` use the same family, or at most a restrained branded variant built from shared tokens

---

### 2. The playback controls are functional but visually flat

Files:

- `src/App/Views/Components/Chrome/ControlsBox.axaml`
- `src/App/Views/Components/Media/SeekBar.axaml`
- `src/App/Views/Resources/App.axaml`
- `src/App/Views/Resources/Colors.axaml`

Observed issues:

- `ControlsBox` structure is solid, but icon groups still feel mechanically assembled instead of intentionally composed.
- `SeekBar` uses white/off-white values that feel disconnected from the rest of the theme.
- `Slider.volume-slider` and `Slider.compact` still hardcode mostly white track/thumb behavior in `App.axaml`.
- separators and button states are serviceable, but not rich enough for a premium media product.

Impact:

- most-used UI surface does not sell the product
- manager feedback will keep landing here until it looks custom and branded

---

### 3. Flyouts and bottom panels are too generic

Files:

- `src/App/Views/Components/Panels/VolumePanel.axaml`
- `src/App/Views/Components/Panels/PlaylistPanel.axaml`
- `src/App/Views/Components/Panels/EqualizerPanel.axaml`
- `src/App/Views/Components/Panels/SubtitlePanel.axaml.cs`
- `src/App/Views/Components/Panels/AudioTrackPanel.axaml.cs`
- `src/App/Views/Components/Media/TrackFlyoutBuilder.cs`

Observed issues:

- panel shells are mostly just dark rectangles with border
- header hierarchy is weak
- list item states do not feel premium
- delay controls created in code are usable but visually plain
- volume panel reads more like a utility popup than a product-quality surface

Impact:

- secondary surfaces feel boring compared with the ambition of the app
- polish drops sharply as soon as the user opens overlays

---

### 4. Preferences window is the weakest major UI surface

Files:

- `src/App/Views/Dialogs/PreferencesWindow.axaml`
- `src/App/Views/Dialogs/PreferencesWindow.axaml.cs`
- `src/App/Views/Components/Panels/EqualizerPanel.axaml`

Observed issues:

- layout is basic `200,*` split with old sidebar feel
- header and navigation have no premium framing
- cards inside the content pane are repetitive and visually dead
- some text/input styling is manually forced per control instead of coming from one standard
- about page looks placeholder-level

Impact:

- this window lowers perceived product maturity immediately
- it feels disconnected from playback chrome and start page

---

### 5. Token discipline is incomplete

Files:

- `src/App/Views/Resources/App.axaml`
- `src/App/Views/Resources/Colors.axaml`
- `src/App/Views/Resources/Spacing.axaml`
- `src/App/Views/Resources/Sizes.axaml`
- `src/App/Views/Resources/Typography.axaml`

Observed issues:

- there are still repeated inline values and special-case styling
- control classes exist, but not all important surfaces are routed through them
- typography token file still contains placeholder comments and fallback mindset
- selection/hover/focus states are not fully harmonized across list, menu, combo, numeric, and panel controls

Impact:

- every new polish pass becomes harder than it should be
- visual drift returns quickly

---

## V1 Quality Bar

V1 is complete only when all of these are true:

- every visible surface looks like it belongs to the same product
- playback chrome is the visual benchmark for the app
- panels, menus, sliders, and preferences window all match that benchmark
- accent use is intentional, limited, and consistent
- no cheap-looking gray-box or default-tool appearance remains
- no new features were added during this pass

---

## V1 Execution Plan: 30% -> 100%

## Phase 1: 30% -> 45%  
## Theme Foundation Lock

Goal: establish one V1 visual language before touching individual screens.

### Deliverables

- `[ ]` choose and lock one V1 accent family — **Dual blue accents coexist (#5B9BD5 + #6CB4FF), no bronze/gold in shared theme**
- `[ ]` define canonical surface stack — **Missing AppSurfaceRaised, AppSurfaceOverlay, AppSurfaceGlass**
- `[ ]` define canonical text hierarchy — **TextPrimary/Secondary/Tertiary/Disabled exist — verify consistency**
- `[ ]` define canonical interactive state set — **Hover states exist, but selected states still use AppDragAccentDim**
- `[ ]` remove conflicting legacy token usage from new polish work — **Drag accent still used for selected states**

### File Targets

- `src/App/Views/Resources/Colors.axaml`
- `src/App/Views/Resources/App.axaml`
- `src/App/Views/Resources/AppColors.cs`
- `src/App/Views/Resources/Typography.axaml`

### Required Changes

1. Keep only one real accent direction for V1 usage:
   - primary accent
   - accent hover
   - accent pressed
   - accent soft fill
   - accent border

2. Create a real surface ladder:
   - `AppBackground`
   - `AppSurface`
   - `AppSurfaceRaised`
   - `AppSurfaceOverlay`
   - `AppSurfaceGlass`

3. Create one canonical border ladder:
   - subtle
   - standard
   - active

4. Create one canonical text ladder:
   - primary
   - secondary
   - tertiary
   - disabled

5. Stop using drag-state color as generic selected-state color.

### Code Snippet Pattern

```xml
<!-- Colors.axaml -->
<SolidColorBrush x:Key="AppAccent" Color="#FFB88C4A" />
<SolidColorBrush x:Key="AppAccentHover" Color="#FFC79B5B" />
<SolidColorBrush x:Key="AppAccentPressed" Color="#FF9E7335" />
<SolidColorBrush x:Key="AppAccentSoft" Color="#26B88C4A" />
<SolidColorBrush x:Key="AppSurfaceRaised" Color="#FF17181D" />
<SolidColorBrush x:Key="AppSurfaceOverlay" Color="#F01A1B20" />
<SolidColorBrush x:Key="AppBorderSubtle" Color="#14FFFFFF" />
<SolidColorBrush x:Key="AppBorderStrong" Color="#30FFFFFF" />
```

### Visual Rules

- accent is for action, focus, active selection, and key emphasis
- accent is not for random decoration
- white should mostly be text/highlight, not the whole personality of controls
- bronze/gold can work, but only if used with discipline

### Exit Criteria

- `[ ]` one accent direction is documented and applied to all new polish work — **Not applied: both Accent and AppAccent used**
- `[ ]` selected states no longer mix drag blue, legacy blue, and bronze independently — **Still using AppDragAccentDim for selected**
- `[ ]` typography and text opacities feel coherent across playback and settings

---

## Phase 2: 45% -> 60%  
## Playback Chrome Premium Pass

Goal: make the player chrome feel like the hero surface of V1.

### Deliverables

- `[ ]` controls row feels composed and balanced — **Good structure with glass gradient, circular buttons, transitions; grouping could be tighter**
- `[ ]` seekbar feels custom, not default — **Custom track/fill/thumb with shadows + chapter marks, but uses white/off-white instead of accent**
- `[ ]` time labels, separators, and button states feel premium — **Glass gradient chrome is good, but plain white sliders drag it down**
- `[ ]` fullscreen/header/control styling is aligned — **ControlsBox + HeaderBar share glass language, but slider identity is disconnected**

### File Targets

- `src/App/Views/Components/Chrome/ControlsBox.axaml`
- `src/App/Views/Components/Media/SeekBar.axaml`
- `src/App/Views/Components/Chrome/HeaderBar.axaml`
- `src/App/Views/Resources/App.axaml`
- `src/App/Views/Resources/Colors.axaml`

### Required Changes

1. Upgrade seekbar materials:
   - richer inactive track
   - accent-led active fill
   - thumb with subtle edge/highlight
   - preview popover styled like product chrome

2. Tighten transport grouping:
   - equal visual rhythm
   - clearer group separation
   - slightly more intentional button weight hierarchy

3. Make header and controls feel like one family:
   - same glass depth
   - same border language
   - same icon opacity logic

4. Remove plain white slider identity from playback surfaces.

### Code Snippet Pattern

```xml
<!-- SeekBar.axaml -->
<Border x:Name="SeekTrack"
        Background="{StaticResource AppSurfaceRaised}"
        Opacity="0.9"
        CornerRadius="{StaticResource radius-xs}"
        Height="4" />

<Border x:Name="SeekFill"
        Background="{StaticResource AppAccent}"
        CornerRadius="{StaticResource radius-xs}"
        Height="4">
    <Border.Effect>
        <DropShadowEffect BlurRadius="8" OffsetY="0" Color="#33B88C4A" />
    </Border.Effect>
</Border>

<Border x:Name="SeekThumb"
        Width="14"
        Height="14"
        Background="{StaticResource AppSurfaceOverlay}"
        BorderBrush="{StaticResource AppAccent}"
        BorderThickness="1.5"
        CornerRadius="7" />
```

### QA Checks

- `[ ]` seek fill does not look chalky or disconnected
- `[ ]` thumb stays visible over bright and dark video
- `[ ]` controls remain smooth during hover/resize — **Transitions and hover effects exist, verify smoothness**
- `[ ]` transport row still feels centered and balanced at 800x600

---

## Phase 3: 60% -> 72%  
## Flyouts And Panels Unification

Goal: make every overlay feel intentionally designed, not simply placed on screen.

### Deliverables

- `[ ]` volume panel upgraded — **Functional with PopoverBackground shell, generic slider, basic preset buttons. Not premium.**
- `[ ]` subtitle/audio panels upgraded — **N/A: panels are empty containers (content code-generated). TrackFlyoutBuilder handles UI.**
- `[ ]` playlist panel upgraded — **Well-structured with full features (search, sort, clear, save, drag, remove). Uses PanelContainer. Verify polish.**
- `[ ]` equalizer panel visually aligned — **Good preset buttons with AppAccent selected state. Delay controls with slider + numeric. Verify structure.**
- `[ ]` track flyout builder outputs premium rows and actions — **Well-structured code with search, delay, selection dot, hover states. Uses AppColors.Accent. Verify code-generated UI polish.**

### File Targets

- `src/App/Views/Components/Panels/VolumePanel.axaml`
- `src/App/Views/Components/Panels/PlaylistPanel.axaml`
- `src/App/Views/Components/Panels/EqualizerPanel.axaml`
- `src/App/Views/Components/Media/TrackFlyoutBuilder.cs`
- `src/App/Views/Components/Panels/SubtitlePanel.axaml.cs`
- `src/App/Views/Components/Panels/AudioTrackPanel.axaml.cs`
- `src/App/Views/Components/Containers/PanelContainer.cs`

### Required Changes

1. Standardize panel shell:
   - same corner radius
   - same padding logic
   - same surface depth
   - same header/title/action layout

2. Standardize panel section anatomy:
   - title
   - optional subtitle/meta
   - section divider
   - content
   - footer/action row

3. Upgrade list rows:
   - better selected state
   - better hover state
   - stronger typography hierarchy
   - more breathing room

4. Rework code-generated delay controls to match XAML-level styling.

### Code Snippet Pattern

```csharp
var button = new Button
{
    Content = grid,
    Classes = { "flyout-item-row" },
    Padding = new Thickness(12, 10),
    MinHeight = 40,
    Background = AppColors.Transparent,
    BorderThickness = new Thickness(0)
};
```

```xml
<!-- App.axaml -->
<Style Selector="Button.flyout-item-row">
    <Setter Property="CornerRadius" Value="{StaticResource radius-sm}" />
    <Setter Property="HorizontalContentAlignment" Value="Stretch" />
</Style>
<Style Selector="Button.flyout-item-row:pointerover">
    <Setter Property="Background" Value="{StaticResource AppHoverSubtle}" />
</Style>
<Style Selector="Button.flyout-item-row:selected">
    <Setter Property="Background" Value="{StaticResource AppAccentSoft}" />
</Style>
```

### QA Checks

- `[ ]` opening any panel no longer causes a "cheap popup" feeling
- `[ ]` list rows in playlist and track selection feel from same family
- `[ ]` equalizer presets, delay controls, and panel headers no longer fight each other visually

---

## Phase 4: 72% -> 84%  
## Preferences Window Redesign Within V1 Scope

Goal: make settings feel like a finished product window while keeping the same functionality.

### Deliverables

- `[ ]` sidebar redesigned — **Basic ListBoxItem.sidebar-item using AppHoverSubtle/AppHover for selected/hover. No premium framing.**
- `[ ]` page header redesigned — **Basic "Preferences" text label. No premium header area.**
- `[ ]` content cards upgraded — **Uses PanelContainer with PopoverBackground and proper layout. No settings-card class exists.**
- `[ ]` forms made consistent — **ToggleSwitch + TextBlock pattern consistent. But inline styling instead of shared tokens.**
- `[ ]` about page no longer looks placeholder-grade — **OK structure with logo/version/info/license. Not premium but not placeholder.**

### File Targets

- `src/App/Views/Dialogs/PreferencesWindow.axaml`
- `src/App/Views/Dialogs/PreferencesWindow.axaml.cs`
- `src/App/Views/Components/Panels/EqualizerPanel.axaml`
- `src/App/Views/Resources/App.axaml`

### Required Changes

1. Replace plain left column feel with a product-quality settings shell:
   - stronger title area
   - better navigation item affordance
   - clearer selected state
   - better content spacing

2. Convert settings sections into richer cards:
   - title
   - descriptive copy
   - aligned action/control
   - proper internal dividers

3. Make toggles, textboxes, numeric inputs, combo boxes all share one form language.

4. Improve the about section so it looks like a product panel, not a fallback placeholder.

### Code Snippet Pattern

```xml
<Border Classes="settings-card">
    <Grid ColumnDefinitions="*,Auto" RowDefinitions="Auto,Auto">
        <TextBlock Text="Hardware Acceleration"
                   Classes="md3-subtitle1"
                   Foreground="{StaticResource TextPrimary}" />
        <TextBlock Grid.Row="1"
                   Text="Use GPU decoding and rendering for smoother playback."
                   Classes="md3-caption"
                   Foreground="{StaticResource TextSecondary}" />
        <ToggleSwitch Grid.Column="1"
                      Grid.RowSpan="2"
                      VerticalAlignment="Center" />
    </Grid>
</Border>
```

```xml
<!-- App.axaml -->
<Style Selector="Border.settings-card">
    <Setter Property="Background" Value="{StaticResource AppSurfaceOverlay}" />
    <Setter Property="BorderBrush" Value="{StaticResource AppBorderSubtle}" />
    <Setter Property="BorderThickness" Value="1" />
    <Setter Property="CornerRadius" Value="{StaticResource radius-md}" />
    <Setter Property="Padding" Value="{StaticResource space-4}" />
</Style>
```

### QA Checks

- `[ ]` settings window looks like part of the same product as playback
- `[ ]` no section feels like generic admin UI
- `[ ]` equalizer embedded inside preferences still feels integrated
- `[ ]` keyboard navigation still works

---

## Phase 5: 84% -> 92%  
## Cross-Surface Consistency Audit

Goal: remove the last obvious mismatches.

### Deliverables

- `[ ]` button families normalized — **.primary, .ghost, .flat exist. No .secondary, .destructive in shared styles.**
- `[ ]` list selection normalized — **ListBoxItem & ComboBoxItem selected state uses AppDragAccentDim instead of accent**
- `[ ]` tooltip/menu/flyout hierarchy normalized — **Patterns exist but not formalized**
- `[ ]` text styles normalized — **MD3 type scale comprehensive — verify consistency**
- `[ ]` inline visual values reduced — **Sliders still use raw White, panels use local popover resources**

### File Targets

- `src/App/Views/Resources/App.axaml`
- `src/App/Views/Resources/Colors.axaml`
- `src/App/Views/Pages/StartPage.axaml`
- `src/App/Views/Dialogs/*.axaml`
- `src/App/Views/Components/**/*.axaml`

### Required Changes

1. Normalize these control families:
   - `Button.primary`
   - `Button.secondary`
   - `Button.ghost`
   - `Button.flat`
   - panel action buttons
   - destructive actions

2. Normalize these selection states:
   - `ListBoxItem`
   - `ComboBoxItem`
   - menu selection
   - active track rows
   - playlist current item

3. Review `StartPage` local resources and reduce anything that should really live in shared theme files.

4. Review typography:
   - title weights
   - caption opacity
   - monospaced time labels
   - panel header hierarchy

### Specific V1 Issues To Eliminate

- `[ ]` legacy `Accent` and modern `AppAccent` both actively styling live UI — **Confirmed: both used**
- `[ ]` drag accent being reused as generic selection background — **Confirmed: AppDragAccentDim in App.axaml**
- `[ ]` plain white slider/thumb identity dominating premium surfaces — **Confirmed: volume-slider & compact slider resources**
- `[ ]` local page palettes overriding shared theme without reason — **Confirmed: StartPage bronze palette, but arguably branded exception**
- `[ ]` tiny flat icon buttons feeling too light for premium overlay panels

---

## Phase 6: 92% -> 100%  
## Final Polish, Review, And Freeze

Goal: lock V1 visual quality and stop churn.

### Deliverables

- `[ ]` full-screen review
- `[ ]` resize review
- `[ ]` dark-scene / bright-scene video review
- `[ ]` hover/focus/pressed review
- `[ ]` screenshot review for manager/demo use

### Review Checklist

- `[ ]` start page feels branded and premium
- `[ ]` player chrome feels like the hero UI
- `[ ]` panels feel custom-designed
- `[ ]` preferences window feels product-ready
- `[ ]` no single window looks like it belongs to another app
- `[ ]` color usage is restrained and deliberate
- `[ ]` accent is used to guide, not decorate randomly

### Test Matrix

Test these exact situations:

1. `800x600`
2. `1024x768`
3. maximized window
4. focus lost / focus regained
5. video paused on dark frame
6. video paused on bright frame
7. open volume panel
8. open subtitle panel
9. open audio track panel
10. open playlist panel
11. open equalizer panel
12. open preferences window and switch all tabs

### Failure Handling

If something still feels wrong, classify it correctly:

- **theme issue** -> fix token/style
- **layout issue** -> fix spacing/sizing/alignment
- **surface issue** -> fix panel/container treatment
- **state issue** -> fix hover/focus/selected/pressed behavior
- **content issue** -> fix label hierarchy/copy density

Do not solve a theme problem with one-off inline styling.

---

## Exact V1 Fix Priorities

Do these in this order:

1. `[ ]` unify accent and surface tokens — **Phase 1: critical blocker**
2. `[ ]` restyle seekbar and playback sliders — **Phase 2: remove plain white slider identity**
3. `[ ]` improve controls/header visual hierarchy — **Phase 2: existing good, minor tightening**
4. `[ ]` upgrade overlay panels and flyout rows — **Phase 3: establish shared panel class**
5. `[ ]` redesign preferences window shell — **Phase 4: biggest single-surface improvement**
6. `[ ]` normalize lists, buttons, text, and selection states — **Phase 5: fix drag accent selection**
7. `[ ]` run final review and freeze V1 visuals — **Phase 6: depends on all above**

---

## Coding Standards For This V1 Pass

- do not add new features
- prefer shared tokens over inline visual values
- prefer shared classes over per-control styling
- if a visual rule is reused twice, move it into `App.axaml` or token files
- no random one-off accent usage
- no mixing old `Accent` and new `AppAccent` usage in the same finished surface
- preserve current behavior unless fixing an existing broken behavior
- do not destabilize resize smoothness while polishing visuals

---

## Anti-Patterns To Avoid

- adding more local palettes like `StartPage` unless absolutely necessary
- solving premium look with heavier glow only
- making everything brighter instead of improving contrast hierarchy
- using drag/drop color for normal selected state
- introducing feature work while doing polish work
- leaving code-generated flyout UI visually behind XAML-authored UI

---

## Definition Of Done For V1 UI

V1 polish is complete when:

- `[ ]` manager can review the current product without visual embarrassment points
- `[ ]` preferences window no longer looks weak beside playback UI
- `[ ]` seekbar and controls feel branded
- `[ ]` every panel/flyout matches the theme
- `[ ]` no major visual mismatch remains between start, player, overlays, and settings
- `[ ]` this was achieved **without expanding scope into V2 feature work**

---

## Suggested First Implementation Batch

When we start execution, the best first batch is:

1. `Colors.axaml`
2. `App.axaml`
3. `SeekBar.axaml`
4. `ControlsBox.axaml`
5. `HeaderBar.axaml`

Reason:

- this gives the fastest visible jump in quality
- it sets the visual standard for every later surface
- it prevents us from polishing preferences and panels against the wrong theme

---

## Code Analysis Summary

### What's Already Good (existing foundation — don't break)

| Item | File(s) | Notes |
|------|---------|-------|
| Typography type scale | `Typography.axaml` | Full MD3 scale: caption through headline2 classes |
| Spacing token system | `Spacing.axaml` | Uniform, horizontal, vertical, asymmetric, spacing, icon size tokens |
| ControlsBox glass chrome | `ControlsBox.axaml` | Glass gradient background, circular buttons, hover/scale transitions |
| HeaderBar glass chrome | `HeaderBar.axaml` | Glass gradient, proper borders, transitions |
| Text hierarchy tokens | `Colors.axaml` | TextPrimary/Secondary/Tertiary/Disabled defined |
| Playlist panel structure | `PlaylistPanel.axaml` | Full feature set with PanelContainer shell |
| Equalizer panel structure | `EqualizerPanel.axaml` | Presets with accent selected state, delay controls |
| Keyboard navigation | Various | Tab navigation, arrow key support in flyouts |
| Animations | `StartPage.axaml` | Entrance fades, slide-ups, glow orb float |

### What Needs Work

| Item | File(s) | What To Fix |
|------|---------|-------------|
| Accent tokens | `Colors.axaml`, `AppColors.cs` | Dual blue accents — pick one direction (bronze suggested) |
| Selection states | `App.axaml` | Replace `AppDragAccentDim` with proper accent |
| Surface ladder | `Colors.axaml` | Add `AppSurfaceRaised`, `AppSurfaceOverlay`, `AppSurfaceGlass` |
| Border ladder | `Colors.axaml` | Add `AppBorderSubtle`, `AppBorderStrong` |
| SeekBar accent | `SeekBar.axaml` | Replace white/off-white fill with accent-led |
| Sliders | `App.axaml` | Remove plain white from volume-slider and compact |
| Volume panel | `VolumePanel.axaml` | Upgrade from basic popup to premium surface |
| Preferences window | `PreferencesWindow.axaml` | Redesign sidebar, header, cards, about page |
| StartPage palette | `StartPage.axaml` | Connect local bronze to shared theme or rationalize isolation |
| Button families | `App.axaml` | Add missing `.secondary`, `.destructive` shared styles |
