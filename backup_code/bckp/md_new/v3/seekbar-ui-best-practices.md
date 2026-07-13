# SeekBar — Best UI Experience

Linked files:
- [SeekBar.axaml](file:///x:/Development/Cine_CSharp_DotNet/srcv3/App/UI/Components/SeekBar/SeekBar.axaml) — layout & visuals
- [SeekBar.axaml.cs](file:///x:/Development/Cine_CSharp_DotNet/srcv3/App/UI/Components/SeekBar/SeekBar.axaml.cs) — interaction logic
- [ControlsBox.axaml](file:///x:/Development/Cine_CSharp_DotNet/srcv3/App/UI/Components/Shell/ControlsBox.axaml) — parent container
- [ControlsBox.axaml.cs](file:///x:/Development/Cine_CSharp_DotNet/srcv3/App/UI/Components/Shell/ControlsBox.axaml.cs) — visibility management
- [MainWindow.WindowControls.cs](file:///x:/Development/Cine_CSharp_DotNet/srcv3/App/UI/Shell/MainWindow.WindowControls.cs) — auto-hide timer

---

## 1. SeekBar Component Architecture

```
ControlsBox (Height="Auto", MinHeight="74")
└── ControlsBoxBorder (Opacity DoubleTransition 0.25s)
    └── Grid (3 rows: TrialBanner / Transport / SeekBar)
        └── SeekBar (Row 2)
            └── Grid (4 cols: SeekArea | PositionTime | Separator | DurationTime)
                └── SeekArea (Grid[0], pointer events)
                    ├── SeekTrack     (4px height, trough)
                    ├── SeekFill      (4px height, gradient + blue glow)
                    ├── SeekThumb     (16x16 circle, ScaleTransform hover)
                    ├── ChapterMarkersControl
                    └── ChapterPreviewPopover (TranslateTransform positioned, Opacity fade)
```

## 2. Key Design Decisions (Anti-Flicker)

### 2.1 Never change layout-affecting properties on pointer move (60fps)

**DO NOT change:** Width, Height, Margin, Padding, IsVisible, MinWidth
**USE instead:** RenderTransform (ScaleTransform, TranslateTransform), Opacity

Each layout change triggers `Measure → Arrange → LayoutUpdated` cascading up the visual tree.
If this hits a parent with a `DoubleTransition` on Opacity (like `ControlsBoxBorder`), flicker occurs.

### 2.2 Thumb hover expansion

- Use `ScaleTransform` (1.0 → 1.2) — zero layout impact
- `RenderTransformOrigin="0.5,0.5"` ensures centered expansion
- Applied in code-behind via `((ScaleTransform)SeekThumb.RenderTransform!).ScaleX/Y`

### 2.3 Time hint popover positioning

- Use `TranslateTransform` for X/Y offsets — zero layout impact
- Use `Opacity` (with transition) for show/hide — no `IsVisible` toggle
- `IsHitTestVisible="False"` — popover doesn't steal pointer events

### 2.4 Seek position tracking

- Thumb: `HorizontalAlignment="Left"` + `Margin` (left only) — minimal layout
- SeekFill: `HorizontalAlignment="Left"` + `Width`
- Both: `VerticalAlignment="Center"` ensures vertical centering on the 4px track

## 3. Thumb Visual Spec

| Property | Value | Reason |
|---|---|---|
| Size | 16×16 px | Compact, proportional to 4px track |
| CornerRadius | 8 | Perfect circle |
| Background | White (`ProgressSliderBackground`) | High contrast on dark track |
| Border | 1px `#33FFFFFF` | Subtle definition on dark backgrounds |
| Shadow | `DropShadowEffect` BlurRadius 6, Y offset 1.5, color `#4D5B9BD5` | Matches SeekFill blue glow |
| Hover scale | 1.0 → 1.2 (ScaleTransform) | Visual feedback, no layout thrash |
| RenderTransformOrigin | 0.5, 0.5 | Scales from center |

## 4. Time Hint Popover Visual Spec

| Property | Value | Reason |
|---|---|---|
| Background | `PopoverBackground` (#D019191B) | Dark semi-transparent, consistent with flyouts |
| CornerRadius | 4 | Snug, compact |
| Padding | 6,3 | Minimum padding for breathing room |
| FontSize | 11 | One step below md3-caption (12) |
| Shadow | `elevation-4` BoxShadow | Depth, consistent with flyouts |
| Opacity transition | 0.1s | Fast, snappy |
| No border | — | Cleaner than 1px outline |
| Show timing | Immediate (no debounce) | User expects instant feedback on hover |

## 5. Code-Behind Flow

```
OnSeekAreaPointerMoved (debounced to ~60fps / 16ms)
├── Thumb: ScaleTransform → 1.2 (if not already)
├── If seeking (pointer captured):
│   └── UpdateSeekBar() → update thumb Margin + SeekFill Width
└── If duration > 0 (popover):
    ├── Calculate chapter/time text
    ├── Opacity → 1
    ├── Calculate Y offset: -(popoverDesiredHeight + 6)
    ├── Clamp MaxWidth within track bounds ± 6px margin
    └── TranslateTransform X/Y → position centered above thumb

OnSeekAreaPointerExited
├── Popover Opacity → 0
└── Thumb ScaleTransform → 1.0

UpdateSeekBar()
├── thumbLeft = seekValue × (trackWidth - thumbWidth)
├── SeekThumb.Margin = (thumbLeft, 0, 0, 0)
└── SeekFill.Width = thumbLeft + thumbHalf
```

## 6. Auto-Hide Integration

- `SeekStarted` / `SeekEnded` events pause/resume auto-hide timer
- `MainWindow.WindowControls.cs` manages the 3-second idle timer
- `ControlsBoxBorder.IsVisible` toggled by timer, with Opacity transition

## 7. Chapter Markers

- `ItemsControl` with `Canvas` panel
- Each marker: 3×12 px rectangle, 60% opacity white
- Position: `Canvas.SetLeft` based on chapter time / duration ratio
- Tooltip shows chapter title + timestamp
