# Phase 1: Analysis & Requirements

## Objectives
- Identify all broken flyout implementations (`ShowAt` usage)
- Catalog all flyout types: HeaderBar, ControlsBox menus, Subtitle/Audio/Video
- Define unified overlay design requirements

## Findings
- **Critical Bug**: Avalonia Popup layer incompatibility on Windows10 SDK
- **Pattern**: All controls had similar issues but varied implementations
- **Best Practice**: Existing `TrackFlyoutBuilder` already worked correctly

## Action Items
1. Refactor all controls to use `FlyoutOverlayControl`
2. Create consistent styling `flyout-item` class
3. Implement mutual exclusion manager
4. Add cross-cutting concerns (dismissal, positioning)

## Success Metrics
- Zero unhandled exceptions from flyout interactions
- All menus open with correct positioning
- Consistent visual appearance across all menus

*Completed on: 2026-06-26*