# Phase 10.5 — Localization & Internationalization (i18n)

> **Goal**: Replace all hardcoded English strings with resource-based lookups, enabling future translation to any language. **Estimated effort: 5-6 hours**.

---

## Current State

All **32 `.axaml` files** and all **`.cs` code-behind files** use hardcoded English strings:

```xml
<!-- ❌ Hardcoded everywhere -->
<TextBlock Text="Open Files" />
<ToolTip.Tip>Play / Pause</ToolTip.Tip>
<Window Title="Preferences" />
```

```csharp
// ❌ Hardcoded in code
dialog.Title = "About Cine";
statusText.Text = "No media loaded";
```

No `.resx` resource files exist. No culture detection or switching infrastructure exists.

---

## Implementation Steps

### Step 1: Create Resource Files

```
UI/Resources/
├── Strings.resx          (default: English)
├── Strings.zh-CN.resx    (Chinese Simplified)
├── Strings.ja.resx       (Japanese)
├── Strings.ko.resx       (Korean)
└── Strings.de.resx       (German)
```

Each `.resx` contains key-value pairs:

```xml
<!-- Strings.resx -->
<data name="MainWindow_Title" xml:space="preserve">
    <value>Cine Media Player</value>
</data>
<data name="BtnOpenFiles" xml:space="preserve">
    <value>Open Files</value>
</data>
<data name="TooltipPlayPause" xml:space="preserve">
    <value>Play / Pause</value>
</data>
```

**String count estimate**: ~200 strings across all views.
**Categorization**:

| Category | Count | Example |
|----------|-------|---------|
| Window titles | 10 | "Preferences", "About Cine" |
| Button labels | 30 | "Open Files", "Play" |
| Tooltips | 40 | "Play / Pause (Space)" |
| Menu items | 50 | "File > Open", "View > Fullscreen" |
| Status messages | 30 | "No media loaded", "Loading..." |
| Dialog content | 20 | "Are you sure?", "Save changes?" |
| Error messages | 20 | "Failed to open file" |

### Step 2: Create `CultureService`

```csharp
public class CultureService
{
    public event Action? CultureChanged;

    public void SetCulture(string cultureCode)
    {
        var culture = new CultureInfo(cultureCode);
        CultureInfo.CurrentCulture = culture;
        CultureInfo.CurrentUICulture = culture;
        CultureChanged?.Invoke();
    }

    public string[] AvailableCultures => new[] { "en-US", "zh-CN", "ja", "ko", "de" };

    public string DetectSystemCulture()
    {
        return CultureInfo.InstalledUICulture.Name;
    }
}
```

### Step 3: Replace XAML Strings with `{x:Static}`

```xml
<!-- Add namespace -->
xmlns:res="clr-namespace:Cine.Avalonia.Resources"

<!-- ✅ After -->
<TextBlock Text="{x:Static res:Strings.MainWindow_Title}" />
<ToolTip.Tip>{x:Static res:Strings.TooltipPlayPause}</ToolTip.Tip>
```

### Step 4: Replace Code-Behind Strings

```csharp
// ❌ Before
dialog.Title = "About Cine";

// ✅ After
dialog.Title = Cine.Avalonia.Resources.Strings.AboutDialog_Title;
```

### Step 5: Culture-Aware Formatting

Ensure all number/date converters use the current culture:

```csharp
public class TimeSpanToStringConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is TimeSpan ts)
            return ts.ToString(@"h\:mm\:ss", culture);
        return null;
    }
}
```

### Step 6: Language Switcher in Preferences

Add a `ComboBox` to the Preferences dialog listing available cultures. On selection, call `CultureService.SetCulture()` and raise `CultureChanged` to refresh all bound strings.

> **Note**: XAML `{x:Static}` bindings don't react to runtime culture changes automatically. Consider using a custom `LocalizeExtension` markup extension or trigger a view rebuild on culture switch.

---

## Success Criteria

- [ ] `Strings.resx` created with all ~200 English strings
- [ ] All 32 `.axaml` files use `{x:Static}` bindings instead of hardcoded strings
- [ ] All `.cs` files use resource lookups instead of hardcoded strings
- [ ] `CultureService` detects system culture on first launch
- [ ] Language switcher in Preferences dialog
- [ ] Number/date formatting respects current culture
