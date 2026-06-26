# Debug: App Not Launching

- **Session ID**: `app-not-launching`
- **Status**: [FIXED] ✅
- **Created**: 2026-06-26
- **Symptom**: App exits silently, no window appears
- **Build**: ✅ 0 errors

## Hypotheses

| # | Hypothesis | Status |
|---|-----------|--------|
| H1 | Exception in Main() before OnFrameworkInitializationCompleted | CONFIRMED |
| H2 | ShowMainWindow() DI resolution fails | NOT REACHED |
| H3 | First-launch dialog / runtime download blocks | NOT REACHED |
| H4 | ANGLE/OpenGL renderer init fails | NOT REACHED |
| H5 | Window off-screen or invisible | NOT REACHED |

## Evidence Log

**Pre-fix (Release build):**
```
=== Cine.Avalonia starting ===
App.Initialize() - before base
Initialize FAILED: System.ArgumentException: An item with the same key has already been added. Key: TextTertiary
   at Colors.axaml:line 235
FATAL: An item with the same key has already been added. Key: TextTertiary
```

## Root Cause

Duplicate `TextTertiary` resource in `Colors.axaml`:
- Line 173: `AppTextOnDarkTertiary` section (original)
- Line 238: Duplicate text hierarchy block (added during §7-10 refactor)

## Fix

Removed duplicate `TextTertiary` definition at line 238 in `Colors.axaml`.
