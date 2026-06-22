# Phase 10.6 — Theme & Appearance System Formalization

> **Goal**: Add light theme support, runtime theme switching, and high-contrast detection. **Estimated effort: 2-3 hours**.

---

## Current State

The app has well-organized resource dictionaries but is hardcoded to dark mode:

| Resource file | Content | Dark values only? |
|---------------|---------|-------------------|
| [`Colors.axaml`](file:///x:/Development/Cine_CSharp_DotNet/src/App/UI/Resources/Colors.axaml) | All color keys (`AppBackground`, `OsdForeground`, etc.) | ✅ Yes |
| [`Typography.axaml`](file:///x:/Development/Cine_CSharp_DotNet/src/App/UI/Resources/Typography.axaml) | Font sizes, weights | ✅ (theme-independent) |
| [`Spacing.axaml`](file:///x:/Development/Cine_CSharp_DotNet/src/App/UI/Resources/Spacing.axaml) | Spacing tokens | ✅ (theme-independent) |
| [`Sizes.axaml`](file:///x:/Development/Cine_CSharp_DotNet/src/App/UI/Resources/Sizes.axaml) | Size constants | ✅ (theme-independent) |
| [`Elevation.axaml`](file:///x:/Development/Cine_CSharp_DotNet/src/App/UI/Resources/Elevation.axaml) | Shadow values | ⚠️ May differ per theme |
| [`Radius.axaml`](file:///x:/Development/Cine_CSharp_DotNet/src/App/UI/Resources/Radius.axaml) | Border radius | ✅ (theme-independent) |

No theme switching mechanism exists. No high-contrast mode detection.

---

## Implementation Steps

### Step 1: Split Colors into Light/Dark Variants

```
UI/Resources/
├── Colors.Dark.axaml         (existing dark values)
├── Colors.Light.axaml        (new light values)
└── App.axaml                 (switch which is loaded dynamically)
```

**Light theme color mapping**:

| Key | Dark Value | Light Value |
|-----|-----------|-------------|
| `AppBackground` | `#1A1A1E` | `#F5F5F5` |
| `OsdForeground` | `#FFFFFF` | `#1A1A1E` |
| `PopoverBackground` | `#2C2C30` | `#E8E8E8` |
| `HoverSubtle` | `#33FFFFFF` | `#331A1A1E` |
| `TrackActive` | `#FFFFFF` | `#1A1A1E` |
| `TrackInactive` | `#66FFFFFF` | `#661A1A1E` |

### Step 2: Create `ThemeService`

```csharp
public enum AppTheme { Dark, Light, HighContrast }

public class ThemeService
{
    private readonly IServiceProvider _services;

    public AppTheme CurrentTheme { get; private set; } = AppTheme.Dark;

    public event Action<AppTheme>? ThemeChanged;

    public void SetTheme(AppTheme theme)
    {
        CurrentTheme = theme;
        ApplyTheme(theme);
        ThemeChanged?.Invoke(theme);
    }

    private void ApplyTheme(AppTheme theme)
    {
        // Switch the merged dictionary from Colors.Dark.axaml ↔ Colors.Light.axaml
        // by removing the old and adding the new
    }

    public void DetectHighContrast()
    {
        // On Windows: SystemParameters.HighContrast
        // If true, set theme to HighContrast (or fallback to a high-contrast variant)
    }
}
```

### Step 3: Add Theme Toggle to Preferences

```xml
<!-- Preferences dialog -->
<ComboBox SelectedItem="{Binding SelectedTheme}">
    <ComboBoxItem>Dark</ComboBoxItem>
    <ComboBoxItem>Light</ComboBoxItem>
    <ComboBoxItem>System (Follow OS)</ComboBoxItem>
</ComboBox>
```

Persist the selection in settings store.

### Step 4: High-Contrast Detection

```csharp
// On Windows, use P/Invoke or SystemParameters
// https://learn.microsoft.com/en-us/windows/win32/api/winuser/ns-winuser-highcontrasta

[DllImport("user32.dll")]
private static extern bool SystemParametersInfo(uint uiAction, uint uiParam, ref HIGHCONTRAST pvParam, uint fWinIni);

private const uint SPI_GETHIGHCONTRAST = 0x0042;

public static bool IsHighContrastEnabled()
{
    var hc = new HIGHCONTRAST();
    hc.cbSize = Marshal.SizeOf(hc);
    if (SystemParametersInfo(SPI_GETHIGHCONTRAST, 0, ref hc, 0))
        return (hc.dwFlags & 0x00000001) != 0; // HCF_HIGHCONTRASTON
    return false;
}
```

### Step 5: Update `App.axaml` to Support Dynamic Theming

```xml
<Application.Styles>
    <FluentTheme />
    <materialIcons:MaterialIconStyles />
    <!-- Colors loaded dynamically by ThemeService -->
    <StyleInclude Source="avares://App/UI/Resources/MenuStyles.axaml" />
</Application.Styles>
```

---

## Success Criteria

- [ ] `Colors.Dark.axaml` and `Colors.Light.axaml` exist with full color key coverage
- [ ] `ThemeService` switches themes at runtime without restart
- [ ] Preferences dialog has a theme selector
- [ ] High-contrast mode is detected and applied on Windows
- [ ] All controls look correct in both light and dark themes
