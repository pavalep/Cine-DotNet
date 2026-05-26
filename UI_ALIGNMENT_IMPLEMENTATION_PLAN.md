# UI Alignment Implementation Plan

## Project Overview
This plan outlines the step-by-step implementation process to achieve pixel-perfect alignment between the Avalonia implementation and the Python (GTK4) reference implementation of Cine.

## Phase 1: Foundation Setup (Week 1)

### Day 1-2: Resource System Setup
**Objective**: Create centralized resource dictionaries for colors, typography, and styles.

**Tasks**:
1. Create `Resources/` directory structure:
   ```
   Resources/
   ├── Colors.axaml
   ├── Typography.axaml
   ├── ButtonStyles.axaml
   ├── IconResources.axaml
   └── GradientBrushes.axaml
   ```

2. Implement `Colors.axaml`:
   - Extract exact color values from Python CSS
   - Define color resources with semantic names
   - Create gradient brush resources

3. Implement `Typography.axaml`:
   - Define font families (Consolas, system fonts)
   - Create text styles (heading, time-label, etc.)
   - Set up font sizing system

**Deliverables**:
- Complete color resource dictionary
- Complete typography resource dictionary
- Resource loading in App.axaml

### Day 3-4: Basic Layout Reconstruction
**Objective**: Convert from Grid layout to Overlay-based design.

**Tasks**:
1. Modify `MainWindow.axaml` structure:
   - Replace Grid with Overlay container
   - Set up video host as primary content
   - Create overlay containers for UI controls

2. Implement base styles:
   - Window transparency and blur effects
   - Black video background
   - OSD (On-Screen Display) styling

3. Create custom `Overlay` control if needed:
   - Handle multiple overlay children
   - Manage z-index ordering

**Deliverables**:
- Overlay-based main window structure
- Proper video area with black background
- Basic OSD styling applied

### Day 5: Button System Implementation
**Objective**: Implement circular button styles matching Python design.

**Tasks**:
1. Create `ButtonStyles.axaml`:
   - Circular button base style
   - Flat button variant (transport controls)
   - Toggle button styles
   - Menu button styles

2. Implement hover and active states:
   - Match Python's rgba transparency values
   - Add shadow effects for depth
   - Implement disabled state styling

3. Test button system:
   - Visual consistency with Python screenshots
   - Interaction feedback quality
   - Accessibility features

**Deliverables**:
- Complete button style system
- All button variants implemented
- Visual testing results

## Phase 2: Core Components (Week 2)

### Day 6-7: Icon System Implementation
**Objective**: Create symbolic icon system matching Python's iconography.

**Tasks**:
1. Extract icon paths from Python source:
   - Analyze `window.ui` for icon names
   - Convert symbolic icons to SVG path data
   - Create comprehensive icon resource dictionary

2. Implement `IconResources.axaml`:
   - Playback icons (play, pause, stop)
   - Skip icons (backward, forward)
   - Volume icons (mute, low, mid, max, overamp)
   - Track icons (subtitles, audio, video)
   - Control icons (shuffle, loop, playlist, options)

3. Create icon styling:
   - Consistent sizing (24px default)
   - Shadow effects matching Python
   - Color states (normal, hover, active, disabled)

**Deliverables**:
- Complete icon resource dictionary
- All reference icons implemented
- Icon styling system

### Day 8-9: Progress Bar & Timeline
**Objective**: Implement custom progress slider with Python styling.

**Tasks**:
1. Create custom `ProgressSlider` control:
   - Custom control template
   - White slider thumb (20px diameter)
   - Semi-transparent trough (rgba(255,255,255,0.225))
   - Shadow effects for depth

