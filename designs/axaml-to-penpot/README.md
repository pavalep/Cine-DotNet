# AXAML ↔ Penpot — Universal UI Design Toolkit

> Convert ANY Avalonia AXAML UI to editable Penpot designs — one command.  
> **Read Penpot shapes back to AXAML too.**  
> Works with ANY project, ANY directory structure, ANY element types.

## What This Toolkit Does

| Feature | Description |
|---|---|
| **Auto-discovery** | Finds ALL `.axaml` files in ANY project structure — no path config needed |
| **Resource resolution** | Parses Colors, Spacing, Radius, Typography, and custom resources |
| **Gradient support** | Full `LinearGradientBrush` and `RadialGradientBrush` → Penpot native gradients |
| **CSS class resolution** | Maps `Classes="md3-headline2"` → correct FontSize from Typography.axaml |
| **Intelligence layer** | Detects lists, playlists, sliders → adds realistic dummy data |
| **Bidirectional** | Read Penpot shapes → generate AXAML (reverse direction) |
| **Any-element fallback** | Unknown/custom elements get generic handler — nothing is lost |
| **Universal** | Works with any folder structure, any element types, any Avalonia project |

## Install

```bash
cd axaml-to-penpot
npm install
```

**Required packages (in `package.json`):**

| Package | Version | Purpose |
|---|---|---|
| `fast-xml-parser` | `^5.9.3` | Parse AXAML XML to JavaScript objects with attribute support |
| `fdir` | `^6.5.0` | Fast recursive directory traversal for auto-discovery |

**To use in ANY project:**

```bash
# Option A: Copy this folder into your project
cp -r axaml-to-penpot /path/to/your/project/designs/
cd /path/to/your/project/designs/axaml-to-penpot
npm install

# Option B: Run from anywhere using --project-root
node /path/to/axaml-to-penpot/convert.mjs MyScreen 1280 800 --project-root /path/to/project

# Option C: Install globally via npm link
cd axaml-to-penpot && npm link
# Then: axaml-to-penpot MyScreen 1280 800 --project-root /path/to/project
```

## Quick Start

### 1. Discover Screens

```bash
# List all AXAML screens found in the project
node convert.mjs --list --project-root "../../"

# Output example:
#   StartPage           → src/App/UI/Components/Start/StartPage.axaml
#   SettingsPage        → src/App/UI/Components/Settings/SettingsPage.axaml
#   ... 68 screens found
```

### 2. Convert a Screen (AXAML → Penpot)

```bash
# Auto-discover and convert (single file at a time)
node convert.mjs StartPage 1280 800 --project-root "../../"

# With a specific file path
node convert.mjs --axaml-path "src/Views/MainWindow.axaml" 1920 1080

# Mobile dimensions
node convert.mjs LoginPage 375 812 --project-root "../../"
```

The output is JavaScript code that creates Penpot shapes via the helpers.  
All screens go on the **same** page `Avalonia - Design System` (Figma-style single canvas).

### 3. Register Helpers in Penpot (once per session)

Copy the **entire** content of `helpers.js` and execute via MCP:
```
execute_code (type: script)
```

This registers `storage.createBoard()`, `storage.createText()`, `storage.createRect()`, `storage.prepareSinglePage()`, etc.

### 4. Setup Single Page & Assets (once per project)

Execute this once to create the shared canvas page and import AXAML resources:

```javascript
// Create/ensure single page
storage.prepareSinglePage();
// Import all colors (~70+) and typographies (~7) from Colors.axaml / Typography.axaml
// THIS MUST be executed AFTER helpers.js is registered
storage.createLibraryAssets();
```

### 5. Execute Generated Code

Take the converter's stdout from step 2 and execute via MCP:
```
execute_code (type: script)
```

### 6. Verify

```javascript
// Run in MCP execute_code to inspect the result
penpotUtils.shapeStructure(penpot.currentPage, 4)
```

---

## Workflow: One File at a Time

Each `.axaml` file is converted **individually** — no batch mode. The model processes one screen per invocation:

```bash
# Convert one file
node convert.mjs ScreenName 1280 800 --project-root "../../"

# Execute the output JS in Penpot → repeat for the next file
```

Each conversion:
- Creates **one Penpot page** named `Avalonia - [Name]`
- Creates **one board** on that page at size `W×H`
- Renders all shapes (rects, text, paths, gradients) inside the board
- The board background color defaults to `#0C0C0E`

---

## Naming Convention

| Aspect | Convention |
|---|---|
| **Penpot Page** | `Avalonia - {ComponentName}` (e.g., `Avalonia - StartPage`) |
| **Board name** | Same as component name |
| **Shape names** | `{Component}_{type}_{index}` (e.g., `Button_bg`, `Rect_0`) |
| **Canvas defaults** | 1280×800 (desktop), override with custom W×H |

---

## Intelligent Arrangement in Penpot

