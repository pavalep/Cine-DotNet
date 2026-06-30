# Debug Session: input-routing-broken

**Status:** [OPEN]  

**Symptom:** No keyboard shortcuts work. No menus open. App is focused but unresponsive to keyboard.

## Hypotheses

### H1: InputRoutingService.TryHandle returns false for all keys
The `OnKeyDown` handler calls `_inputRouter?.TryHandle(e.Key, modifiers, out action)`. If `TryHandle` returns `false` for all bindings (due to wrong scope or init failure), all keyboard input is silently consumed.

### H2: OnKeyDown event is never raised
The window might not be receiving keyboard events at all — focus could be captured by a child control that doesn't bubble events up.

### H3: PopulatePaletteCommands() or Register() throws during startup
If any `Register()` call throws (e.g., duplicate key binding), the `RegisterKeyboardShortcuts()` method might exit early, leaving the router empty.

### H4: InputRoutingService scope stack is corrupt
The stack-based scope routing might be initialized with wrong scope (e.g., stuck in TextEdit), causing TryHandle to reject all normal keys.

## Instrumentation Plan
1. Add debug logging at the start of `OnKeyDown` to see if it's called.
2. Add debug logging at `InputRoutingService.TryHandle` to see scope state.
3. Add debug logging after `RegisterKeyboardShortcuts()` to count bindings.
