# Avalonia Visual API Reference
## Brushes · Gradients · Effects · Transitions · Animations · Fonts

> Extracted from official Avalonia documentation (v12, current).
> All examples are copy-paste ready for use in our StartPage AXAML.
> Marked with `[✅ USED]` where already applied, `[🎯 APPLY]` where we should add to StartPage.

**Official source pages:**
- https://docs.avaloniaui.net/docs/graphics-animation/brushes
- https://docs.avaloniaui.net/docs/graphics-animation/gradients
- https://docs.avaloniaui.net/docs/graphics-animation/effects
- https://docs.avaloniaui.net/docs/graphics-animation/keyframe-animations
- https://docs.avaloniaui.net/docs/graphics-animation/control-transitions
- https://docs.avaloniaui.net/docs/how-to/custom-font-how-to

---

## Table of Contents

1. [SolidColorBrush — All Color Formats](#1-solidcolorbrush--all-color-formats)
2. [LinearGradientBrush](#2-lineargradientbrush)
3. [RadialGradientBrush](#3-radialgradientbrush)
4. [ConicGradientBrush](#4-conicgradientbrush)
5. [VisualBrush](#5-visualbrush)
6. [ExperimentalAcrylicBrush (Frosted Glass)](#6-experimentalacrylicbrush-frosted-glass)
7. [ImmutableSolidColorBrush — Performance](#7-immutablesolidcolorbrush--performance)
8. [BoxShadow (Border)](#8-boxshadow-border)
9. [BlurEffect / DropShadowEffect](#9-blureffect--dropshadoweffect)
10. [OpacityMask](#10-opacitymask)
11. [Control Transitions (BrushTransition, DoubleTransition, etc.)](#11-control-transitions)
12. [Keyframe Animations](#12-keyframe-animations)
13. [Custom Fonts (Embedding Google Fonts)](#13-custom-fonts-embedding-google-fonts)
14. [SpreadMethod — Gradient Fill Modes](#14-spreadmethod--gradient-fill-modes)
15. [Applied Patterns for StartPage](#15-applied-patterns-for-startpage)

---

---

## 1. SolidColorBrush — All Color Formats

**Source:** https://docs.avaloniaui.net/docs/graphics-animation/brushes

### Inline shorthand (implicit brush)
```xml
<!-- Named color -->
<Border Background="SteelBlue" />

<!-- Hex #RRGGBB -->
<Border Background="#4682B4" />

<!-- Hex #AARRGGBB (with alpha!) — most common in our dark UI -->
<Border Background="#80C9A96E" />   <!-- 50% opacity bronze -->
<Border Background="#0AFFFFFF" />   <!-- 4% white glass -->

<!-- Short hex #RGB -->
<Border Background="#F00" />

<!-- CSS rgb() function -->
<Border Background="rgb(70, 130, 180)" />

<!-- CSS rgba() function -->
<Border Background="rgba(70, 130, 180, 0.8)" />

<!-- CSS hsl() function — GREAT for tuning saturation without hex math -->
<Border Background="hsl(207, 44%, 49%)" />

<!-- CSS hsla() function -->
<Border Background="hsla(207, 44%, 49%, 0.8)" />

<!-- HSV / HSVA -->
<Border Background="hsv(207, 61%, 71%)" />
<Border Background="hsva(207, 61%, 71%, 0.8)" />
```

### Explicit form (with Opacity property)
```xml
<Border>
    <Border.Background>
        <SolidColorBrush Color="#4682B4" Opacity="0.8" />
    </Border.Background>
</Border>
```
> ⚠️ **Prefer `#AARRGGBB` alpha hex over `Opacity` on the brush.** `Opacity` on a brush affects the entire element's rendering. Using alpha in the color hex is more performant and composites correctly.

### Creating in C# code
```csharp
var brush  = new SolidColorBrush(Colors.SteelBlue);
var brush2 = new SolidColorBrush(Color.Parse("#4682B4"));
var brush3 = Brush.Parse("rgb(70, 130, 180)");
var brush4 = Brush.Parse("hsl(207, 44%, 49%)");
myBorder.Background = brush;
```

---

## 2. LinearGradientBrush

**Source:** https://docs.avaloniaui.net/docs/graphics-animation/brushes  /  https://docs.avaloniaui.net/docs/graphics-animation/gradients

### Basic usage (relative percentage coordinates)
```xml
<Border Height="80" CornerRadius="8">
    <Border.Background>
        <LinearGradientBrush StartPoint="0%,50%" EndPoint="100%,50%">
            <GradientStop Color="#6366F1" Offset="0" />
            <GradientStop Color="#EC4899" Offset="1" />
        </LinearGradientBrush>
    </Border.Background>
</Border>
```

### Key coordinate patterns
```xml
<!-- Left → Right (horizontal) -->
<LinearGradientBrush StartPoint="0%,50%" EndPoint="100%,50%">

<!-- Top → Bottom (vertical) -->
<LinearGradientBrush StartPoint="50%,0%" EndPoint="50%,100%">

<!-- Diagonal top-left → bottom-right (like our background) -->
<LinearGradientBrush StartPoint="0,0" EndPoint="1,1">

<!-- Diagonal top-right → bottom-left -->
<LinearGradientBrush StartPoint="1,0" EndPoint="0,1">

<!-- NOTE: "0,0" to "1,1" = normalized (0.0–1.0) coordinates
           "0%,0%" to "100%,100%" = percentage coordinates
           Both are equivalent — use whichever is clearer -->
```

### Multi-stop gradient (our background style)
```xml
<Rectangle>
    <Rectangle.Fill>
        <LinearGradientBrush StartPoint="0,0" EndPoint="1,1">
            <GradientStop Offset="0"   Color="#08080A" />
            <GradientStop Offset="0.5" Color="#0C0C0E" />
            <GradientStop Offset="1"   Color="#0F0F12" />
        </LinearGradientBrush>
    </Rectangle.Fill>
</Rectangle>
```

### Fade-out edge (top edge sheen — 🎯 APPLY to StartPage)
```xml
<!-- Subtle top-edge highlight, like iOS home screen -->
<Rectangle IsHitTestVisible="False">
    <Rectangle.Fill>
        <LinearGradientBrush StartPoint="0%,0%" EndPoint="0%,100%">
            <GradientStop Offset="0"   Color="#0AFFFFFF" />
            <GradientStop Offset="0.3" Color="#00000000" />
        </LinearGradientBrush>
    </Rectangle.Fill>
</Rectangle>
```

### Bottom fade-to-black overlay (for card track fade)
```xml
<!-- Horizontal fade-out on right edge for card overflow hint -->
<Rectangle HorizontalAlignment="Right" Width="40" IsHitTestVisible="False">
    <Rectangle.Fill>
        <LinearGradientBrush StartPoint="0%,50%" EndPoint="100%,50%">
            <GradientStop Offset="0" Color="#00000000" />
            <GradientStop Offset="1" Color="#FF0C0C0E" />
        </LinearGradientBrush>
    </Rectangle.Fill>
</Rectangle>
```

### SpreadMethod (how gradient fills beyond its range)
```xml
<!-- Pad (default): holds first/last stop color -->
<LinearGradientBrush SpreadMethod="Pad" StartPoint="0,0" EndPoint="0.5,0">
    <GradientStop Color="Red"  Offset="0" />
    <GradientStop Color="Blue" Offset="1" />
</LinearGradientBrush>

<!-- Reflect: mirrors the gradient -->
<LinearGradientBrush SpreadMethod="Reflect" StartPoint="0,0" EndPoint="0.5,0">

<!-- Repeat: tiles the gradient -->
<LinearGradientBrush SpreadMethod="Repeat" StartPoint="0,0" EndPoint="0.25,0">
```

---

## 3. RadialGradientBrush

**Source:** https://docs.avaloniaui.net/docs/graphics-animation/gradients

### Basic usage
```xml
<Ellipse Width="200" Height="200">
    <Ellipse.Fill>
        <RadialGradientBrush>
            <GradientStop Color="#6366F1" Offset="0" />
            <GradientStop Color="Transparent" Offset="1" />
        </RadialGradientBrush>
    </Ellipse.Fill>
</Ellipse>
```

### With Center + GradientOrigin (offset hotspot — our glow orb style)
```xml
<RadialGradientBrush Center="0.5,0.4" GradientOrigin="0.5,0.4">
    <GradientStop Offset="0"   Color="#14C9A96E" />
    <GradientStop Offset="0.6" Color="#07C9A96E" />
    <GradientStop Offset="1"   Color="#00C9A96E" />
</RadialGradientBrush>
```
> `Center` = center of the ellipse. `GradientOrigin` = where the focal hotspot is. Setting them to the same value gives a standard radial. Offsetting `GradientOrigin` creates an off-center look (like a lens flare).

### Corner glow (ambient light from corner)
```xml
<!-- Top-right ambient glow — matches our GlowOrb pattern -->
<RadialGradientBrush Center="0.85,0.15" GradientOrigin="0.85,0.15">
    <GradientStop Offset="0"   Color="#18C9A96E" />
    <GradientStop Offset="0.5" Color="#08C9A96E" />
    <GradientStop Offset="1"   Color="#00C9A96E" />
</RadialGradientBrush>

<!-- Bottom-left cool accent -->
<RadialGradientBrush Center="0.15,0.85" GradientOrigin="0.15,0.85">
    <GradientStop Offset="0"   Color="#0A4060C0" />
    <GradientStop Offset="0.6" Color="#00000000" />
</RadialGradientBrush>
```

### Vignette (darken edges — ✅ USED in StartPage)
```xml
<Rectangle IsHitTestVisible="False">
    <Rectangle.Fill>
        <RadialGradientBrush Center="0.5,0.5" GradientOrigin="0.5,0.5">
            <GradientStop Offset="0.35" Color="#00000000" />
            <GradientStop Offset="1"    Color="#66000000" />
        </RadialGradientBrush>
    </Rectangle.Fill>
</Rectangle>
```

---

## 4. ConicGradientBrush

**Source:** https://docs.avaloniaui.net/docs/graphics-animation/gradients

Sweeps colors angularly around a center point (like a colour wheel / pie chart).

```xml
<Ellipse Width="200" Height="200">
    <Ellipse.Fill>
        <ConicGradientBrush Center="50%,50%" Angle="0">
            <GradientStop Color="Red"    Offset="0" />
            <GradientStop Color="Yellow" Offset="0.33" />
            <GradientStop Color="Blue"   Offset="0.66" />
            <GradientStop Color="Red"    Offset="1" />
        </ConicGradientBrush>
    </Ellipse.Fill>
</Ellipse>
```

> **For StartPage:** A `ConicGradientBrush` could be used to create a rotating aurora/halo effect behind the logo as a subtle background decoration. The `Angle` property can be animated with keyframes.

---

## 5. VisualBrush

Paints an area using the rendered output of another visual element. Useful for reflections, tiling patterns, or using controls as fill.

```xml
<VisualBrush Stretch="None">
    <VisualBrush.Visual>
        <Ellipse Width="100" Height="100" Fill="Blue" />
    </VisualBrush.Visual>
</VisualBrush>
```

> **For StartPage:** Not needed currently. Most useful for custom tiled backgrounds or reflection effects.

---

## 6. ExperimentalAcrylicBrush (Frosted Glass)

**Source:** https://docs.avaloniaui.net/docs/graphics-animation/brushes

Provides a real OS-level frosted glass / acrylic blur effect. Works on Windows 10+ and macOS.

```xml
<!-- Must be on a TransparencyLevel-aware Window -->
<Window TransparencyLevelHint="AcrylicBlur"
        Background="Transparent">

    <!-- Acrylic panel -->
    <Panel>
        <Panel.Background>
            <ExperimentalAcrylicBrush MaterialOpacity="0.5"
                                      TintColor="#FF000000"
                                      TintOpacity="0.3"
                                      BackgroundSource="Digger" />
        </Panel.Background>
    </Panel>
</Window>
```

### Key properties
| Property | Description |
|---|---|
| `TintColor` | The tint color layered over the blur |
| `TintOpacity` | How opaque the tint is (0–1) |
| `MaterialOpacity` | How opaque the whole acrylic material is (0–1) |
| `BackgroundSource` | `Digger` (sees through window) or `Host` (captures behind the control) |

> **For StartPage:** Our `MainWindow` uses `TransparencyLevelHint="None"`. To enable real acrylic, change it to `"AcrylicBlur"` and set `Background="Transparent"`. The button panels and recent section header could then use `ExperimentalAcrylicBrush` for a true glass look. This is a significant change — test on Windows first.

---

## 7. ImmutableSolidColorBrush — Performance

For static brushes that never change, use `ImmutableSolidColorBrush` in C# code instead of `SolidColorBrush`. It's allocation-free and faster to render.

```csharp
using Avalonia.Media.Immutable;

// Use in custom controls, ItemsRepeater templates, etc.
private static readonly IBrush _bgBrush =
    new ImmutableSolidColorBrush(Color.Parse("#06FFFFFF"));

// Apply
myBorder.Background = _bgBrush;
```

> **For StartPage:** When creating `RecentCard` backgrounds from code-behind, use `ImmutableSolidColorBrush` to avoid per-item brush allocations.

---

## 8. BoxShadow (Border)

**Source:** https://docs.avaloniaui.net/docs/graphics-animation/effects

`BoxShadow` is a property on `Border` (and `ContentPresenter`). Syntax follows CSS `box-shadow`.

### Syntax
```
BoxShadow="offsetX offsetY blur spread color"
```

### Basic drop shadow
```xml
<Border BoxShadow="5 5 10 0 #80000000"
        CornerRadius="8"
        Background="White"
        Padding="20">
    <TextBlock Text="Shadow" />
</Border>
```

### Inset shadow (inner glow / pressed effect)
```xml
<Border BoxShadow="inset 0 2 4 0 #40000000"
        CornerRadius="8"
        Background="#F0F0F0"
        Padding="20">
    <TextBlock Text="Inset shadow" />
</Border>
```

### Multiple layered shadows (material elevation system)
```xml
<!-- Subtle elevation — resting card -->
<Border BoxShadow="0 1 3 0 #20000000" />

<!-- Medium elevation — hovered card -->
<Border BoxShadow="0 4 6 -1 #20000000, 0 2 4 -2 #20000000" />

<!-- High elevation — dragged / dialog -->
<Border BoxShadow="0 10 15 -3 #20000000, 0 4 6 -4 #20000000" />
```

### Glow effect (colored shadow — 🎯 APPLY to buttons)
```xml
<!-- Bronze glow on primary button hover -->
<Border BoxShadow="0 0 20 5 #40C9A96E"
        CornerRadius="8" />

<!-- Sharper inner glow ring (like Apple focus ring) -->
<Border BoxShadow="0 0 0 2 #60C9A96E, 0 4 12 0 #30C9A96E"
        CornerRadius="8" />
```

### rgba() colors in shadows
```xml
<!-- rgba() is fully supported, including multiple shadows -->
<Border BoxShadow="0 4 8 0 rgba(0,0,0,0.3), 0 2 4 0 rgba(0,0,0,0.1)"
        CornerRadius="8" Background="White" Padding="20">
    <TextBlock Text="RGBA shadows" />
</Border>
```

### 🎯 APPLY — Button lift shadow (add to `.PremiumBtn:pointerover`)
```xml
<Style Selector="Button.PremiumBtn:pointerover">
    <!-- Existing: lift -2px -->
    <Setter Property="TranslateTransform.Y" Value="-2" />
    <!-- NEW: add glow + elevation shadow -->
    <Setter Property="BoxShadow" Value="0 4 16 0 #30C9A96E, 0 2 6 0 #20000000" />
</Style>
<Style Selector="Button.PremiumBtn">
    <Setter Property="Transitions">
        <Transitions>
            <!-- Existing transitions... -->
            <!-- NEW: animate the box shadow -->
            <BoxShadowTransition Property="BoxShadow" Duration="0:0:0.2" />
        </Transitions>
    </Setter>
</Style>
```

### 🎯 APPLY — Recent card depth on hover
```xml
<Style Selector="Button.RecentCard">
    <Setter Property="BoxShadow" Value="0 1 4 0 #18000000" />
    <Setter Property="Transitions">
        <Transitions>
            <BoxShadowTransition Property="BoxShadow" Duration="0:0:0.3" Easing="CubicEaseOut" />
            <!-- ...existing transitions -->
        </Transitions>
    </Setter>
</Style>
<Style Selector="Button.RecentCard:pointerover">
    <Setter Property="BoxShadow" Value="0 8 24 0 #30000000, 0 0 16 0 #18C9A96E" />
</Style>
```

---

## 9. BlurEffect / DropShadowEffect

**Source:** https://docs.avaloniaui.net/docs/graphics-animation/effects

`Effect` is a property on any `Control`. Unlike `BoxShadow` (which is only on Border), `Effect` works on any element.

> ⚠️ `Effect` uses Skia compositing. It can be expensive on large elements. Use sparingly — mostly on small decorative elements like icons.

### BlurEffect
```xml
<!-- Background blur (use on a semi-transparent overlay for frosted glass sim) -->
<Rectangle Fill="#40000000">
    <Rectangle.Effect>
        <BlurEffect Radius="20" />
    </Rectangle.Effect>
</Rectangle>
```

### DropShadowEffect
```xml
<!-- DropShadowEffect on any control (not just Border) -->
<TextBlock Text="SIMBA">
    <TextBlock.Effect>
        <DropShadowEffect BlurRadius="12"
                          OffsetX="0"
                          OffsetY="4"
                          Color="#80000000"
                          Opacity="0.6" />
    </TextBlock.Effect>
</TextBlock>

<!-- On Path / Shape icon -->
<shapes:Path Data="M8 5v14l11-7z" Fill="White">
    <shapes:Path.Effect>
        <DropShadowEffect BlurRadius="8" OffsetX="0" OffsetY="2"
                          Color="Black" Opacity="0.5" />
    </shapes:Path.Effect>
</shapes:Path>
```

> ✅ **Already used in StartPage** on the play/music icons in recent cards.

### 🎯 APPLY — Wordmark text glow (subtle)
```xml
<TextBlock x:Name="WordmarkText" Text="SIMBA">
    <TextBlock.Effect>
        <DropShadowEffect BlurRadius="16"
                          OffsetX="0" OffsetY="0"
                          Color="#C9A96E"
                          Opacity="0.3" />
    </TextBlock.Effect>
</TextBlock>
```

---

## 10. OpacityMask

**Source:** https://docs.avaloniaui.net/docs/graphics-animation/effects

`OpacityMask` uses a brush as an alpha mask — white = fully visible, black = invisible, gradient = fade.

```xml
<!-- Fade element to transparent on right edge -->
<Border Width="300" Height="60">
    <Border.OpacityMask>
        <LinearGradientBrush StartPoint="0%,50%" EndPoint="100%,50%">
            <GradientStop Color="White"       Offset="0" />
            <GradientStop Color="White"       Offset="0.7" />
            <GradientStop Color="Transparent" Offset="1" />
        </LinearGradientBrush>
    </Border.OpacityMask>
    <!-- Content here fades to transparent on right -->
</Border>
```

### 🎯 APPLY — Recent card track: right-edge fade hint
```xml
<!-- Overlay on top of ScrollViewer to hint more cards to the right -->
<Rectangle HorizontalAlignment="Right"
           Width="60"
           IsHitTestVisible="False">
    <Rectangle.Fill>
        <LinearGradientBrush StartPoint="0%,50%" EndPoint="100%,50%">
            <GradientStop Color="#00000000" Offset="0" />
            <GradientStop Color="#FF0C0C0E" Offset="1" />
        </LinearGradientBrush>
    </Rectangle.Fill>
</Rectangle>
```

---

## 11. Control Transitions

**Source:** https://docs.avaloniaui.net/docs/graphics-animation/control-transitions

Transitions animate property changes caused by style triggers (like `:pointerover`). They use CSS-like easing.

### Available transition types
| Type | Property types it animates |
|---|---|
| `DoubleTransition` | `double` (Opacity, Width, Height, font size) |
| `BrushTransition` | `IBrush` (Background, BorderBrush, Foreground) |
| `ThicknessTransition` | `Thickness` (Margin, Padding, BorderThickness) |
| `ColorTransition` | `Color` |
| `CornerRadiusTransition` | `CornerRadius` |
| `BoxShadowTransition` | `BoxShadows` |
| `PointTransition` | `Point` |
| `SizeTransition` | `Size` |
| `TransformOperationsTransition` | `RenderTransform` (scale, translate, rotate) |

### Full syntax with easing
```xml
<Style Selector="Button.MyBtn">
    <Setter Property="Transitions">
        <Transitions>
            <!-- Animate opacity over 200ms with ease-out -->
            <DoubleTransition Property="Opacity"
                              Duration="0:0:0.2"
                              Easing="CubicEaseOut" />

            <!-- Animate background brush -->
            <BrushTransition Property="Background"
                             Duration="0:0:0.2" />

            <!-- Animate border brush -->
            <BrushTransition Property="BorderBrush"
                             Duration="0:0:0.2" />

            <!-- Animate corner radius -->
            <CornerRadiusTransition Property="CornerRadius"
                                    Duration="0:0:0.15" />

            <!-- Animate box shadow -->
            <BoxShadowTransition Property="BoxShadow"
                                 Duration="0:0:0.2"
                                 Easing="CubicEaseOut" />

            <!-- Animate render transform (scale/translate) -->
            <TransformOperationsTransition Property="RenderTransform"
                                           Duration="0:0:0.3"
                                           Easing="CubicEaseOut" />
        </Transitions>
    </Setter>
</Style>
```

### Easing function names (all available)
```
LinearEasing
CubicEaseIn   CubicEaseOut   CubicEaseInOut
SineEaseIn    SineEaseOut    SineEaseInOut
QuadEaseIn    QuadEaseOut    QuadEaseInOut
QuartEaseIn   QuartEaseOut   QuartEaseInOut
QuintEaseIn   QuintEaseOut   QuintEaseInOut
ExpoEaseIn    ExpoEaseOut    ExpoEaseInOut
CircEaseIn    CircEaseOut    CircEaseInOut
BackEaseIn    BackEaseOut    BackEaseInOut   (overshoot spring)
ElasticEaseIn ElasticEaseOut ElasticEaseInOut
BounceEaseIn  BounceEaseOut  BounceEaseInOut
SplineEasing  (custom cubic bezier)
```

### TransformOperationsTransition — the correct way to animate transforms
```xml
<!-- ✅ CORRECT — single transition on RenderTransform covers scale, rotate, translate -->
<Style Selector="Button.RecentCard">
    <Setter Property="RenderTransformOrigin" Value="50%,50%" />
    <Setter Property="Transitions">
        <Transitions>
            <TransformOperationsTransition Property="RenderTransform"
                                           Duration="0:0:0.3"
                                           Easing="CubicEaseOut" />
        </Transitions>
    </Setter>
</Style>
<Style Selector="Button.RecentCard:pointerover">
    <Setter Property="RenderTransform">
        <TranslateTransform Y="-4" />
    </Setter>
</Style>
```

---

## 12. Keyframe Animations

**Source:** https://docs.avaloniaui.net/docs/graphics-animation/keyframe-animations

Keyframe animations run declaratively in AXAML styles. Unlike Transitions (which respond to state changes), animations run automatically when a style is applied.

### Basic structure
```xml
<Style Selector="Border#MyPanel">
    <Setter Property="Opacity" Value="0" />
    <Style.Animations>
        <Animation Duration="0:0:0.5"
                   Delay="0:0:0.1"
                   FillMode="Forward"
                   Easing="CubicEaseOut">
            <KeyFrame Cue="0%">
                <Setter Property="Opacity" Value="0" />
            </KeyFrame>
            <KeyFrame Cue="100%">
                <Setter Property="Opacity" Value="1" />
            </KeyFrame>
        </Animation>
    </Style.Animations>
</Style>
```

### FillMode options
| Value | Behaviour |
|---|---|
| `None` | Returns to pre-animation state when done |
| `Forward` | Holds the final keyframe value (most common for entrance animations) |
| `Backward` | Applies first keyframe before animation starts |
| `Both` | Both Forward + Backward |

### PlaybackDirection
```xml
<!-- Infinite ping-pong loop (our glow orb animation) -->
<Animation Duration="0:0:12"
           IterationCount="INFINITE"
           PlaybackDirection="Alternate"
           Easing="SineEaseInOut">
```

### TransformGroup animation (translate + scale together)
```xml
<Style Selector="StackPanel#ButtonPanel">
    <Setter Property="Opacity" Value="0" />
    <Setter Property="RenderTransform">
        <Setter.Value>
            <TransformGroup>
                <TranslateTransform Y="12" />
                <ScaleTransform ScaleX="0.97" ScaleY="0.97" />
            </TransformGroup>
        </Setter.Value>
    </Setter>
    <Style.Animations>
        <Animation Duration="0:0:0.6" Delay="0:0:0.2"
                   FillMode="Forward" Easing="CubicEaseOut">
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
    </Style.Animations>
</Style>
```

### Animating brush color (opacity pulse)
```xml
<!-- Subtle breathing glow: opacity pulses 0.4 → 0.8 → 0.4 -->
<Style Selector="Border#AccentRing">
    <Setter Property="Opacity" Value="0.4" />
    <Style.Animations>
        <Animation Duration="0:0:3"
                   IterationCount="INFINITE"
                   PlaybackDirection="Alternate"
                   Easing="SineEaseInOut">
            <KeyFrame Cue="0%">   <Setter Property="Opacity" Value="0.4" /></KeyFrame>
            <KeyFrame Cue="100%"> <Setter Property="Opacity" Value="0.8" /></KeyFrame>
        </Animation>
    </Style.Animations>
</Style>
```

### Staggered entrance sequence (✅ pattern already in StartPage — replicate for new elements)
```xml
<!-- Delay each element by an increasing amount for cascade effect -->
<Animation Duration="0:0:0.4" Delay="0:0:0.10" FillMode="Forward" Easing="CubicEaseOut"> <!-- Logo -->
<Animation Duration="0:0:0.5" Delay="0:0:0.25" FillMode="Forward" Easing="CubicEaseOut"> <!-- Wordmark -->
<Animation Duration="0:0:0.6" Delay="0:0:0.35" FillMode="Forward" Easing="CubicEaseOut"> <!-- Tagline -->
<Animation Duration="0:0:0.6" Delay="0:0:0.45" FillMode="Forward" Easing="CubicEaseOut"> <!-- Buttons -->
<Animation Duration="0:0:0.5" Delay="0:0:0.60" FillMode="Forward" Easing="CubicEaseOut"> <!-- Recent section -->
<Animation Duration="0:0:0.5" Delay="0:0:0.90" FillMode="Forward" Easing="CubicEaseOut"> <!-- Kbd hint -->
```

---

## 13. Custom Fonts (Embedding Google Fonts)

**Source:** https://docs.avaloniaui.net/docs/how-to/custom-font-how-to  
**Quick guide sample:** https://github.com/AvaloniaUI/AvaloniaUI.QuickGuides/tree/main/GoogleFonts

### Step 1 — Add font files to project
```
src/App/Assets/Fonts/Outfit-Regular.ttf
src/App/Assets/Fonts/Outfit-Bold.ttf
src/App/Assets/Fonts/Outfit-Light.ttf
```

### Step 2 — Set build action in .csproj
```xml
<ItemGroup>
    <!-- Include font files as AvaloniaResource (NOT EmbeddedResource) -->
    <AvaloniaResource Include="Assets\Fonts\**\*.ttf" />
</ItemGroup>
```

### Step 3 — Register font family in App.axaml (or App.xaml)
```xml
<Application.Resources>
    <FontFamily x:Key="OutfitFont">
        avares://App/Assets/Fonts#Outfit
    </FontFamily>
</Application.Resources>
```
> The `#Outfit` part is the font family name as stored inside the TTF file. Use a font viewer to confirm the exact name.

### Step 4 — Use in AXAML
```xml
<!-- Via resource key (preferred) -->
<TextBlock FontFamily="{StaticResource OutfitFont}" Text="SIMBA" />

<!-- Inline URI (works but not recommended for repeated use) -->
<TextBlock FontFamily="avares://App/Assets/Fonts#Outfit" Text="SIMBA" />
```

### Step 5 — Update Typography.axaml token
```xml
<!-- In Typography.axaml — update existing token -->
<FontFamily x:Key="font-family-display">
    avares://App/Assets/Fonts#Outfit
</FontFamily>
```

### Then in StartPage, replace inline FontFamily
```xml
<!-- BEFORE (relies on system font) -->
<TextBlock FontFamily="Outfit" />

<!-- AFTER (guaranteed embedded font) -->
<TextBlock FontFamily="{StaticResource font-family-display}" />
```

> **Download Outfit from:** https://fonts.google.com/specimen/Outfit  
> Get Regular (400), Light (300), SemiBold (600), Bold (700) weights.

---

## 14. SpreadMethod — Gradient Fill Modes

When a gradient's coordinate range doesn't fill the entire element, `SpreadMethod` controls what happens outside:

```xml
<!-- Pad (DEFAULT): fills with the end stop color -->
<LinearGradientBrush SpreadMethod="Pad"
                     StartPoint="25%,50%" EndPoint="75%,50%">
    <GradientStop Color="Blue"  Offset="0" />
    <GradientStop Color="Green" Offset="1" />
</LinearGradientBrush>
<!-- Left 25% = Blue, middle = gradient, right 25% = Green -->

<!-- Reflect: gradient mirrors at each end -->
<LinearGradientBrush SpreadMethod="Reflect"
                     StartPoint="25%,50%" EndPoint="75%,50%">

<!-- Repeat: gradient tiles continuously -->
<LinearGradientBrush SpreadMethod="Repeat"
                     StartPoint="0%,50%" EndPoint="25%,50%">
<!-- Produces 4 copies of the gradient across the element -->
```

---

## 15. Applied Patterns for StartPage

This section maps each official API to a specific improvement in StartPage.axaml.

---

### A. Background Stack (Current + Recommended)

**Current (✅ correct pattern, minor tuning needed):**
```xml
<!-- Layer 1: Base dark gradient -->
<Rectangle IsHitTestVisible="False">
    <Rectangle.Fill>
        <LinearGradientBrush StartPoint="0,0" EndPoint="1,1">
            <GradientStop Offset="0"   Color="#08080A" />
            <GradientStop Offset="0.5" Color="#0C0C0E" />
            <GradientStop Offset="1"   Color="#0F0F12" />
        </LinearGradientBrush>
    </Rectangle.Fill>
</Rectangle>

<!-- Layer 2-4: Radial ambient lights -->
<Rectangle Fill="{StaticResource BgRadialWarm}" IsHitTestVisible="False" />
<Rectangle Fill="{StaticResource BgRadialCool}" IsHitTestVisible="False" />
<Rectangle Fill="{StaticResource BgRadialLeft}" IsHitTestVisible="False" />

<!-- Layer 5: Vignette -->
<Rectangle IsHitTestVisible="False">
    <Rectangle.Fill>
        <RadialGradientBrush Center="0.5,0.5" GradientOrigin="0.5,0.5">
            <GradientStop Offset="0.35" Color="#00000000" />
            <GradientStop Offset="1"    Color="#66000000" />
        </RadialGradientBrush>
    </Rectangle.Fill>
</Rectangle>
```

**🎯 ADD — Layer 6: Top edge sheen (Apple-style subtle top highlight)**
```xml
<!-- Insert AFTER vignette, BEFORE drag overlay -->
<Rectangle IsHitTestVisible="False" VerticalAlignment="Top" Height="120">
    <Rectangle.Fill>
        <LinearGradientBrush StartPoint="50%,0%" EndPoint="50%,100%">
            <GradientStop Offset="0"   Color="#0AFFFFFF" />
            <GradientStop Offset="1"   Color="#00000000" />
        </LinearGradientBrush>
    </Rectangle.Fill>
</Rectangle>
```

---

### B. Button Glow & BoxShadow (🎯 Add)

```xml
<!-- In UserControl.Resources — add these brushes -->
<SolidColorBrush x:Key="StartGlassBg"     Color="#09FFFFFF" />  <!-- was #05 -->
<SolidColorBrush x:Key="CardGlassBg"      Color="#0AFFFFFF" />  <!-- was #06 -->
<SolidColorBrush x:Key="StartAccentBorder" Color="#44C9A96E" /> <!-- was #33 -->

<!-- Add BoxShadow transitions in .PremiumBtn style -->
<Style Selector="Button.PremiumBtn">
    <Setter Property="Transitions">
        <Transitions>
            <DoubleTransition     Property="TranslateTransform.Y" Duration="0:0:0.2" Easing="CubicEaseOut" />
            <BrushTransition      Property="Background"           Duration="0:0:0.2" />
            <BrushTransition      Property="BorderBrush"          Duration="0:0:0.2" />
            <BoxShadowTransition  Property="BoxShadow"            Duration="0:0:0.2" Easing="CubicEaseOut" />
        </Transitions>
    </Setter>
</Style>
<Style Selector="Button.BtnPrimary:pointerover">
    <Setter Property="Background"  Value="#18C9A96E" />
    <Setter Property="BorderBrush" Value="{StaticResource StartAccent}" />
    <Setter Property="BoxShadow"   Value="0 4 16 0 #30C9A96E" />  <!-- bronze glow -->
</Style>
```

---

### C. Recent Card Depth (🎯 Add)

```xml
<Style Selector="Button.RecentCard">
    <Setter Property="BoxShadow" Value="0 1 4 0 #18000000" />
    <Setter Property="Transitions">
        <Transitions>
            <TransformOperationsTransition Property="RenderTransform" Duration="0:0:0.3" Easing="CubicEaseOut" />
            <BrushTransition              Property="BorderBrush"      Duration="0:0:0.2" />
            <BoxShadowTransition          Property="BoxShadow"        Duration="0:0:0.3" Easing="CubicEaseOut" />
        </Transitions>
    </Setter>
</Style>
<Style Selector="Button.RecentCard:pointerover">
    <Setter Property="RenderTransform">
        <TranslateTransform Y="-4" />
    </Setter>
    <Setter Property="BorderBrush" Value="{StaticResource StartAccentBorder}" />
    <Setter Property="BoxShadow"   Value="0 8 24 0 #28000000, 0 0 16 0 #18C9A96E" />
</Style>
```

---

### D. Wordmark Glow (🎯 Add)

```xml
<TextBlock x:Name="WordmarkText"
           FontSize="{a:Breakpoint XS=22, S=26, M=30, L=34, XL=38, XXL=40}"
           FontFamily="{StaticResource font-family-display}"
           FontWeight="Bold"
           LetterSpacing="{a:Breakpoint XS=4, S=5, M=6, L=7, XL=8, XXL=8}"
           TextAlignment="Center"
           Foreground="{StaticResource AppTextPrimary}">
    <TextBlock.Effect>
        <DropShadowEffect BlurRadius="20"
                          OffsetX="0" OffsetY="0"
                          Color="#C9A96E"
                          Opacity="0.25" />
    </TextBlock.Effect>
</TextBlock>
```

---

### E. Window Controls Hover (🎯 Add)

```xml
<!-- Minimize / Maximize hover -->
<Style Selector="Button.WindowCtrl:pointerover">
    <Setter Property="Background" Value="{StaticResource WindowButtonHoverBackground}" />
    <!-- WindowButtonHoverBackground = #2BFFFFFF (already in Colors.axaml) -->
</Style>

<!-- Close button — red on hover -->
<Style Selector="Button.WindowCtrl.close:pointerover">
    <Setter Property="Background" Value="{StaticResource WindowCloseButtonHoverBackground}" />
    <!-- WindowCloseButtonHoverBackground = #E81123 (already in Colors.axaml) -->
</Style>

<!-- Add smooth fade to WindowCtrl -->
<Style Selector="Button.WindowCtrl">
    <Setter Property="Transitions">
        <Transitions>
            <BrushTransition Property="Background" Duration="0:0:0.15" />
        </Transitions>
    </Setter>
</Style>
```

---

### F. Right-edge Fade Hint on Card Track (🎯 Add)

```xml
<!-- Inside RecentSection grid, overlaid on top of ScrollViewer -->
<Rectangle Grid.Row="2"
           HorizontalAlignment="Right"
           Width="48"
           IsHitTestVisible="False"
           ZIndex="1">
    <Rectangle.Fill>
        <LinearGradientBrush StartPoint="0%,50%" EndPoint="100%,50%">
            <GradientStop Offset="0" Color="#00000000" />
            <GradientStop Offset="1" Color="#FF0C0C0E" />
        </LinearGradientBrush>
    </Rectangle.Fill>
</Rectangle>
```

---

*Reference created: 2026-07-13. Source: official Avalonia docs (v12 current).*