When laying out components in Penpot, arrange them like a well-organized Figma or Stitch file — not dumped at (0,0). Group related components together and use spatial hierarchy:

### Grouping by Type

```
🖥️  Main Screens          (top-left, large boards)
├── PlaybackScreen         # Main player view
├── StartPage              # Landing / home

💬  Dialogs                (center section, medium boards)
├── AboutDialog
├── PreferencesDialog
├── SubtitleSettingsDialog

🪟  Flyouts / Overlays     (right section, smaller boards)
├── VolumeFlyout
├── SubtitleOverlay
├── AudioEqualizerFlyout

🧩  Components             (bottom section, compact boards)
├── HeaderBar              # Positioned above PlaybackScreen
├── SeekBar                # Inside the control bar area
├── ControlBar             # Below the playback area
```

### Arrangement Guidelines

| Principle | Example |
|---|---|
| **Top-down hierarchy** | HeaderBar → PlaybackScreen → ControlBar (vertical stack) |
| **Related = adjacent** | VolumeFlyout next to AudioEqualizerFlyout (same row) |
| **Size-appropriate grid** | Large screens fill width; small components share a row |
| **Visual separation** | 30–50px gap between groups, subtle background for sections |
| **Labels for context** | Add a visible text label above each board with its component name |
| **Consistent alignment** | Left-align all boards in a column; center solo boards |

### Expected Result

The Penpot page should look like a curated design system page — not a messy pile. Clear grouping, breathing room between sections, and logical order (screens → dialogs → flyouts → components).

### Model Adjustment Rule

The converter generates shapes at AXAML-native coordinates, but the model **may adjust positioning** for visual polish when the main elements are present. Examples:

| Situation | Adjustment |
|---|---|
| Button at x=0 in a 1280-wide board | Re-center buttons horizontally under the content |
| List items left-aligned with no margin | Add consistent padding from parent bounds |
| Component stacked at (0,0) | Arrange in a spaced row/column layout |
| Label text clipped or overlapped | Reposition for readability |

This applies only to **positioning tweaks** — shape structure, colors, and sizes stay true to the original AXAML.

**No manual correction files**: Do NOT create separate fix/correction JS files (e.g., `xxx_corrected.js`). The model must produce correct output directly using its own capabilities. If the output is wrong, fix the converter or adjust the approach — never leave a manual fix file alongside the real output.

### Pre-Execution Verification Checklist

Before running generated JS in Penpot, verify:

| # | Check | How |
|---|---|---|
| 1 | **Button dimensions** | Read the AXAML style selector for `MinWidth`/`Height`/`Padding` — don't guess |
| 2 | **Button colors** | Check style selector `Background`/`Foreground` + modifier styles |
| 3 | **Text centering** | `align='center'` on `auto-width` text places the **left edge** at x, not center |
| 4 | **Use `centerTextX()`** | Call `storage.centerTextX(text, centerX)` **after** appending to board |
| 5 | **Code-behind elements** | Check `.axaml.cs` for programmatically created elements |
| 6 | **Dummy data context** | Use app-relevant filenames (`.mp4`/`.mkv` for media player, not `.pdf`) |
| 7 | **Button text sizing** | Call `txt.resize(btnW, lineHeight)` so `align='center'` works |

### Post-Execution Verification

After running JS, read back all shape positions to confirm:

```javascript
var page = penpot.root.children[0];
var children = page.children;
var result = [];
for (var i = 0; i < children.length; i++) {
  var s = children[i];
  result.push(s.name + ' | x=' + Math.round(s.x) + ' y=' + Math.round(s.y)
    + ' w=' + Math.round(s.width) + ' h=' + Math.round(s.height));
}
return result.join('\n');
```

Verify:
- Centered elements: `x + width/2 === centerX` (not `x === centerX`)
- Button text: same `x` and `width` as the button background rect
- Colors: match style selector values via `.fills[0].fillColor`
- Recent items: positioned with correct left padding from code-behind

### Single Canvas Rule (Figma-style)

ALL screens/boards go on ONE shared page called **"Avalonia - Design System"**. Do NOT create separate pages per screen. Use `storage.prepareSinglePage()` which appends boards without clearing existing content. Board x-position is auto-offset.

### Library Assets & Design Tokens

Penpot's Library stores reusable Colors, Typography, and Components — like Avalonia's resource dictionaries. Run `storage.createLibraryAssets()` once to import all colors (~70+) and typographies (7 styles) from your `Colors.axaml` and `Typography.axaml` into Penpot, plus 2 Design Token Sets.

### Reverse: Penpot → AXAML

```bash
# Step A: Export shapes from Penpot via MCP
# Copy examples/penpot-read-script.js → execute via MCP execute_code
# Save the JSON output to shapes.json

# Step B: Convert back to AXAML
node lib/reverse-convert.mjs shapes.json --output MyScreen.axaml
```

---

## File Structure

