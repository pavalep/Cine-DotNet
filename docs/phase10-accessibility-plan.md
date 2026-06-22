# Phase 10.9 — Accessibility (Beyond Phase 3.2)

> **Goal**: Achieve W3C WCAG 2.1 Level AA compliance for the player UI. **Estimated effort: 4-5 hours**.

---

## Current State

Phase 3.2 addressed `AutomationProperties.Name` on tooltip buttons only. Full accessibility requires:

| Requirement | Current Status | Target |
|------------|---------------|--------|
| `AutomationProperties.Name` on all interactive elements | Partial (buttons only) | All controls |
| `AutomationProperties.LiveSetting` for dynamic content | ❌ Missing | OSD, status messages |
| Keyboard navigation (TabIndex, Focusable) | ❌ Not configured | Logical tab order |
| Dialog focus management | ❌ Missing | Initial focus on open |
| High-contrast theme support | ❌ Missing | Colors adapt to system |
| Screen reader announcements | ❌ Missing | Dynamic content announced |
| Keyboard shortcuts documentation | Partial (InputRoutingService) | Accessible shortcuts dialog |

---

## WCAG 2.1 AA Checklist for Media Players

### 1. Non-text Content (WCAG 1.1.1)

| Element | Fix |
|---------|-----|
| All icon buttons | `AutomationProperties.Name` with descriptive text |
| Media poster/art | `AutomationProperties.Name` with title |
| Control icons (play, pause, volume) | `AutomationProperties.Name` matching tooltip |

```xml
<!-- ✅ Complete pattern -->
<Button ToolTip.Tip="Play / Pause (Space)"
        AutomationProperties.Name="Play media">
    <materialIcons:MaterialIcon Kind="Play" />
</Button>
```

### 2. Live Regions (WCAG 4.1.3)

OSD notifications and status changes must be announced to screen readers:

```xml
<!-- OSD notification area -->
<Border AutomationProperties.LiveSetting="Polite"
        AutomationProperties.Name="{Binding CurrentOsdMessage}" />
```

| Dynamic content | LiveSetting | Trigger |
|----------------|-------------|---------|
| Volume change | `Polite` | Volume adjustment |
| Play/Pause state | `Polite` | State change |
| Track change | `Assertive` | New media loaded |
| Error messages | `Assertive` | Error event |

### 3. Keyboard Navigation (WCAG 2.1.1, 2.4.3)

```xml
<!-- Set TabIndex for logical navigation order -->
<!-- Main playback controls: 10-19 -->
<StackPanel AutomationProperties.Name="Playback controls">
    <Button x:Name="BtnPlayPause" TabIndex="10" />
    <Button x:Name="BtnStop" TabIndex="11" />
    <Slider x:Name="SeekBar" TabIndex="12" />
</StackPanel>

<!-- Volume controls: 20-29 -->
<StackPanel AutomationProperties.Name="Volume controls">
    <Button x:Name="BtnMute" TabIndex="20" />
    <Slider x:Name="VolumeSlider" TabIndex="21" />
</StackPanel>
```

| Control type | Keyboard interaction |
|-------------|---------------------|
| Buttons | Enter/Space to activate |
| Sliders | Arrow keys to adjust, Home/End for min/max |
| Checkboxes | Space to toggle |
| ComboBoxes | Arrow keys to navigate, Enter to select |

### 4. Focus Management (WCAG 2.4.3)

```csharp
// Dialog opens → set initial focus
public class PreferencesDialog : Window
{
    protected override void OnOpened(EventArgs e)
    {
        base.OnOpened(e);
        // Focus the first interactive element
        CloseButton.Focus();
    }
}

// Ensure focus is trapped within modal dialogs
// Prevent Tab from moving behind the dialog
```

### 5. Color & Contrast (WCAG 1.4.3, 1.4.6)

| Requirement | Minimum Ratio | Enhanced Ratio |
|-------------|---------------|----------------|
| Text contrast | 4.5:1 (AA) | 7:1 (AAA) |
| Large text contrast | 3:1 (AA) | 4.5:1 (AAA) |
| UI component contrast | 3:1 (AA) | — |

**Action**: Verify all color pairs in `Colors.axaml` meet these ratios. Add high-contrast overrides.

### 6. Keyboard Shortcuts Accessibility (WCAG 2.1.4)

The `InputRoutingService` already supports shortcut registration. Add:

```csharp
// Allow users to remap shortcuts (future enhancement)
// For now, expose all shortcuts in the KeyboardShortcutsDialog
// with proper AutomationProperties for screen reader navigation
```

### 7. Media Alternatives (WCAG 1.2)

| Feature | Status |
|---------|--------|
| Closed captions/subtitles | ✅ Already implemented |
| Audio descriptions | ⚠️ Future (requires libmpv support) |
| Transcript | ❌ Not applicable for media player |

---

## Implementation Priority

| Priority | Task | Effort |
|----------|------|--------|
| 🔴 P0 | Add `AutomationProperties.Name` to all interactive elements | 1 hr |
| 🔴 P0 | Configure tab order for main playback controls | 30 min |
| 🟡 P1 | Add `AutomationProperties.LiveSetting` to OSD and status areas | 30 min |
| 🟡 P1 | Dialog focus management (set initial focus, trap tab) | 30 min |
| 🟡 P1 | Verify color contrast ratios for AA compliance | 30 min |
| 🟢 P2 | High-contrast theme variant | 1 hr |
| 🟢 P2 | Screen reader testing with Narrator/NVDA | 30 min |

---

## Success Criteria

- [ ] All interactive elements have `AutomationProperties.Name`
- [ ] Tab order follows visual layout in main window and all dialogs
- [ ] OSD notifications have `LiveSetting="Polite"` and are announced by screen readers
- [ ] Modal dialogs trap focus and set initial focus
- [ ] All text/background color pairs meet WCAG AA 4.5:1 contrast ratio
- [ ] Keyboard shortcuts dialog is screen-reader accessible