2. Implement time display system:
   - Elapsed time (left-aligned, negative margin)
   - Time separator (2px width, #DDD, 40% opacity)
   - Total time (right-aligned)
   - Consolas font family, 13px size

3. Add interaction features:
   - Drag behavior
   - Click-to-seek
   - Keyboard navigation (arrow keys)

**Deliverables**:
- Custom progress slider control
- Complete time display system
- Full interaction implementation

### Day 10: Menu System Implementation
**Objective**: Implement file menu and primary menu system.

**Tasks**:
1. Create `OpenMenuButton`:
   - Circular menu button style
   - Flyout with menu items:
     - Open Files
     - Open Folder
     - Add Files
     - Add Subtitle Track
     - Add Audio Track

2. Create `PrimaryMenuButton`:
   - Three-dot menu icon
   - Flyout with menu items:
     - New Window
     - Preferences
     - Keyboard Shortcuts
     - About Cine

3. Implement menu behaviors:
   - Show/hide based on start page visibility
   - Keyboard shortcuts
   - Accessibility labels

**Deliverables**:
- Complete menu button system
- All menu items implemented
- Menu behavior testing

## Phase 3: Advanced Features (Week 3)

### Day 11-12: Animation System
**Objective**: Implement revealer animations and transitions.

**Tasks**:
1. Create `RevealerBehavior`:
   - Attached property system
   - Transition types (slide-up, slide-down, fade, crossfade)
   - Duration and easing controls

2. Implement `UiAutoHideBehavior`:
   - Mouse/keyboard interaction tracking
   - Auto-hide timer system
   - Smooth show/hide transitions

3. Create animation utilities:
   - Cubic easing functions
   - Parallel animation support
   - Animation cancellation

**Deliverables**:
- Complete animation behavior system
- Auto-hide functionality
- Performance-optimized animations

### Day 13: Volume Control Popover
**Objective**: Implement volume control matching Python design.

**Tasks**:
1. Create `VolumeMenuButton`:
   - Circular button with volume icon
   - Icon changes based on volume level
   - Popover flyout behavior

2. Implement volume popover content:
   - Mute toggle button (circular)
   - Volume slider (180px width, 0-130 range)
   - Popover styling (background, border, shadow)

3. Add volume behavior:
   - Mute toggle functionality
   - Volume level icon updates
   - Keyboard shortcuts (M, arrow keys)

**Deliverables**:
- Complete volume control system
- Popover styling matching Python
- Full functionality implementation

### Day 14: Track Selection Menus
**Objective**: Implement subtitles, audio, and video track menus.

**Tasks**:
1. Create track menu buttons:
   - Subtitles menu button
   - Audio tracks menu button
   - Video tracks menu button
   - Circular button styling

2. Implement dynamic menu population:
   - Bind to view model collections
   - Track selection functionality
   - Current track indication (bold text)

3. Add menu styling:
   - Popover background and borders
   - Menu item styling
   - Hover and selection states

**Deliverables**:
- Complete track menu system
- Dynamic menu population
- Visual consistency with Python

## Phase 4: Polish & Integration (Week 4)

### Day 15-16: Start Page Implementation
**Objective**: Implement drag-and-drop start page.

**Tasks**:
1. Create `StartPage` overlay:
   - Gradient background matching Python
   - "Drag and Drop Files Here" title
   - Open buttons (Open…, Open Folder)

2. Implement drag-and-drop behavior:
   - File drop handling
   - Drop indicator animations
   - Visual feedback during drag

3. Add start page styling:
   - Button styles (pill buttons)
   - Suggested action styling
   - Typography and spacing

**Deliverables**:
- Complete start page implementation
- Drag-and-drop functionality
- Visual design matching Python

### Day 17: Playlist Controls
**Objective**: Implement shuffle, loop, and playlist controls.

**Tasks**:
1. Create playlist control buttons:
   - Shuffle toggle button
   - Playlist loop toggle button
   - File loop toggle button
   - Playlist dialog button

2. Implement toggle behaviors:
   - Visual checked states (white background)
   - Tooltip text
   - Keyboard shortcuts

3. Add playlist dialog:
   - Dialog window design
   - Playlist management
   - Current playing item highlighting

**Deliverables**:
- Complete playlist control system
- Toggle button functionality
- Playlist dialog implementation

### Day 18: Options Menu & PIP
**Objective**: Implement options menu and picture-in-picture.

**Tasks**:
1. Create `OptionsMenuButton`:
   - Circular button with options icon
   - Flyout with settings options
   - Styling matching Python

2. Implement `PipToggleButton`:
   - Picture-in-picture toggle
   - Icon state changes
   - Window behavior

3. Add options flyout content:
   - Settings controls
   - Layout and styling
   - Interaction behaviors

**Deliverables**:
- Complete options menu system
- PIP functionality
- Visual design consistency

### Day 19-20: Testing & Validation
**Objective**: Comprehensive testing and quality assurance.

**Tasks**:
1. Visual consistency testing:
   - Pixel-by-pixel comparison with Python screenshots
   - Color accuracy validation
   - Typography alignment checking

2. Functional testing:
   - All interactive components
   - Animation performance
   - Responsive design breakpoints

3. Accessibility testing:
   - Keyboard navigation
   - Screen reader compatibility
   - High contrast mode

4. Performance testing:
   - Memory usage profiling
   - Animation frame rates
   - Startup time optimization

**Deliverables**:
- Complete test suite
- Performance benchmarks
- Accessibility audit report
- Visual consistency report

## Technical Specifications

### File Structure Updates
```
Cine.Avalonia/
├── Resources/
│   ├── Colors.axaml
│   ├── Typography.axaml
│   ├── ButtonStyles.axaml
│   ├── IconResources.axaml
│   └── GradientBrushes.axaml
├── Behaviors/
│   ├── RevealerBehavior.cs
│   ├── UiAutoHideBehavior.cs
│   └── BreakpointBehavior.cs
├── Controls/
│   ├── CustomProgressSlider.axaml
│   ├── CustomProgressSlider.axaml.cs
│   └── OverlayControl.axaml
├── Styles/
│   └── App.axaml (updated)
├── MainWindow.axaml (completely rewritten)
└── MainWindow.axaml.cs (updated)
```

### Color System Specifications
| Resource Name | Python Value | Avalonia Value | Usage |
|---------------|--------------|----------------|-------|
| OsdForeground | `white` | `#FFFFFF` | All OSD text and icons |
| HeaderGradient | `rgba(0,0,0,0.14) → transparent` | Linear gradient | Header bar background |
| ControlsGradient | `rgba(0,0,0,0.2) → transparent` | Linear gradient | Controls box background |
| ButtonHoverBackground | `rgba(255,255,255,0.17)` | `#2BFFFFFF` | Button hover state |
| ButtonActiveBackground | `rgba(255,255,255,0.25)` | `#40FFFFFF` | Button active state |
| ProgressTroughBackground | `rgba(255,255,255,0.225)` | `#39FFFFFF` | Progress slider trough |
| TimeSeparatorBackground | `#DDDDDD` (40% opacity) | `#DDDDDD` with 0.4 opacity | Time display separator |

### Typography Specifications
| Style Name | Font Family | Size | Weight | Usage |
|------------|-------------|------|--------|-------|
| time-label | Consolas, monospace | 13px | Normal | Time display labels |
| time-elapsed | Consolas, monospace | 13px | Normal | Elapsed time (special margin) |
| heading | System font | 14px | Medium | Headers and titles |
| button-label | System font | 14px | Medium | Button text labels |

### Button Specifications
| Button Type | Size | Corner Radius | Icon Size | Spacing |
|-------------|------|---------------|-----------|---------|
| Circular Transport | 40x40px | 20px | 24x24px | 4px between buttons |
| Circular Menu | 32x32px | 16px | 24x24px | 4px between buttons |
| Circular Toggle | 40x40px | 20px | 24x24px | 4px between buttons |
| Pill Button | 40px height | 20px | N/A | 12px between buttons |

### Animation Specifications
| Animation Type | Duration | Easing | Usage |
|----------------|----------|--------|-------|
| UI Reveal/Hide | 300ms | CubicEaseOut | Main UI controls |
| Pause Indicator | 350ms | CubicEaseOut | Play/pause overlay |
| Drop Indicator | 200ms | CubicEaseOut | File drag feedback |
| Button Hover | 150ms | CubicEaseOut | Button state transitions |

## Risk Mitigation

### Technical Risks
1. **Performance Impact**: Overlay system with multiple animations may affect performance.
   - **Mitigation**: Implement lazy loading, optimize animation rendering, use GPU acceleration.

2. **Cross-Platform Compatibility**: Avalonia behaviors may work differently across platforms.
   - **Mitigation**: Test on Windows, Linux, and macOS during development.

3. **Resource Loading**: Multiple resource dictionaries may increase startup time.
   - **Mitigation**: Implement asynchronous loading, combine resources where possible.

### Design Risks
1. **Visual Inconsistency**: Difficult to achieve exact pixel matching.
   - **Mitigation**: Regular comparison with Python screenshots, use pixel measurement tools.

2. **Interaction Differences**: Avalonia and GTK4 have different interaction paradigms.
   - **Mitigation**: Custom event handling, user testing for interaction feedback.

### Schedule Risks
1. **Complexity Underestimation**: Some features may be more complex than anticipated.
   - **Mitigation**: Buffer time in schedule, prioritize core features first.

2. **Integration Issues**: New components may not integrate smoothly with existing code.
   - **Mitigation**: Incremental integration, comprehensive testing at each phase.

## Success Criteria

### Phase 1 Success Criteria
- [ ] Color system implemented and visually matches Python
- [ ] Typography system implemented with correct fonts and sizes
- [ ] Overlay-based layout structure working
- [ ] Circular button styles implemented and functional

### Phase 2 Success Criteria
- [ ] Icon system complete with all reference icons
- [ ] Custom progress slider with Python styling
- [ ] Menu system implemented with all items
- [ ] Volume control popover functional

### Phase 3 Success Criteria
- [ ] Revealer animations working smoothly
- [ ] UI auto-hide behavior implemented
- [ ] Track selection menus functional
- [ ] Responsive breakpoints working

### Phase 4 Success Criteria
- [ ] Start page with drag-and-drop functionality
- [ ] Playlist controls implemented
- [ ] Options menu and PIP functional
- [ ] All tests passing (visual, functional, performance, accessibility)

## Measurement & Reporting

### Daily Standups
- **Time**: 9:00 AM daily
- **Duration**: 15 minutes
- **Format**: What was done yesterday, what's planned today, any blockers

### Weekly Reviews
- **Time**: Friday 4:00 PM
- **Duration**: 1 hour
- **Content**: 
  - Progress against plan
  - Demo of completed features
  - Adjustments for following week
  - Risk assessment update

### Quality Gates
Each phase has specific quality gates that must be passed before proceeding:
1. **Phase 1 Gate**: Visual consistency score ≥ 90%
2. **Phase 2 Gate**: Functional test coverage ≥ 85%
3. **Phase 3 Gate**: Animation performance ≥ 50fps
4. **Phase 4 Gate**: All success criteria met

## Conclusion

This implementation plan provides a structured approach to achieving pixel-perfect alignment between the Avalonia and Python implementations of Cine. By following this phased approach with clear deliverables and success criteria, the team can systematically address the UI mismatches while maintaining code quality and performance.

Regular testing against the Python reference implementation will ensure visual accuracy throughout the development process, and the risk mitigation strategies will help address potential challenges as they arise.