```
axaml-to-penpot/
├── convert.mjs                     # MAIN: AXAML → Penpot JS converter
├── helpers.js                      # Penpot runtime helpers (register via MCP)
├── UNIVERSAL-PROMPT-ENGINE.md      # ★ MASTER reference — all prompts, all elements
├── prompt-guide.md                 # 8 ready-to-use scenario prompts
├── README.md                       # This file — quick start guide
├── package.json                    # npm: fast-xml-parser + fdir
├── lib/
│   ├── discovery.mjs               # Auto-discover all .axaml files in any project
│   ├── resolver.mjs                # Parse/Resolve resources, gradients, CSS styles
│   ├── penpot-api.mjs              # Penpot JS API reference + code snippets
│   └── reverse-convert.mjs         # Penpot shapes → AXAML converter (NEW)
├── examples/
│   ├── all-elements.md             # ★ COMPLETE element reference with code patterns
│   ├── penpot-read-script.js       # Ready-to-run Penpot shape reader (MCP script)
│   ├── with-gradients.md           # Gradient conversion pattern
│   ├── smart-lists.md              # ListBox with dummy data pattern
│   └── penpot-to-axaml.md          # Reverse direction pattern
└── temp_output.js                  # (runtime: last generated output)
```

---

## Hybrid Approach

The converter uses a hybrid strategy for best results:

| AXAML Element | Penpot Method | Result |
|---|---|---|
| `TextBlock` | `createText()` + `font.applyToText()` | Fully editable text with correct weight |
| `Rectangle` | `createRect()` | Editable rect with fill/stroke/radius |
| `Ellipse` | `createEllipse()` | Editable circle/ellipse |
| `Path` | `createFromSvg()` | Editable vector shapes |
| `Button` | Rect (bg) + Text (label) | Editable composite |
| `Border` | Rect with fill + stroke + radius | Container border |
| Gradients | `createGradientRect()` / `createGradientBoard()` | Native Penpot gradients |
| `8-digit hex` | `setFillFromHex8()` | `#AARRGGBB` → color + opacity |

## Intelligence Layer

The converter auto-detects patterns and generates realistic mock content:

| Pattern Detected | Enhancement |
|---|---|
| `ListBox` / `ItemsControl` | 5 dummy items with realistic names |
| `"playlist"`, `"track"`, `"song"`, `"music"` | Song titles + artist names + durations + album art |
| `"subtitle"`, `"language"`, `"audio"` | Language names with checkmarks |
| `"file"`, `"document"`, `"recent"` | Document names + dates + sizes |
| `Slider` / "Volume" / "SeekBar" | Filled slider bar with thumb + percentage |
| `TextBox` / `PasswordBox` | Placeholder/watermark text |
| `CheckBox` → hint of "check" | Checkmark SVG + labeled box |
| `RadioButton` | Circle + inner dot + label |
| `TabControl` / `TabItem` | Active/inactive tabs with underline |
| `DataGrid` / table | 3 sample data rows with zebra striping |
| Empty state | Icon + message + action button |
| `ComboBox` | Selected item + dropdown arrow ▼ |

## Prompt-Based Workflow (for AI Models)

This toolkit is designed for AI-assisted workflows. See these documents:

| Document | Purpose |
|---|---|
| **[UNIVERSAL-PROMPT-ENGINE.md](UNIVERSAL-PROMPT-ENGINE.md)** | ★ Master reference — covers EVERYTHING |
| **[prompt-guide.md](prompt-guide.md)** | 8 scenario-specific prompt templates |
| **[examples/all-elements.md](examples/all-elements.md)** | Complete code patterns for every element |
| **[examples/penpot-read-script.js](examples/penpot-read-script.js)** | Ready-to-use MCP shape reader |

## MCP Tools Used (for Penpot)

| MCP Tool | Purpose |
|---|---|
| `execute_code` | Execute JavaScript in Penpot's plugin context |
| `high_level_overview` | Visual text-tree overview of current page |
| `export_shape` | Export shapes as PNG/SVG |
| `penpot_api_info` | Get Penpot JavaScript API docs |

## Troubleshooting

| Problem | Check |
|---|---|
| No shapes in Penpot | Did you register `helpers.js` first? |
| Fonts wrong | Run `storage.font.fontFamily` in MCP to verify font |
| Shapes at wrong positions | Check StackPanel stacking order / Grid row heights |
| Gradients missing | Check resolver output: `[resolver] Loaded: X gradients` |
| Converter errors | Try `--axaml-path` with explicit path |
| No files found | Point `--project-root` at the project root (where .axaml files are) |

## Adding New Element Support

See [UNIVERSAL-PROMPT-ENGINE.md](UNIVERSAL-PROMPT-ENGINE.md) section "HOW TO ADD A NEW ELEMENT TYPE" for step-by-step instructions.

## License

MIT — use in any project, commercial or personal.
