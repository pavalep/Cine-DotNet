# Example: Converting a Screen with Gradients

**Scenario:** Your AXAML uses `LinearGradientBrush` or `RadialGradientBrush` for backgrounds.

## Input AXAML (slider track with gradient)
```xml
<Border Background="{StaticResource SliderTrackGradient}"
        CornerRadius="2" Height="4" Width="200">
</Border>
```

## Resource Definition (in Colors.axaml)
```xml
<LinearGradientBrush x:Key="SliderTrackGradient"
    StartPoint="0%,0%" EndPoint="100%,0%">
  <GradientStop Offset="0" Color="#FF0078D4" />
  <GradientStop Offset="1" Color="#FF00BCD4" />
</LinearGradientBrush>
```

## Converter Output (gradient support)

```javascript
// Slider track with gradient
var s3 = storage.createGradientRect(
  'SliderTrack',
  40, 450, 200, 4,
  'linear',
  [
    { offset: 0, color: '#0078D4', opacity: 1 },
    { offset: 1, color: '#00BCD4', opacity: 1 }
  ],
  { startX: 0, startY: 0, endX: 1, endY: 0 }
);
board.appendChild(s3);

// Fallback: If gradient fails, creates a solid rectangle with last stop color
// var s3 = storage.createRect('SliderTrack', 40, 450, 200, 4, '#00BCD4', 1, 2);
```

## Prompt to Achieve This

```
Convert [ScreenName] to Penpot. This screen uses gradients.
Use storage.createGradientRect() or storage.createGradientBoard() for
gradient-filled shapes. Fall back to solid color if gradient creation fails.

The resolver has already parsed gradient definitions.
Use resolveGradient('GradientName') to get the gradient descriptor.
```
