# Phase 10.4 — Cross-Platform Readiness

> **Goal**: Make the project buildable on macOS and Linux, unlocking Avalonia's core value proposition. **Estimated effort: 4-5 hours**.

---

## Current Blockers

| Blocker | File | Impact |
|---------|------|--------|
| `net10.0-windows` target only | [`App.csproj:6`](file:///x:/Development/Cine_CSharp_DotNet/src/App/App.csproj#L6) | Can't compile on macOS/Linux |
| `UseWindowsForms=true` | [`App.csproj:11`](file:///x:/Development/Cine_CSharp_DotNet/src/App/App.csproj#L11) | Blocks non-Windows builds entirely |
| `Win32PlatformOptions` | [`App.axaml.cs:130-134`](file:///x:/Development/Cine_CSharp_DotNet/src/App/App.axaml.cs#L130-L134) | Windows-specific rendering config |
| Hardcoded `Ctrl` key | [`MainWindow.Input.cs`](file:///x:/Development/Cine_CSharp_DotNet/src/App/UI/Shell/MainWindow.Input.cs) | No `Cmd` mapping for macOS |
| `\` path separators | Various string literals | Breaks on Linux/macOS |

---

## Implementation Steps

### Step 1: Multi-Target the Project

```xml
<!-- App.csproj -->
<OutputType>WinExe</OutputType>
<TargetFrameworks>net10.0-windows;net10.0-macos;net10.0</TargetFrameworks>
```

- `net10.0-windows` — Windows build (full features)
- `net10.0-macos` — macOS build
- `net10.0` — Linux build (no platform-specific APIs)

### Step 2: Remove `UseWindowsForms`

```xml
<!-- ❌ Remove this line — blocks non-Windows -->
<UseWindowsForms>true</UseWindowsForms>
```

Replace any `System.Windows.Forms` dependency with Avalonia-native alternatives.

### Step 3: Guard Windows-Specific Code

```csharp
// App.axaml.cs — guard Win32PlatformOptions
#if WINDOWS
    .With(new Win32PlatformOptions
    {
        RenderingMode = new[] { Win32RenderingMode.AngleEgl, Win32RenderingMode.Software },
        CompositionMode = new[] { Win32CompositionMode.RedirectionSurface }
    })
#endif
#if MACOS
    .With(new MacOSPlatformOptions { ... })
#endif
```

### Step 4: Create `KeyboardHelper` for Cross-Platform Keys

```csharp
public static class KeyboardHelper
{
    public static KeyModifiers PrimaryModifier =>
        OperatingSystem.IsMacOS() ? KeyModifiers.Meta : KeyModifiers.Control;

    public static bool IsPrimaryModifier(KeyModifiers modifiers) =>
        (modifiers & PrimaryModifier) != 0;
}
```

Replace all hardcoded `KeyModifiers.Control` checks with `KeyboardHelper.PrimaryModifier`.

### Step 5: Audit Path Separators

```csharp
// ❌ Before
var path = "C:\\Users\\...";

// ✅ After
var path = Path.Combine("C:", "Users", "...");
// Or use Path.DirectorySeparatorChar
```

---

## Platform-Specific Feature Matrix

| Feature | Windows | macOS | Linux |
|---------|---------|-------|-------|
| mpv video playback | ✅ | ⚠️ (libmpv must be installed) | ⚠️ (libmpv must be installed) |
| File dialogs | ✅ (native) | ✅ (native) | ✅ (native) |
| Title bar customization | ✅ | ✅ | ⚠️ (limited) |
| Single-instance | ✅ | ⚠️ (different mechanism) | ⚠️ |
| MSIX packaging | ✅ | ❌ | ❌ |

---

## Success Criteria

- [ ] Project builds on `net10.0-macos` target
- [ ] Project builds on `net10.0` (Linux) target
- [ ] `UseWindowsForms=true` removed
- [ ] All `Win32PlatformOptions` guarded with `#if WINDOWS`
- [ ] Keyboard shortcuts use `KeyboardHelper` for cross-platform modifier keys
- [ ] All file paths use `Path.Combine()` or `/` separators
